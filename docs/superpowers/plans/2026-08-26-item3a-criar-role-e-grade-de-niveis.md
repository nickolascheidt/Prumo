# Item 3A — Criar role por tenant e a grade Recurso × Nível

> **Para quem executa:** use `superpowers:subagent-driven-development` ou
> `superpowers:executing-plans`. Os passos usam checkbox (`- [ ]`).

**Objetivo:** o admin de um tenant cria uma role própria (ex.: "Leitura") e define, numa
grade, o nível dela em cada recurso — para que exista o caso "funcionário que vê mas não
edita".

**Arquitetura:** puramente **aditivo**. Nada é removido, o `RolePermission` continua vivo
e intocado, e o caminho de emissão de token **não é tocado** — essa limpeza é o 3B.
No backend, quase tudo já existe: a grade tem `GET /api/resources/role/{roleId}`,
`POST /api/resources/assign` e `DELETE /api/resources/remove`. **O que falta de verdade é
criar/excluir role, e o `TenantId` na role.**

**Stack:** .NET 10 / EF Core / Npgsql (PG 17) no backend; Angular 18 + Material no frontend.

---

## Por que este item existe, em uma frase

`ResourcePermission` (Role × Recurso → `None/Read/Write/Full`) **já é** o que gateia menu,
guards e API — o `[TenantModule]` infere o nível do verbo (`GET`→`Read`,
`POST/PUT/PATCH`→`Write`, `DELETE`→`Full`). Logo "vê mas não edita" **já é expressável**;
faltava a tela.

---

## Decisões que este plano executa (não reabrir)

| # | Decisão | Data |
|---|---|---|
| 1 | Role ganha **`TenantId` nullable**: `null` = canônica (as 5 de hoje + master), preenchido = criada por aquele tenant e só visível nele. | 2026-08-26 |
| 2 | **Homônimos entre tenants são permitidos.** Índice único vira `(NormalizedName, TenantId)`. | 2026-08-26 |
| 3 | A tela "Permissões por Role" **vira "Roles"** e ganha criar/excluir + a grade. Reverte a decisão de 2026-08-18 ("página separada"). | 2026-08-26 |
| 4 | **Role nova nasce vazia** — zero `ResourcePermission`. Fail-closed. | 2026-08-18 |
| 5 | Aposentar o `RolePermission` fica para o **3B**. | 2026-08-26 |

---

## A armadilha do Postgres, verificada nesta máquina

Índice único `(NormalizedName, TenantId)` com `TenantId = NULL` **não protege as roles
canônicas**: no Postgres, `NULL` não é igual a `NULL`, então duas roles "RH" globais
passam. Testado em PG 17.9 — inseriu 2 linhas.

A correção é `NULLS NOT DISTINCT` (PG 15+), **também testada**: bloqueia a segunda "RH"
global (`duplicate key value violates unique constraint`) e continua permitindo "Leitura"
em dois tenants diferentes. **É obrigatório**, senão as canônicas ficam desprotegidas.

## A armadilha do Identity

`RoleManager.FindByNameAsync` assume **unicidade global de nome** e, com homônimas,
devolve uma das duas arbitrariamente. Auditoria dos 20 usos:

| Onde | Quantos | Veredito |
|---|---|---|
| `UserManager.GetRolesAsync` / `AddToRoleAsync` / `IsInRoleAsync` (AuthService, ResourcePermissionService, DbInitializer) | 15 | **Seguros.** Operam em `AspNetUserRoles`, o sistema **global** — só o master admin e legado passam por lá. Feature roles por tenant vão por `TenantUserRole`. |
| `PermissionService.FindByNameAsync` (linhas 103, 133, 185) | 3 | **Do sistema que o 3B aposenta.** Neste plano são evitados, não corrigidos: **tudo que este item escreve opera por `RoleId`, nunca por nome.** |
| `DbInitializer.FindByNameAsync` / `RoleExistsAsync` (36, 64, 125) | 2 | **Seguros.** Só nomes canônicos, que têm `TenantId = null` e continuam únicos graças ao `NULLS NOT DISTINCT`. |

**Regra que atravessa o plano: nunca resolver role de tenant por nome. Sempre por `RoleId`.**

---

## Estrutura de arquivos

### Backend (`~/source/repos/SaaSBasePlatform`)

| Ação | Arquivo |
|---|---|
| Modificar | `Prumo.Domain/Entities/ApplicationRole.cs` — ganha `TenantId` |
| Criar | `Prumo.Infrastructure/Data/Configurations/ApplicationRoleConfiguration.cs` — o índice composto |
| Criar | migration `AddTenantIdToRoles` |
| Criar | `Prumo.Application/DTOs/Roles/RoleDtos.cs` |
| Criar | `Prumo.Application/Services/ITenantRoleAdminService.cs` + `TenantRoleAdminService.cs` |
| Criar | `Prumo.Api/Controllers/TenantRolesController.cs` — rota `api/tenants/{tenantId:guid}/roles` |
| Modificar | `Prumo.Api/Configuration/DependencyInjectionConfiguration.cs` — registrar o service |
| Modificar | `Prumo.Application/Services/TenantService.cs` — `assignable-roles` passa a incluir as do tenant |
| Criar | `Prumo.Tests/Services/TenantRoleAdminServiceTests.cs` |
| Criar | `Prumo.Tests/Architecture/RoleUniquenessTests.cs` |

### Frontend (`~/source/repos/SaaSBasePlatform-Angular`)

| Ação | Arquivo |
|---|---|
| Renomear/reescrever | `modules/admin/permissions-management.component.*` → `modules/admin/roles/roles.component.*` |
| Criar | `modules/admin/roles/create-role-dialog.component.ts` |
| Modificar | `core/services/api.service.ts` — criar/excluir role |
| Modificar | `core/models/*.ts` — `TenantRoleDto`, `ResourcePermissionDto` |
| Modificar | `app.routes.ts`, `layout.component.ts` — `/admin/permissions` → `/admin/roles` |

---

## Task 1: `TenantId` na role, com o índice que realmente protege

**Arquivos:**
- Modificar: `Prumo.Domain/Entities/ApplicationRole.cs`
- Criar: `Prumo.Infrastructure/Data/Configurations/ApplicationRoleConfiguration.cs`

- [ ] **Passo 1: a entidade**

`ApplicationRole.cs` inteiro:

```csharp
using Microsoft.AspNetCore.Identity;

namespace Prumo.Domain.Entities
{
    public class ApplicationRole : IdentityRole<Guid>
    {
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tenant dono desta role. <c>null</c> = role canônica do sistema (Administrador,
        /// RH, Financeiro, ContasAPagar, Funcionario, Cliente), visível em todos os tenants.
        /// Preenchido = criada por aquele tenant e visível só nele.
        /// </summary>
        /// <remarks>
        /// Deliberadamente <b>não</b> implementa <c>ITenantScoped</c>: o filtro global é
        /// fail-closed e esconderia as canônicas (que têm TenantId null) de todo mundo,
        /// inclusive do login. A filtragem é explícita nas listagens.
        /// </remarks>
        public Guid? TenantId { get; set; }
    }
}
```

- [ ] **Passo 2: a configuração do EF, com o índice**

Criar `Prumo.Infrastructure/Data/Configurations/ApplicationRoleConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prumo.Domain.Entities;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
    {
        public void Configure(EntityTypeBuilder<ApplicationRole> builder)
        {
            // O Identity cria RoleNameIndex único em NormalizedName. Com role por tenant,
            // dois tenants podem ter "Leitura", então a unicidade passa a ser por par.
            builder.HasIndex(r => new { r.NormalizedName, r.TenantId })
                   .HasDatabaseName("RoleNameIndex")
                   .IsUnique()
                   // OBRIGATÓRIO: no Postgres NULL != NULL, então sem isto duas roles
                   // canônicas "RH" (ambas TenantId null) passariam pelo índice.
                   // Verificado em PG 17.9: sem a flag insere 2 linhas; com ela, viola.
                   // Requer PG 15+.
                   .AreNullsDistinct(false);
        }
    }
}
```

> **Se `AreNullsDistinct` não existir** na versão do Npgsql deste projeto, o
> equivalente é `.HasFilter(null)` **não** resolve — nesse caso escreva o índice na
> migration à mão com SQL cru:
> `CREATE UNIQUE INDEX "RoleNameIndex" ON "AspNetRoles" ("NormalizedName", "TenantId") NULLS NOT DISTINCT;`

- [ ] **Passo 3: confirmar que a configuração é aplicada**

O `ApplicationDbContext` aplica configurações por assembly. Confirme:

```bash
grep -n "ApplyConfigurationsFromAssembly\|ApplyConfiguration" Prumo.Infrastructure/Data/ApplicationDbContext.cs
```

Se usar `ApplyConfigurationsFromAssembly`, nada a fazer. Se listar uma a uma, acrescente
`builder.ApplyConfiguration(new ApplicationRoleConfiguration());` **depois** da chamada a
`base.OnModelCreating(builder)` — antes dela o Identity sobrescreve o índice.

- [ ] **Passo 4: gerar a migration**

```bash
dotnet ef migrations add AddTenantIdToRoles -p Prumo.Infrastructure -s Prumo.Api
```

**Leia o arquivo gerado antes de aplicar.** Ele deve: adicionar a coluna `TenantId`
(nullable), **dropar** o `RoleNameIndex` antigo e recriar o composto. Se o `NULLS NOT
DISTINCT` não aparecer no SQL, acrescente à mão com `migrationBuilder.Sql(...)`.

- [ ] **Passo 5: aplicar e verificar no banco**

```bash
docker compose up -d
dotnet ef database update -p Prumo.Infrastructure -s Prumo.Api
```

```bash
docker exec -i saasbase-postgres psql -U postgres -d SaaSBasePlatformDb -c "\d \"AspNetRoles\""
```

Esperado: coluna `TenantId uuid` e o índice `RoleNameIndex` sobre
`(NormalizedName, TenantId)` com `NULLS NOT DISTINCT`.

- [ ] **Passo 6: provar que o índice morde, nos dois sentidos**

```bash
docker exec -i saasbase-postgres psql -U postgres -d SaaSBasePlatformDb -c "
-- (a) canônica duplicada deve FALHAR
INSERT INTO \"AspNetRoles\" (\"Id\",\"Name\",\"NormalizedName\",\"CreatedAt\",\"ConcurrencyStamp\",\"TenantId\")
VALUES (gen_random_uuid(),'RH','RH', now(), '', NULL);"
```

Esperado: **erro** `duplicate key value violates unique constraint "RoleNameIndex"`.

```bash
docker exec -i saasbase-postgres psql -U postgres -d SaaSBasePlatformDb -c "
-- (b) homônimas em tenants diferentes devem PASSAR
INSERT INTO \"AspNetRoles\" (\"Id\",\"Name\",\"NormalizedName\",\"CreatedAt\",\"ConcurrencyStamp\",\"TenantId\") VALUES
 (gen_random_uuid(),'Leitura','LEITURA', now(), '', '319e52f0-70ee-4b7a-9bcf-62e642a5ab74'),
 (gen_random_uuid(),'Leitura','LEITURA', now(), '', 'cbc67987-a72f-454f-bdbf-028d3434694f');
SELECT \"Name\", \"TenantId\" FROM \"AspNetRoles\" WHERE \"NormalizedName\"='LEITURA';"
```

Esperado: **2 linhas inseridas**. Depois **limpe**:

```bash
docker exec -i saasbase-postgres psql -U postgres -d SaaSBasePlatformDb -c "DELETE FROM \"AspNetRoles\" WHERE \"NormalizedName\"='LEITURA';"
```

- [ ] **Passo 7: suíte + commit**

Parar a API antes, senão dá `MSB3027`:
`Get-Process -Name "Prumo.Api" | Stop-Process -Force`

```bash
dotnet test
```

Esperado: **101/101** (nada mudou de comportamento ainda).

```bash
git add -A && git commit -F- <<'EOF'
feat(roles): let a role belong to a tenant

TenantId null keeps meaning "canonical role, visible everywhere"; a value means
the role was created by that tenant and is only visible there.

The unique index moves from NormalizedName to (NormalizedName, TenantId) with
NULLS NOT DISTINCT. That flag is not optional: Postgres treats NULL as distinct
from NULL, so without it two canonical "RH" rows would slip past the index.
Verified against PG 17.9 both ways -- the duplicate canonical is rejected, and
two tenants may each have their own "Leitura".

ApplicationRole deliberately does not implement ITenantScoped: the global filter
is fail-closed and would hide the canonical roles from everyone, login included.
EOF
```

---

## Task 2: criar e excluir role, por tenant

**Arquivos:**
- Criar: `Prumo.Application/DTOs/Roles/RoleDtos.cs`
- Criar: `Prumo.Application/Services/ITenantRoleAdminService.cs`
- Criar: `Prumo.Application/Services/TenantRoleAdminService.cs`
- Modificar: `Prumo.Api/Configuration/DependencyInjectionConfiguration.cs`

- [ ] **Passo 1: os DTOs**

```csharp
namespace Prumo.Application.DTOs.Roles
{
    public record CreateTenantRoleDto(string Name, string? Description);

    /// <param name="IsCanonical">Role do sistema: não pode ser excluída nem renomeada.</param>
    /// <param name="MemberCount">Quantos membros deste tenant carregam a role.</param>
    public record TenantRoleDto(
        Guid Id,
        string Name,
        string? Description,
        bool IsCanonical,
        int MemberCount);
}
```

- [ ] **Passo 2: a interface**

```csharp
using Prumo.Application.DTOs.Roles;

namespace Prumo.Application.Services
{
    public interface ITenantRoleAdminService
    {
        /// <summary>Canônicas + as criadas por este tenant.</summary>
        Task<IReadOnlyList<TenantRoleDto>> GetVisibleRolesAsync(Guid tenantId, CancellationToken ct = default);

        Task<TenantRoleDto> CreateAsync(Guid tenantId, CreateTenantRoleDto dto, CancellationToken ct = default);

        Task DeleteAsync(Guid tenantId, Guid roleId, CancellationToken ct = default);
    }
}
```

- [ ] **Passo 3: a implementação**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prumo.Application.DTOs.Roles;
using Prumo.Domain.Authorization;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;

namespace Prumo.Application.Services
{
    public class TenantRoleAdminService : ITenantRoleAdminService
    {
        private readonly ApplicationDbContext _context;
        private readonly RoleManager<ApplicationRole> _roleManager;

        public TenantRoleAdminService(ApplicationDbContext context, RoleManager<ApplicationRole> roleManager)
        {
            _context = context;
            _roleManager = roleManager;
        }

        public async Task<IReadOnlyList<TenantRoleDto>> GetVisibleRolesAsync(Guid tenantId, CancellationToken ct = default)
        {
            // Cross-tenant de propósito: as canônicas têm TenantId null e precisam
            // aparecer em todo tenant. O filtro é explícito no Where abaixo.
            var roles = await _roleManager.Roles
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == null || r.TenantId == tenantId)
                .OrderBy(r => r.Name)
                .ToListAsync(ct);

            var counts = await _context.TenantUserRoles
                .IgnoreQueryFilters()   // cross-tenant: contamos só este tenant, no Where
                .Where(tur => tur.TenantId == tenantId)
                .GroupBy(tur => tur.RoleId)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);

            return roles.Select(r => new TenantRoleDto(
                r.Id,
                r.Name!,
                r.Description,
                r.TenantId == null,
                counts.TryGetValue(r.Id, out var c) ? c : 0)).ToList();
        }

        public async Task<TenantRoleDto> CreateAsync(Guid tenantId, CreateTenantRoleDto dto, CancellationToken ct = default)
        {
            var name = (dto.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("O nome da role é obrigatório.");

            if (name.Length > 64)
                throw new ArgumentException("O nome da role deve ter no máximo 64 caracteres.");

            var normalized = _roleManager.NormalizeKey(name);

            // Colide com canônica? Reservamos esses nomes: deixar um tenant criar a
            // própria "RH" tornaria o nome ambíguo em toda tela e em todo log.
            var clashesWithCanonical = await _roleManager.Roles
                .IgnoreQueryFilters()   // cross-tenant: canônicas não pertencem a tenant
                .AnyAsync(r => r.NormalizedName == normalized && r.TenantId == null, ct);
            if (clashesWithCanonical)
                throw new InvalidOperationException($"'{name}' é uma role do sistema e não pode ser recriada.");

            var alreadyHere = await _roleManager.Roles
                .IgnoreQueryFilters()   // cross-tenant: o Where restringe a este tenant
                .AnyAsync(r => r.NormalizedName == normalized && r.TenantId == tenantId, ct);
            if (alreadyHere)
                throw new InvalidOperationException($"Já existe uma role '{name}' neste tenant.");

            var role = new ApplicationRole
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = dto.Description?.Trim(),
                TenantId = tenantId
            };

            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(" | ", result.Errors.Select(e => e.Description)));

            // Nasce vazia, de propósito (decisão de 2026-08-18): zero ResourcePermission.
            return new TenantRoleDto(role.Id, role.Name!, role.Description, false, 0);
        }

        public async Task DeleteAsync(Guid tenantId, Guid roleId, CancellationToken ct = default)
        {
            var role = await _roleManager.Roles
                .IgnoreQueryFilters()   // cross-tenant: a checagem de dono é o Where seguinte
                .FirstOrDefaultAsync(r => r.Id == roleId, ct);

            if (role == null)
                throw new KeyNotFoundException("Role não encontrada.");

            if (role.TenantId == null)
                throw new InvalidOperationException("Roles do sistema não podem ser excluídas.");

            // O gate que impede um tenant de apagar a role de outro.
            if (role.TenantId != tenantId)
                throw new KeyNotFoundException("Role não encontrada.");

            var inUse = await _context.TenantUserRoles
                .IgnoreQueryFilters()   // cross-tenant: o Where restringe a este tenant
                .AnyAsync(tur => tur.RoleId == roleId && tur.TenantId == tenantId, ct);
            if (inUse)
                throw new InvalidOperationException(
                    "Esta role ainda está atribuída a membros. Remova-a deles antes de excluir.");

            // As ResourcePermissions da role morrem junto; senão viram lixo apontando
            // para uma role inexistente, e um Id reaproveitado herdaria acesso.
            var grants = await _context.ResourcePermissions
                .IgnoreQueryFilters()   // cross-tenant: o Where restringe a este tenant
                .Where(rp => rp.RoleId == roleId && rp.TenantId == tenantId)
                .ToListAsync(ct);
            _context.ResourcePermissions.RemoveRange(grants);
            await _context.SaveChangesAsync(ct);

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(" | ", result.Errors.Select(e => e.Description)));
        }
    }
}
```

> **Sobre os `IgnoreQueryFilters`:** o `DataAccessHygieneTests` da fase 2 quebra o build
> se um deles não tiver comentário contendo "cross-tenant" nas 6 linhas acima
> (case-insensitive). Todos acima têm. **Não remova os comentários.**

- [ ] **Passo 4: registrar no DI**

Em `DependencyInjectionConfiguration.cs`, junto dos outros:

```csharp
services.AddScoped<ITenantRoleAdminService, TenantRoleAdminService>();
```

- [ ] **Passo 5: build**

```bash
dotnet build
```

Esperado: 0 erros.

- [ ] **Passo 6: commit**

```bash
git add -A && git commit -F- <<'EOF'
feat(roles): create and delete a tenant's own roles

Canonical names are reserved -- letting a tenant mint its own "RH" would make
the name ambiguous in every screen and log. Deleting takes the role's resource
grants with it, so a reused id cannot inherit access, and refuses while members
still carry the role.

Every lookup goes by RoleId, never by name: RoleManager.FindByNameAsync assumes
names are globally unique and would pick arbitrarily between two tenants'
homonyms.
EOF
```

---

## Task 3: os testes que prendem as regras de isolamento

**Arquivos:**
- Criar: `Prumo.Tests/Services/TenantRoleAdminServiceTests.cs`

- [ ] **Passo 1: escrever os testes**

Siga o padrão das classes vizinhas em `Prumo.Tests/Services/` (EF InMemory +
NSubstitute). Os seis casos que **têm** de existir:

```csharp
// 1. Cria role no tenant A e ela aparece em GetVisibleRoles(A)
// 2. ...e NÃO aparece em GetVisibleRoles(B)          <- o isolamento
// 3. GetVisibleRoles(B) ainda traz as canônicas       <- TenantId null é visível a todos
// 4. Criar "RH" (canônica) falha com InvalidOperationException
// 5. Excluir role do tenant A a partir do tenant B falha com KeyNotFoundException
// 6. Excluir role ainda atribuída a um membro falha com InvalidOperationException
```

Escreva-os com nomes descritivos em português, como as classes vizinhas
(`Select_tenant_response_body_carries_the_same_roles_as_the_token` é o estilo do repo —
frase completa, snake_case).

- [ ] **Passo 2: rodar**

Com a API **parada**:

```bash
dotnet test --filter "FullyQualifiedName~TenantRoleAdminServiceTests"
```

Esperado: 6 aprovados.

- [ ] **Passo 3: ver o teste 2 morder**

Troque, no `GetVisibleRolesAsync`, o filtro para `r => true` (ou seja, deixe vazar
cross-tenant). Rode de novo.

Esperado: o caso 2 **falha**. Restaure e confirme 6 aprovados.

- [ ] **Passo 4: suíte inteira + commit**

```bash
dotnet test
```

Esperado: **107** (101 + 6).

```bash
git add -A && git commit -m "test(roles): pin down that a tenant's role stays invisible to other tenants"
```

---

## Task 4: o controller

**Arquivos:**
- Criar: `Prumo.Api/Controllers/TenantRolesController.cs`

- [ ] **Passo 1: escrever o controller**

A regra do `CLAUDE.md` é absoluta: **rota com `{tenantId:guid}` + `[TenantModule]` na
classe**. O `TenantCoverageTests` quebra o build se uma action ficar descoberta.

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prumo.Api.Attributes;
using Prumo.Application.DTOs.Roles;
using Prumo.Application.Services;

namespace Prumo.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/tenants/{tenantId:guid}/roles")]
    [TenantModule("Role.Management")]
    public class TenantRolesController : ControllerBase
    {
        private readonly ITenantRoleAdminService _service;

        public TenantRolesController(ITenantRoleAdminService service) => _service = service;

        /// <summary>Canônicas + as criadas por este tenant.</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TenantRoleDto>>> GetAll(Guid tenantId, CancellationToken ct)
            => Ok(await _service.GetVisibleRolesAsync(tenantId, ct));

        [HttpPost]
        public async Task<ActionResult<TenantRoleDto>> Create(
            Guid tenantId, [FromBody] CreateTenantRoleDto dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(tenantId, dto, ct);
            return CreatedAtAction(nameof(GetAll), new { tenantId }, created);
        }

        [HttpDelete("{roleId:guid}")]
        public async Task<IActionResult> Delete(Guid tenantId, Guid roleId, CancellationToken ct)
        {
            await _service.DeleteAsync(tenantId, roleId, ct);
            return NoContent();
        }
    }
}
```

> **Níveis inferidos:** `GET`→`Read`, `POST`→`Write`, `DELETE`→`Full`. É o comportamento
> desejado — excluir role exige o nível mais alto em `Role.Management`.

> **Não trate exceções aqui.** O `ExceptionHandlingMiddleware` já mapeia
> `ArgumentException`→400 e `KeyNotFoundException`→404 desde o item 15.
> `InvalidOperationException` **precisa ser conferida**: veja no middleware para qual
> status ela cai, e se cair em 500, acrescente o mapeamento para **409 Conflict** —
> "nome já existe" e "role em uso" são conflitos, não erros de servidor.

- [ ] **Passo 2: conferir o mapeamento de `InvalidOperationException`**

```bash
grep -nE "InvalidOperationException|ArgumentException|KeyNotFoundException|StatusCode" Prumo.Api/Middleware/ExceptionHandlingMiddleware.cs
```

Se `InvalidOperationException` não estiver mapeada, acrescente o caso devolvendo
**409**, seguindo o estilo já usado ali.

- [ ] **Passo 3: build + suíte**

```bash
dotnet build && dotnet test
```

Esperado: 0 erros; **107** aprovados, incluindo `TenantCoverageTests` verde (o que prova
que as 3 actions estão cobertas pelo `[TenantModule]`).

- [ ] **Passo 4: commit**

```bash
git add -A && git commit -m "feat(api): expose tenant role creation under the tenant-scoped route"
```

---

## Task 5: a lista de roles atribuíveis passa a incluir as do tenant

Sem isto a role nasce e **não aparece** no painel "Chaves de módulo" da tela de membros —
que é exatamente o fluxo que o Nickolas descreveu.

**Arquivos:**
- Modificar: `Prumo.Application/Services/TenantService.cs` (o método por trás de
  `GET /api/tenants/{tenantId}/assignable-roles`)

- [ ] **Passo 1: achar o método**

```bash
grep -n "AssignableFeatureRoles\|GetAssignableRoles" Prumo.Application/Services/TenantService.cs
```

- [ ] **Passo 2: passar a consultar o Identity**

Substituir a devolução da lista fixa por: as canônicas atribuíveis
(`Permissions.Roles.AssignableFeatureRoles`, que **exclui o master de propósito**) **mais**
os nomes das roles com `TenantId == tenantId`. Ordenar por nome.

```csharp
var tenantRoleNames = await _context.Roles
    .IgnoreQueryFilters()   // cross-tenant: o Where restringe a este tenant
    .Where(r => r.TenantId == tenantId)
    .Select(r => r.Name!)
    .ToListAsync(ct);

return Permissions.Roles.AssignableFeatureRoles
    .Concat(tenantRoleNames)
    .OrderBy(n => n)
    .ToList();
```

> **Atenção ao master:** ele **não** entra aqui. `assignable-roles` responde "o que um
> admin de tenant pode atribuir", e excluir o master é intencional desde 2026-08-18.

- [ ] **Passo 3: teste**

Acrescente a `TenantRoleAdminServiceTests` (ou à classe de testes do `TenantService`,
onde couber melhor) um caso: **criar role no tenant A → `assignable-roles` de A a contém,
e o de B não.**

- [ ] **Passo 4: suíte + commit**

```bash
dotnet test
```

Esperado: **108**.

```bash
git add -A && git commit -m "feat(roles): offer a tenant's own roles as assignable keys"
```

---

## Task 6: a tela "Roles"

**Arquivos (repo Angular):**
- Criar: `src/app/modules/admin/roles/roles.component.{ts,html,scss}`
- Criar: `src/app/modules/admin/roles/create-role-dialog.component.ts`
- Modificar: `src/app/core/services/api.service.ts`
- Modificar: `src/app/core/models/` (o arquivo que define os DTOs de admin)
- Modificar: `src/app/app.routes.ts`, `src/app/shared/components/layout/layout.component.ts`
- Apagar: `src/app/modules/admin/permissions-management.component.{ts,html,scss}`

A tela tem **duas partes**: a lista de roles à esquerda (com `[+ Nova role]` e, nas não
canônicas, um botão de excluir) e, à direita, para a role selecionada, a **grade
Recurso × Nível**.

```
ROLES                                    [+ Nova role]

  RH            sistema    3 membros
  Financeiro    sistema    1 membro
▸ Leitura                  0 membros   [🗑]

  ── Leitura ────────────────────────────────────
  Recurso                 Nenhum  Ler  Escrever  Total
  Funcionários              ( )   (•)     ( )     ( )
  Horas                     ( )   (•)     ( )     ( )
  Contas a Pagar            (•)   ( )     ( )     ( )
```

- [ ] **Passo 1: métodos novos no `ApiService`**

Todos os outros já existem (`getRoleResourcePermissions`, `assignResourcePermission`,
`removeResourcePermission` — confira os nomes reais antes de duplicar):

```ts
getTenantRoles(tenantId: string): Observable<TenantRoleDto[]> {
  return this.http.get<TenantRoleDto[]>(
    `${this.apiUrl}/tenants/${encodeURIComponent(tenantId)}/roles`);
}

createTenantRole(tenantId: string, data: CreateTenantRoleRequest): Observable<TenantRoleDto> {
  return this.http.post<TenantRoleDto>(
    `${this.apiUrl}/tenants/${encodeURIComponent(tenantId)}/roles`, data);
}

deleteTenantRole(tenantId: string, roleId: string): Observable<void> {
  return this.http.delete<void>(
    `${this.apiUrl}/tenants/${encodeURIComponent(tenantId)}/roles/${encodeURIComponent(roleId)}`);
}
```

- [ ] **Passo 2: os models**

```ts
export interface TenantRoleDto {
  id: string;
  name: string;
  description?: string;
  /** Role do sistema: não pode ser excluída. */
  isCanonical: boolean;
  memberCount: number;
}

export interface CreateRoleRequest { name: string; description?: string; }
```

- [ ] **Passo 3: a grade — o ponto que exige cuidado**

`PermissionLevel` chega do backend **como string** (`"Read"`), não número. **Esta é a
armadilha que já mordeu três vezes neste repo** (`22aba79`, e de novo no item 5 com
`TenantRole`). Normalize na fronteira, como o `toTenantRole` faz em
`tenant-members.component.ts`:

```ts
/** A API serializa PermissionLevel como string ("Read"). Valor desconhecido vira None. */
export function toPermissionLevel(value: PermissionLevel | string | null | undefined): PermissionLevel {
  if (typeof value === 'number') { return value; }
  const parsed = typeof value === 'string'
    ? PermissionLevel[value as keyof typeof PermissionLevel]
    : undefined;
  return typeof parsed === 'number' ? parsed : PermissionLevel.None;
}
```

Escolher **Nenhum** na grade chama `removeResourcePermission`; qualquer outro nível chama
`assignResourcePermission` com o `Level`. Depois de escrever, **releia do servidor** —
mesmo motivo do item 5: a tela não pode divergir do que o gate vai enxergar.

- [ ] **Passo 4: estilos**

Reuse `.page-header`, `.page-header__icon` e `.role-chip` — já estão no `styles.scss`.
**Zero hex chumbado**, senão o `check-tokens.sh` quebra. Os tokens reais são
`--color-primary-light`, `--color-text-inverse`, `--color-warn-light`, `--color-surface-alt`
(**não** `--color-primary-surface` nem `--color-on-primary`, que não existem).

- [ ] **Passo 5: rota e menu**

`app.routes.ts`: trocar `admin/permissions` por `admin/roles` apontando para
`RolesComponent`, com `resourceAccessGuard` e
`data: { resource: 'Role.Management', requiredLevel: PermissionLevel.Read }`
(**`Role.Management`**, não `Permission.Management`). Acrescentar
`{ path: 'admin/permissions', redirectTo: 'admin/roles', pathMatch: 'full' }`.

`layout.component.ts`: a entrada "Permissões por Role" vira **"Roles"**, ícone
`admin_panel_settings`, `resourceCode: 'Role.Management'`.

- [ ] **Passo 6: apagar a tela velha e provar que ficou órfã**

```bash
grep -rn "PermissionsManagementComponent" src/ --include=*.ts
```

Esperado: nada além da própria declaração. Então `git rm` os três arquivos.

- [ ] **Passo 7: build + tokens**

```bash
npm run build:prod
```
```bash
bash .claude/skills/design-sync/check-tokens.sh
```

Esperado: zero erro nos dois.

- [ ] **Passo 8: commit**

```bash
git add -A && git commit -F- <<'EOF'
feat(admin): turn the permissions screen into a roles screen

Creating a role and setting what it may reach are the same job, so they became
one screen. The grid writes ResourcePermission levels -- the system that
actually gates the menu, the route guards and the API -- which is how "sees but
cannot edit" is expressed: Read on a resource passes GET and refuses POST.

PermissionLevel is normalized at the boundary; it crosses the wire as a string,
which has now bitten this repo three times.
EOF
```

---

## Task 7: verificação contra a API viva

**Pré-requisito:** `docker compose up -d`, depois `dotnet run --project Prumo.Api`, depois
`npm start`. (Subir a API antes do Postgres faz o startup falhar.)

- [ ] **Passo 1: criar a role**

Como admin, em **Administração › Roles**, criar **"Leitura"**. Esperado: aparece na lista,
`0 membros`, **sem** selo "sistema", com botão de excluir.

- [ ] **Passo 2: a role nasce vazia (fail-closed)**

Selecionar "Leitura". Esperado: **todos** os recursos em `Nenhum`. Confirmar:

```bash
docker exec -i saasbase-postgres psql -U postgres -d SaaSBasePlatformDb -t -c "
select count(*) from \"ResourcePermissions\" rp
join \"AspNetRoles\" r on r.\"Id\"=rp.\"RoleId\" where r.\"Name\"='Leitura';"
```

Esperado: **0**.

- [ ] **Passo 3: o caso de uso inteiro — vê mas não edita**

Dar a "Leitura" o nível **Ler** em `HR.Employees`. Depois, na tela de Membros, expandir um
membro descartável e marcar a chave **Leitura**.

Entrar como esse membro e verificar:

- **Funcionários aparece no menu** e a lista **abre** (GET → `Read`, passa)
- Tentar **salvar** um funcionário devolve **403** (POST → `Write`, nega)

Confirmar o 403 pela API, não só pela tela:

```bash
curl -s -o /dev/null -w "POST /employees -> %{http_code} (403 esperado)\n" \
  -X POST -H "Authorization: Bearer $TOKEN_DO_MEMBRO" -H "Content-Type: application/json" \
  -d '{"fullName":"x"}' http://localhost:5201/api/tenants/$TENANT_ID/employees
```

**Este passo é o item 3A inteiro.** Se ele passa, o objetivo foi cumprido.

- [ ] **Passo 4: o isolamento entre tenants**

Trocar para outro tenant (o seletor no topo). Esperado: **"Leitura" não aparece** nem na
tela de Roles nem nas chaves de módulo da tela de Membros. As canônicas continuam lá.

- [ ] **Passo 5: as recusas**

- Criar role chamada **"RH"** → erro claro ("é uma role do sistema"), **não** 500.
- Criar **"Leitura"** de novo no mesmo tenant → erro de nome duplicado.
- Excluir **"Leitura"** enquanto o membro a carrega → recusa pedindo para removê-la antes.
- Tentar excluir uma **canônica** → o botão nem aparece; e via API deve dar erro.

- [ ] **Passo 6: excluir de verdade**

Remover a chave do membro, depois excluir "Leitura". Confirmar que as
`ResourcePermissions` dela morreram junto:

```bash
docker exec -i saasbase-postgres psql -U postgres -d SaaSBasePlatformDb -t -c "
select count(*) from \"ResourcePermissions\" rp
left join \"AspNetRoles\" r on r.\"Id\"=rp.\"RoleId\" where r.\"Id\" is null;"
```

Esperado: **0** — nenhuma permissão órfã.

- [ ] **Passo 7: nada regrediu**

Parar a API (`Get-Process -Name "Prumo.Api" | Stop-Process -Force`), então `dotnet test`.
Esperado: **108**. E no frontend, `npm run build:prod` + `check-tokens.sh` limpos.

- [ ] **Passo 8: registrar**

Acrescentar ao fim deste arquivo uma seção `## Registro de execução (data)` com o que foi
verificado e **os erros deste plano** que a execução revelou.

---

## Task 8: fechar o 3A no backlog

- [ ] **Passo 1:** em `docs/MVP-BACKLOG.md`, marcar o item 3 como **3A feito, 3B pendente**,
  e registrar o que a execução mudou.
- [ ] **Passo 2:** commit no repo backend.

---

## Revisão do plano

**Cobertura:** criar role (Task 2/4), excluir (Task 2/4), role nasce vazia (Task 2, passo 3
e Task 7 passo 2), aparece nas chaves de módulo (Task 5), grade Recurso × Nível (Task 6),
isolamento por tenant (Tasks 1, 2, 3, 7), "vê mas não edita" (Task 7 passo 3).

**O que este plano deliberadamente NÃO faz** — tudo isto é 3B: aposentar
`RolePermission`, o catálogo `Permissions.cs`, as policies e o
`PermissionAuthorizationHandler`; remodelar o `PermissionAuditLog`; converter o
`PermissionsController`. **O caminho de emissão de token não é tocado.**

**Riscos conhecidos:** (a) o `NULLS NOT DISTINCT` é obrigatório e depende de PG 15+ — se
o deploy for para um Postgres mais velho, o índice precisa de outra estratégia; (b) a
migration mexe num índice **do Identity**, então vale ler o SQL gerado antes de aplicar;
(c) `PermissionLevel` chega como string — a Task 6 normaliza, e ignorar isso repetiria o
bug pela quarta vez.

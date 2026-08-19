# Fase 2 — limpeza do acesso a dados

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fazer o banco recusar dado de outro tenant por construção, para que as ~86
chamadas `IgnoreQueryFilters()` e as 23 checagens `CanAccess` deixem de ser necessárias —
e possam ser apagadas sem perder proteção.

**Architecture:** A fase 1 pôs `[TenantModule]` resolvendo o tenant **pela rota** antes da
action e um filtro global **fail-closed** no EF. A fase 2 colhe isso em três movimentos:
(1) estender o filtro global às 4 entidades que hoje não têm nenhum, via *query filter por
navegação*; (2) apagar os bypasses que viraram redundantes e as chamadas que nunca fizeram
nada; (3) travar a regressão com um teste de arquitetura.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, xUnit, Postgres 17 via `docker compose`.

---

## Antes de começar: o que a auditoria de 2026-08-18 estabeleceu

Ler isto primeiro. Três fatos que mudam o que este plano faz, todos verificados no código,
não supostos.

**1. Não existe vazamento hoje.** Os 22 sites das entidades sem `TenantId` foram lidos um
a um: todo caminho de leitura ou faz o join e filtra pelo pai
(`p.Employee.TenantId == tenantId`, `l.JournalEntry.TenantId == tenantId`), ou tem guard
antes provando que o pai é do tenant. Nenhum controller toca esses `DbSet` direto.
**Este plano não conserta bug — ele troca disciplina por estrutura.** Não escreva commit
message dizendo que fecha vazamento.

**2. Metade dos `IgnoreQueryFilters` é no-op.** Só **10 entidades são `ITenantScoped`** e
portanto têm filtro global: `Account`, `AccountsPayableCategory`, `AccountsPayableEntry`,
`Employee`, `JournalEntry`, `PermissionAuditLog`, `Resource`, `ResourcePermission`,
`RolePermission`, `TenantUserRole`. Em `Tenants`, `TenantUsers`, `WorkLogs`,
`PaymentPeriods` e `Payments` **não há filtro nenhum para ignorar** — a chamada é ruído que
faz o leitor achar que algo está sendo contornado.

| Categoria | Sites | O que fazer |
|---|---|---|
| Bypass real (entidade `ITenantScoped`) | ~43 | Apagar a chamada **e** o `Where` de `TenantId` |
| No-op (entidade sem filtro) | ~35 | Apagar só a chamada; o `Where` continua sendo a proteção |
| Cross-tenant legítimo (`DbInitializer`, login, seleção de tenant) | ~8 | **Manter**, com comentário dizendo por quê |

**3. `CanAccess` e `CanManage` NÃO são a mesma coisa.** Este é o erro que quebraria
segurança se o refactor fosse mecânico:

- `CanAccess(role) => role.HasValue` — "é membro do tenant". **Redundante**: o
  `[TenantModule]` já provou isso antes da action rodar. 23 ocorrências, podem sair.
- `CanManage(role) => role is Owner or Admin` — "é administrador do tenant". **NÃO é
  redundante**: o `[TenantModule]` checa associação e nível de permissão no recurso, nunca
  o cargo administrativo. 19 ocorrências, **todas ficam**. Apagá-las deixaria um Member
  gerenciar categorias e plano de contas.

---

## File Structure

| Arquivo | Responsabilidade nesta fase |
|---|---|
| `Prumo.Infrastructure/Data/ApplicationDbContext.cs` | Ganha os 4 filtros por navegação. É a única mudança de comportamento do plano. |
| `Prumo.Tests/Infrastructure/NavigationTenantFilterTests.cs` | **Novo.** Prova que as 4 entidades passam a filtrar sozinhas. |
| `Prumo.Tests/Architecture/DataAccessHygieneTests.cs` | **Novo.** Quebra o build quando um `IgnoreQueryFilters` novo aparece sem justificativa. |
| `Prumo.Application/Services/*.cs` | Perdem os bypasses redundantes. Sem mudança de comportamento. |
| `Prumo.Api/Controllers/*.cs` | Perdem as 23 checagens `CanAccess`. As 19 `CanManage` ficam. |
| `Prumo.Infrastructure/Data/DbInitializer.cs` | **Não muda o código**, só ganha comentários nos 5 sites cross-tenant legítimos. |

---

## Task 1: Filtro por navegação nas 4 entidades órfãs

`WorkLog`, `Payment`, `PaymentPeriod` e `JournalLine` não têm coluna `TenantId` — são
escopados via `Employee.TenantId` / `JournalEntry.TenantId`. O EF **já reclama disso a cada
boot**, 4 avisos `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning`,
mandando "define matching query filters for both entities". Esta task faz o que ele pede.

**Já foi verificado num spike em 2026-08-18:** o EF aceita, o build fica limpo, o startup
conclui sem erro de modelo, e o aviso correspondente some (foram de 4 para 3 com só o
`WorkLog` filtrado). Não é território desconhecido.

**Files:**
- Modify: `Prumo.Infrastructure/Data/ApplicationDbContext.cs:52-59`
- Test: `Prumo.Tests/Infrastructure/NavigationTenantFilterTests.cs` (criar)

- [ ] **Step 1: Escrever o teste que falha**

Criar `Prumo.Tests/Infrastructure/NavigationTenantFilterTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Infrastructure
{
    /// <summary>
    /// WorkLog, Payment, PaymentPeriod e JournalLine não têm coluna TenantId — são
    /// escopados pelo pai. Sem filtro por navegação, o isolamento deles depende
    /// inteiramente de o service lembrar do join, que é a disciplina que a fase 2
    /// troca por estrutura.
    /// </summary>
    public class NavigationTenantFilterTests
    {
        private static ApplicationDbContext NewDb(ITenantContext ctx, string dbName) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName).Options, ctx);

        [Fact]
        public async Task WorkLogs_of_another_tenant_are_invisible()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();

            var seedCtx = new TenantContext();
            seedCtx.SetTenant(tenantA);
            await using (var seed = NewDb(seedCtx, dbName))
            {
                var empA = new Employee { TenantId = tenantA, FullName = "A", CPF = "1" };
                var empB = new Employee { TenantId = tenantB, FullName = "B", CPF = "2" };
                seed.Employees.AddRange(empA, empB);
                seed.WorkLogs.AddRange(
                    new WorkLog { EmployeeId = empA.Id, WorkDate = DateTime.UtcNow.Date },
                    new WorkLog { EmployeeId = empB.Id, WorkDate = DateTime.UtcNow.Date });
                await seed.SaveChangesAsync();
            }

            var ctx = new TenantContext();
            ctx.SetTenant(tenantA);
            await using var db = NewDb(ctx, dbName);

            // Sem nenhum Where de tenant e sem guard: só o filtro pode proteger.
            var visible = await db.WorkLogs.ToListAsync();

            Assert.Single(visible);
        }

        [Fact]
        public async Task WorkLogs_are_invisible_when_no_tenant_is_resolved()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantA = Guid.NewGuid();

            var seedCtx = new TenantContext();
            seedCtx.SetTenant(tenantA);
            await using (var seed = NewDb(seedCtx, dbName))
            {
                var emp = new Employee { TenantId = tenantA, FullName = "A", CPF = "1" };
                seed.Employees.Add(emp);
                seed.WorkLogs.Add(new WorkLog { EmployeeId = emp.Id, WorkDate = DateTime.UtcNow.Date });
                await seed.SaveChangesAsync();
            }

            // Fail-closed: sem tenant resolvido, nada volta.
            await using var db = NewDb(new TenantContext(), dbName);

            Assert.Empty(await db.WorkLogs.ToListAsync());
        }
    }
}
```

- [ ] **Step 2: Rodar e ver falhar pelo motivo certo**

Rodar: `dotnet test --filter "FullyQualifiedName~NavigationTenantFilterTests"`

Esperado: **2 falhas.** `WorkLogs_of_another_tenant_are_invisible` falha com
`Assert.Single() Failure: The collection contained 2 items` — porque hoje não existe filtro
nenhum em `WorkLogs`. Se falhar com `NullReferenceException` ou erro de construção de
entidade, é erro de setup do teste: conserte e rode de novo até falhar pela asserção.

> **Nota sobre o InMemory provider:** ele avalia filtros por navegação em memória, então o
> teste é válido para *comportamento*, não para o SQL gerado. A verificação de que o
> Postgres gera o join certo está no Step 6.

- [ ] **Step 3: Implementar**

Em `Prumo.Infrastructure/Data/ApplicationDbContext.cs`, dentro de `OnModelCreating`, logo
depois de `ApplyTenantQueryFilters(modelBuilder);`:

```csharp
            ApplyNavigationTenantQueryFilters(modelBuilder);
        }

        /// <summary>
        /// WorkLog, Payment, PaymentPeriod e JournalLine não carregam TenantId — o dono do
        /// tenant é o pai (Employee ou JournalEntry). Sem estes filtros o isolamento delas
        /// depende de todo service lembrar do join, e o próprio EF avisa disso no startup
        /// (PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning), pedindo
        /// "matching query filters for both entities". Isto é esse matching filter.
        ///
        /// Não dá para reusar SetTenantQueryFilter: aquele exige ITenantScoped, e o ponto
        /// aqui é justamente que estas entidades não têm a coluna.
        /// </summary>
        private void ApplyNavigationTenantQueryFilters(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WorkLog>().HasQueryFilter(w =>
                _tenantContext != null && _tenantContext.HasTenant
                && w.Employee.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<Payment>().HasQueryFilter(p =>
                _tenantContext != null && _tenantContext.HasTenant
                && p.Employee.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<PaymentPeriod>().HasQueryFilter(p =>
                _tenantContext != null && _tenantContext.HasTenant
                && p.Employee.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<JournalLine>().HasQueryFilter(l =>
                _tenantContext != null && _tenantContext.HasTenant
                && l.JournalEntry.TenantId == _tenantContext.TenantId);
        }
```

- [ ] **Step 4: Rodar e ver passar**

Rodar: `dotnet test --filter "FullyQualifiedName~NavigationTenantFilterTests"`
Esperado: `Aprovado! – Com falha: 0, Aprovado: 2`

- [ ] **Step 5: Rodar a suíte inteira**

Rodar: `dotnet test`
Esperado: **90 aprovados, 0 falhas** (88 antes + 2).

Se algum teste de HR/financeiro quebrar aqui, **não desabilite o filtro**: quase certamente
o teste montava dado sem `TenantContext` e agora não enxerga o que criou. Corrija o setup do
teste para semear com tenant.

- [ ] **Step 6: Verificar contra Postgres de verdade**

O InMemory não prova o SQL. Subir a API e conferir que os avisos do EF sumiram:

```bash
docker compose up -d
dotnet run --project Prumo.Api --urls http://localhost:5201 > /tmp/api.log 2>&1 &
sleep 40
grep -c "PossibleIncorrectRequiredNavigation" /tmp/api.log
```

Esperado: **0** (eram 4). Depois, com a API viva, listar horas de um funcionário pela rota
`GET /api/tenants/{tenantId}/employees/{employeeId}/worklogs` e confirmar **200 com os
mesmos dados de antes** — o filtro não pode ter escondido dado legítimo.

- [ ] **Step 7: Commit**

```bash
git add Prumo.Infrastructure/Data/ApplicationDbContext.cs Prumo.Tests/Infrastructure/NavigationTenantFilterTests.cs
git commit -m "feat(security): give the four parent-scoped entities a query filter of their own"
```

Mensagem de commit **em inglês**, assunto e corpo. No corpo: explicar que estas 4 entidades
não têm TenantId, que o EF já pedia esse filtro no startup, e que isto não conserta
vazamento nenhum — troca disciplina por estrutura.

---

## Task 2: EmployeeService — estabelecer o padrão (5 sites, todos classe A)

O arquivo mais simples, e o modelo para os próximos. Todos os 5 sites têm a mesma forma:
`.IgnoreQueryFilters().Where(e => e.TenantId == tenantId)`. `Employee` **é** `ITenantScoped`,
então o filtro global já faz exatamente isso.

**Files:**
- Modify: `Prumo.Application/Services/EmployeeService.cs` (linhas 18, 26, 44, 76, 111)

- [ ] **Step 1: Confirmar que existe teste cobrindo o serviço**

Rodar: `dotnet test --filter "FullyQualifiedName~Employee"`
Anotar quantos passam. Este número não pode cair no fim da task.

- [ ] **Step 2: Aplicar a transformação**

Regra: apague `.IgnoreQueryFilters()` **e** a cláusula `x.TenantId == tenantId`. O parâmetro
`tenantId` do método continua existindo (a assinatura é contrato público), mas deixa de ser
usado na query — o filtro global usa o `TenantContext`, que o `[TenantModule]` preencheu
**a partir da mesma rota**, e rejeita divergência com o claim.

Antes (`EmployeeService.cs:18`):
```csharp
var query = _db.Employees.IgnoreQueryFilters().Where(e => e.TenantId == tenantId);
```
Depois:
```csharp
var query = _db.Employees.AsQueryable();
```

Antes (`EmployeeService.cs:26`):
```csharp
var e = await _db.Employees.IgnoreQueryFilters()
    .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == employeeId, ct);
```
Depois:
```csharp
var e = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct);
```

Antes (`EmployeeService.cs:44`):
```csharp
var cpfTaken = await _db.Employees.IgnoreQueryFilters()
    .AnyAsync(e => e.TenantId == tenantId && e.CPF == cpf, ct);
```
Depois:
```csharp
var cpfTaken = await _db.Employees.AnyAsync(e => e.CPF == cpf, ct);
```

Antes (`EmployeeService.cs:76` e `:111`, forma idêntica nos dois):
```csharp
var employee = await _db.Employees.IgnoreQueryFilters()
    .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == employeeId, ct)
    ?? throw new KeyNotFoundException("Employee not found.");
```
Depois:
```csharp
var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
    ?? throw new KeyNotFoundException("Employee not found.");
```

- [ ] **Step 3: Confirmar que sobrou zero no arquivo**

Rodar: `grep -c "IgnoreQueryFilters" Prumo.Application/Services/EmployeeService.cs`
Esperado: `0`

- [ ] **Step 4: Rodar a suíte inteira**

Rodar: `dotnet test`
Esperado: **90 aprovados, 0 falhas.** O número não pode cair.

- [ ] **Step 5: Verificar o isolamento na API viva**

Este é o passo que os testes não substituem, porque nenhum deles exercita o
`TenantResolutionMiddleware` + `[TenantModule]` juntos. Com a API rodando e dois tenants
semeados, repetir a matriz da fase 1 só para employees:

| Requisição | Esperado |
|---|---|
| `GET /api/tenants/{A}/employees` com tenant A selecionado | 200, e a **mesma lista de antes** |
| `GET /api/tenants/{B}/employees` com tenant A selecionado | 403 |
| `GET /api/tenants/{A}/employees` sem tenant selecionado | 403 |

O primeiro é o que importa nesta task: se voltar lista **vazia**, o filtro global não está
sendo alimentado e o refactor quebrou leitura legítima — pare e investigue o
`TenantContext` antes de seguir para os outros arquivos.

- [ ] **Step 6: Commit**

```bash
git add Prumo.Application/Services/EmployeeService.cs
git commit -m "refactor: drop the redundant tenant bypass from EmployeeService"
```

---

## Task 3: AccountService e JournalService (11 sites, classe A)

Mesma transformação da Task 2. `Account` e `JournalEntry` são `ITenantScoped`.

**Files:**
- Modify: `Prumo.Application/Services/AccountService.cs` (7 sites)
- Modify: `Prumo.Application/Services/JournalService.cs` (4 sites)

- [ ] **Step 1: Aplicar a mesma regra da Task 2, Step 2**

Apagar `.IgnoreQueryFilters()` e a cláusula `TenantId == tenantId` de cada site.

**Uma exceção neste par:** `AccountService.cs:125` consulta `_db.JournalLines` e **já não
usa** `IgnoreQueryFilters` — depois da Task 1 ela ganhou filtro por navegação. O guard de
`Account` logo acima (linhas 120-123) continua necessário, porque ele produz o
`KeyNotFoundException` que a API traduz em 404. **Não apague o guard.**

- [ ] **Step 2: Confirmar zero no arquivo**

```bash
grep -c "IgnoreQueryFilters" Prumo.Application/Services/AccountService.cs Prumo.Application/Services/JournalService.cs
```
Esperado: `0` nos dois.

- [ ] **Step 3: Rodar a suíte**

Rodar: `dotnet test` → **90 aprovados, 0 falhas**

- [ ] **Step 4: Verificar na API viva**

`GET /api/tenants/{A}/chart-of-accounts` e
`GET /api/tenants/{A}/general-ledger/journal-entries` com tenant A: **200 e mesma
quantidade de linhas de antes**. Cross-tenant: 403.

- [ ] **Step 5: Commit**

```bash
git add Prumo.Application/Services/AccountService.cs Prumo.Application/Services/JournalService.cs
git commit -m "refactor: drop the redundant tenant bypass from the ledger services"
```

---

## Task 4: AccountsPayableService (13 sites, o maior arquivo, classe A)

`AccountsPayableCategory` e `AccountsPayableEntry` são `ITenantScoped`.

**Files:**
- Modify: `Prumo.Application/Services/AccountsPayableService.cs`

- [ ] **Step 1: Aplicar a regra da Task 2, Step 2 aos 13 sites**

- [ ] **Step 2: Confirmar zero**

```bash
grep -c "IgnoreQueryFilters" Prumo.Application/Services/AccountsPayableService.cs
```
Esperado: `0`

- [ ] **Step 3: Rodar a suíte** → **90 aprovados, 0 falhas**

- [ ] **Step 4: Verificar na API viva**

`GET /api/tenants/{A}/accounts-payable/entries` e
`GET /api/tenants/{A}/accounts-payable/categories`: 200 e mesma contagem. Cross-tenant: 403.

- [ ] **Step 5: Commit**

```bash
git add Prumo.Application/Services/AccountsPayableService.cs
git commit -m "refactor: drop the redundant tenant bypass from AccountsPayableService"
```

---

## Task 5: WorkLog, Payment e PaymentPeriod (24 sites, classe B — a task mais delicada)

Aqui a Task 1 é o que torna a remoção segura. Estes services filtram por `EmployeeId` ou
por `x.Employee.TenantId`, e antes da Task 1 **não havia filtro nenhum** por baixo.

**Files:**
- Modify: `Prumo.Application/Services/WorkLogService.cs` (8)
- Modify: `Prumo.Application/Services/PaymentService.cs` (6)
- Modify: `Prumo.Application/Services/PaymentPeriodService.cs` (10)

- [ ] **Step 1: Apagar `.IgnoreQueryFilters()` e as cláusulas de tenant do pai**

Antes (`PaymentService.cs:59`):
```csharp
var p = await _db.Payments.IgnoreQueryFilters()
    .Include(p => p.Employee)
    .Include(p => p.PaidByUser)
    .FirstOrDefaultAsync(p => p.Id == paymentId && p.Employee.TenantId == tenantId, ct);
```
Depois:
```csharp
var p = await _db.Payments
    .Include(p => p.Employee)
    .Include(p => p.PaidByUser)
    .FirstOrDefaultAsync(p => p.Id == paymentId, ct);
```

- [ ] **Step 2: MANTER os guards, e trocar o motivo deles no comentário**

`RequireEmployeeAsync` (`WorkLogService.cs:133`) e os `exists`/lookup de employee
(`PaymentService.cs:81`, `PaymentPeriodService.cs:18` e `:83`) **não saem**. Depois da
Task 1 eles deixam de ser a proteção e passam a ser a **semântica de erro**: sem eles, pedir
horas de um employee de outro tenant devolveria `200 []` em vez de `404`, que é vazamento de
existência e uma mudança de contrato da API.

Acrescente o comentário acima de `RequireEmployeeAsync`:

```csharp
        /// <summary>
        /// Não é mais a proteção de tenant — desde a fase 2 o filtro por navegação em
        /// WorkLog cuida disso. Continua existindo pela semântica de erro: sem este guard,
        /// pedir horas de um employee de outro tenant devolveria 200 com lista vazia em vez
        /// de 404, vazando a informação de que o employee não existe *para você*.
        /// </summary>
```

- [ ] **Step 3: Confirmar zero nos três**

```bash
grep -c "IgnoreQueryFilters" Prumo.Application/Services/WorkLogService.cs Prumo.Application/Services/PaymentService.cs Prumo.Application/Services/PaymentPeriodService.cs
```
Esperado: `0` nos três.

- [ ] **Step 4: Rodar a suíte** → **90 aprovados, 0 falhas**

- [ ] **Step 5: Verificar na API viva, incluindo o caso de 404**

| Requisição | Esperado |
|---|---|
| `GET /tenants/{A}/employees/{empA}/worklogs` com tenant A | 200, mesma lista de antes |
| `GET /tenants/{A}/employees/{empB}/worklogs` (employee do tenant B) | **404**, não 200 vazio |
| `GET /tenants/{A}/payments` com tenant A | 200, mesma contagem |
| `GET /tenants/{B}/payments` com tenant A selecionado | 403 |

A segunda linha é a razão de o Step 2 existir. Se der `200 []`, o guard foi apagado por
engano.

- [ ] **Step 6: Commit**

```bash
git add Prumo.Application/Services/WorkLogService.cs Prumo.Application/Services/PaymentService.cs Prumo.Application/Services/PaymentPeriodService.cs
git commit -m "refactor: let the navigation filter carry HR tenant isolation"
```

---

## Task 6: Apagar os no-op de Tenants e TenantUsers (15 sites)

`Tenant` e `TenantUser` **não são** `ITenantScoped`. Nenhuma dessas 15 chamadas jamais
contornou coisa alguma. Aqui só se apaga a chamada — **os `Where` ficam todos**, porque
neste caso eles são a única proteção que existe e continuam sendo.

**Files:**
- Modify: `Prumo.Application/Services/TenantService.cs` (12 — 8 em `TenantUsers`, 4 em `Tenants`)
- Modify: `Prumo.Application/Services/TenantRoleService.cs` (3)

- [ ] **Step 1: Apagar só `.IgnoreQueryFilters()`, preservando cada `Where`**

Antes (`TenantService.cs:30`):
```csharp
var slugTaken = await _db.Tenants
    .IgnoreQueryFilters()
    .AnyAsync(t => t.Slug == slug, cancellationToken);
```
Depois:
```csharp
var slugTaken = await _db.Tenants
    .AnyAsync(t => t.Slug == slug, cancellationToken);
```

**Atenção:** esta query é cross-tenant **por definição** — unicidade de slug é global. Ela
está correta assim. O que muda é só parar de fingir que existia um filtro sendo contornado.

**Cuidado com `TenantUserRoles`:** `TenantUserRole` **é** `ITenantScoped`, ao contrário de
`TenantUser`. Os 3 sites de `TenantRoleService.cs` precisam ser lidos um a um — os que caem
em `TenantUserRoles` são classe A (apaga chamada **e** `Where` de tenant), os que caem em
`TenantUsers` são no-op (apaga só a chamada). Não trate o arquivo em bloco.

- [ ] **Step 2: Rodar a suíte** → **90 aprovados, 0 falhas**

- [ ] **Step 3: Verificar login e troca de tenant na API viva**

Estes são os caminhos cross-tenant legítimos, e são os que quebram feio se algo sair errado:

| Cenário | Esperado |
|---|---|
| `POST /api/auth/login` | 200 com token |
| `GET /api/tenants/me` | 200 com os 3 tenants |
| `POST /api/tenants/select` para cada um dos 3 | 200 |
| Criar tenant com slug já existente | 400/409, não duplica |

- [ ] **Step 4: Commit**

```bash
git add Prumo.Application/Services/TenantService.cs Prumo.Application/Services/TenantRoleService.cs
git commit -m "refactor: delete IgnoreQueryFilters calls that never bypassed anything"
```

---

## Task 7: Os arquivos que sobraram (5 sites)

**Files:**
- Modify: `Prumo.Application/Services/ResourcePermissionService.cs` (2)
- Modify: `Prumo.Infrastructure/Authorization/PermissionService.cs` (2)
- Modify: `Prumo.Application/Services/TenantGlSettingsService.cs` (1)

- [ ] **Step 1: Classificar os 5 antes de mexer**

`ResourcePermission` e `RolePermission` são `ITenantScoped` (classe A). `TenantGlSettings`
**não é** (no-op). Ler cada site e aplicar a regra da categoria correspondente.

- [ ] **Step 2: Rodar a suíte** → **90 aprovados, 0 falhas**

- [ ] **Step 3: Verificar o menu na API viva**

`GET /api/resources/my-permissions` com tenant selecionado: **200 e os mesmos 12 recursos**.
Este endpoint é o que alimenta o menu inteiro do frontend — se ele voltar vazio, o menu
some, que é exatamente o bug `73dc458` de volta.

- [ ] **Step 4: Commit**

```bash
git add Prumo.Application/Services/ResourcePermissionService.cs Prumo.Infrastructure/Authorization/PermissionService.cs Prumo.Application/Services/TenantGlSettingsService.cs
git commit -m "refactor: finish the tenant bypass cleanup in the permission services"
```

---

## Task 8: Documentar os cross-tenant que ficam (`DbInitializer`, 5 sites)

Estes **não saem**. São cross-tenant de propósito: backfill de roles legadas, limpeza da
role `User`, e o lookup do tenant `default`. O `DbInitializer` roda no startup, **sem
`TenantContext` nenhum** — sem `IgnoreQueryFilters` o filtro fail-closed devolveria zero
linha e o seeding quebraria em silêncio.

**Files:**
- Modify: `Prumo.Infrastructure/Data/DbInitializer.cs` (só comentários)

- [ ] **Step 1: Comentar cada um dos 5 sites**

Padrão a usar, adaptando o motivo:

```csharp
// Cross-tenant de propósito: o DbInitializer roda no startup, sem TenantContext.
// Sem IgnoreQueryFilters o filtro fail-closed devolveria zero linha e o seeding
// quebraria em silêncio. Ver DataAccessHygieneTests.
```

- [ ] **Step 2: Rodar a suíte** → **90 aprovados, 0 falhas**

- [ ] **Step 3: Commit**

```bash
git add Prumo.Infrastructure/Data/DbInitializer.cs
git commit -m "docs: say why the initializer's cross-tenant reads are deliberate"
```

---

## Task 9: Remover as 23 checagens `CanAccess` — e **só** elas

**Leia de novo o fato 3 do topo antes de começar.** `CanAccess` sai, `CanManage` fica.

**Files:**
- Modify: `Prumo.Api/Controllers/AccountsPayableCategoriesController.cs`
- Modify: `Prumo.Api/Controllers/AccountsPayableEntriesController.cs`
- Modify: `Prumo.Api/Controllers/ChartOfAccountsController.cs`
- Modify: `Prumo.Api/Controllers/EmployeesController.cs`
- Modify: `Prumo.Api/Controllers/GeneralLedgerController.cs`
- Modify: `Prumo.Api/Controllers/PaymentPeriodsController.cs`
- Modify: `Prumo.Api/Controllers/PaymentsController.cs`
- Modify: `Prumo.Api/Controllers/WorkLogsController.cs`

- [ ] **Step 1: Contar antes**

```bash
grep -rn "if (!CanAccess(" --include=*.cs Prumo.Api/Controllers/ | wc -l   # espera 23
grep -rnE "if \(!CanManage" --include=*.cs Prumo.Api/Controllers/ | wc -l  # espera 19
```

- [ ] **Step 2: Apagar as linhas `CanAccess` e o helper**

Apagar cada `if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();` e, quando o
helper `private static bool CanAccess(TenantRole? role) => role.HasValue;` ficar sem uso,
apagar o helper também. Se `GetRoleAsync` ficar sem nenhum chamador num controller, apague-o
junto; se ainda for usado pelo `CanManage`, **fica**.

- [ ] **Step 3: Contar depois**

```bash
grep -rn "if (!CanAccess(" --include=*.cs Prumo.Api/Controllers/ | wc -l   # espera 0
grep -rnE "if \(!CanManage" --include=*.cs Prumo.Api/Controllers/ | wc -l  # espera 19 — INALTERADO
```

Se o segundo número não for exatamente 19, alguma checagem de cargo administrativo foi
apagada. Reverta e refaça.

- [ ] **Step 4: Rodar a suíte, incluindo o teste de arquitetura da fase 1** → **90 aprovados**

`TenantCoverageTests` continua exigindo `[TenantModule]` em toda action sob `{tenantId}` —
é ele que garante que remover `CanAccess` não descobre nada.

- [ ] **Step 5: Verificar na API viva que o cargo ainda gateia**

Com o Member de feature role `RH` (criar por `POST /tenants/{id}/users` com `Role: 0`, depois
`POST /tenants/{id}/members/{uid}/roles` com `{"RoleName":"RH"}`):

| Requisição como Member | Esperado |
|---|---|
| `GET /tenants/{A}/employees` | 200 |
| `POST /tenants/{A}/accounts-payable/categories` | **403** (é `CanManage`, tem que sobreviver) |
| `GET /tenants/{B}/employees` | 403 |

A linha do meio é a que prova que a Task 9 não quebrou segurança.

- [ ] **Step 6: Commit**

```bash
git add Prumo.Api/Controllers/
git commit -m "refactor: drop the membership checks the tenant gate already performs"
```

---

## Task 10: Travar a regressão com teste de arquitetura

Sem isto, a fase 2 é um mutirão que se desfaz no primeiro service novo. Este teste é o
"lugar só que erra fechado" que o item 10 do backlog pedia.

**Files:**
- Test: `Prumo.Tests/Architecture/DataAccessHygieneTests.cs` (criar)

- [ ] **Step 1: Escrever o teste**

```csharp
using System.Text.RegularExpressions;

namespace Prumo.Tests.Architecture
{
    /// <summary>
    /// A fase 2 removeu ~78 IgnoreQueryFilters que tinham virado redundantes ou que nunca
    /// fizeram nada. O que sobra é legítimo e está documentado. Este teste existe para que
    /// o próximo não entre sem justificativa — sem ele, a limpeza se desfaz sozinha.
    /// </summary>
    public class DataAccessHygieneTests
    {
        [Fact]
        public void Every_IgnoreQueryFilters_call_is_justified_by_a_comment()
        {
            var root = FindRepoRoot();
            var offenders = new List<string>();

            var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !f.Contains("Prumo.Tests"));

            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].Contains("IgnoreQueryFilters")) continue;

                    // Justificativa = comentário nas 6 linhas acima contendo "cross-tenant".
                    var start = Math.Max(0, i - 6);
                    var justified = lines[start..i]
                        .Any(l => l.TrimStart().StartsWith("//") && l.Contains("cross-tenant"));

                    if (!justified)
                        offenders.Add($"{Path.GetRelativePath(root, file)}:{i + 1}");
                }
            }

            Assert.True(offenders.Count == 0,
                "IgnoreQueryFilters sem justificativa. Ou o filtro global já cobre este caso "
                + "e a chamada deve sair, ou a leitura é cross-tenant de propósito e precisa "
                + "de um comentário acima contendo 'cross-tenant' explicando por quê.\n  "
                + string.Join("\n  ", offenders));
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Prumo.slnx")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Prumo.slnx não encontrado.");
        }
    }
}
```

- [ ] **Step 2: Rodar e ver passar**

Rodar: `dotnet test --filter "FullyQualifiedName~DataAccessHygieneTests"`
Esperado: **1 aprovado.** Se falhar, ele lista o arquivo e a linha — ou a chamada devia ter
saído numa task anterior, ou falta o comentário da Task 8.

- [ ] **Step 3: Ver o teste morder**

Acrescente `.IgnoreQueryFilters()` a qualquer query de `EmployeeService.cs`, sem comentário.
Rodar de novo: tem que **falhar** nomeando `Prumo.Application/Services/EmployeeService.cs` e
a linha. Desfaça em seguida.

Este passo não é opcional: um teste de arquitetura que nunca foi visto falhando é um teste
que talvez não teste nada — foi exatamente o que aconteceu com `DbInitializerCleanupTests`,
que duplica a lista de roles e assere sobre a própria cópia.

- [ ] **Step 4: Rodar a suíte inteira**

Rodar: `dotnet test`
Esperado: **91 aprovados, 0 falhas**.

- [ ] **Step 5: Commit**

```bash
git add Prumo.Tests/Architecture/DataAccessHygieneTests.cs
git commit -m "test: fail the build when an unjustified tenant bypass appears"
```

---

## Fora do escopo desta fase, de propósito

O escopo original da fase 2 (escrito no fim do plano da fase 1) também listava **"remover o
`[RequireResourceAccess]` do `PermissionsController`"**. Isso **não entra aqui**, e o motivo
precisa ficar registrado para ninguém achar que foi esquecido.

Aquela frase só faz sentido se o `PermissionsController` virar um controller roteado por
tenant e passar a usar `[TenantModule]`. Mas ele **não tem `{tenantId}` na rota** — é
`api/permissions/...` — e resolve o tenant pelo `TenantContext`, que vem do claim do JWT.
Remover o `[RequireResourceAccess]` sem trocar a rota deixaria o controller **sem gate
nenhum**, que é o oposto do que a fase 2 quer.

Converter a rota é mudança **quebrante para o frontend**: `api.service.ts` chama
`/permissions`, `/permissions/catalog`, `/permissions/roles`, `/permissions/roles/{nome}`,
`/permissions/roles/{nome}/grant` e `/permissions/roles/{nome}/revoke/{permissão}` — seis
chamadas, mais o `GET /permissions/roles` acrescentado em 2026-08-18. É trabalho de
backend + frontend coordenado, não limpeza.

**Vira item próprio no backlog**, para ser decidido junto com o item 3 (criar/excluir role),
que mexe nas mesmas telas. Enquanto isso o `[RequireResourceAccess]` fica onde está e
continua sendo a proteção correta daquele controller.

---

## Fechamento

- [ ] **Contagem final**

```bash
grep -rn "IgnoreQueryFilters" --include=*.cs . | grep -v obj | grep -v bin | grep -v Prumo.Tests | wc -l
```
Esperado: **~8** (só os cross-tenant documentados), vindo de 86.

- [ ] **Suíte:** 91 aprovados, 0 falhas.

- [ ] **Avisos do EF no startup:** 0 `PossibleIncorrectRequiredNavigation` (eram 4).

- [ ] **Smoke test do frontend**, com as 12 telas abertas e o log da API sem nenhum não-2xx.
      A fase 2 não deveria mudar nada visível — se mudou, é regressão.

- [ ] **Atualizar** `docs/MVP-BACKLOG.md` (itens 10 e 13) e o `CLAUDE.md` do backend, que
      hoje diz "Quem precisa ler cross-tenant de propósito chama `IgnoreQueryFilters()`
      explicitamente" — continua verdade, mas agora com o teste de arquitetura por trás.
      **Lembrar que o `CLAUDE.md` é gitignored**: editá-lo nunca entra em commit.

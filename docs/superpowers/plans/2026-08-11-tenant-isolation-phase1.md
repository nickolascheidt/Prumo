# Isolamento de tenant no backend — Fase 1 (plano de implementação)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tornar impossível que um endpoint novo vaze dado de outro tenant ou autorize quem não devia, mesmo que o desenvolvedor esqueça toda a checagem.

**Architecture:** Um filtro de autorização `[TenantModule("code")]` por controller de módulo prova a associação contra o `tenantId` da **rota**, rejeita divergência entre rota e claim, preenche o `TenantContext` a partir da rota e só então checa a permissão de recurso. O filtro global do EF passa a ser fail-closed. Um teste de arquitetura quebra o build quando uma action sob `{tenantId}` fica descoberta. Fase aditiva: nada das proteções existentes é removido.

**Tech Stack:** .NET 10 / ASP.NET Core, EF Core 10 (PostgreSQL em runtime, InMemory nos testes), xUnit + NSubstitute.

**Spec:** `docs/superpowers/specs/2026-08-11-backend-tenant-isolation-design.md`

---

## Estrutura de arquivos

| Arquivo | Responsabilidade |
|---|---|
| `Prumo.Api/Attributes/TenantModuleAttribute.cs` | **Criar.** O gate único: identidade, rota, claim, associação, `TenantContext`, permissão de recurso. |
| `Prumo.Api/Controllers/*.cs` (9 arquivos) | **Modificar.** Recebem o atributo. |
| `Prumo.Infrastructure/Data/ApplicationDbContext.cs:75-81` | **Modificar.** Filtro global fail-closed. |
| `Prumo.Infrastructure/Data/ApplicationDbContextFactory.cs` | **Modificar.** Lê configuração em vez da base chumbada. |
| `Prumo.Infrastructure/Data/DbInitializer.cs` | **Modificar.** Apaga `EnsureTenantBootstrapAsync`; credencial do admin por ambiente. |
| `Prumo.Infrastructure/Data/Seeders/TenantBootstrapSeeder.cs` | **Modificar.** Expõe os códigos do catálogo para o teste de arquitetura. |
| `Prumo.Infrastructure/Migrations/*_BackfillTenantResources.cs` | **Criar.** Backfill de catálogo para tenants existentes. |
| `Prumo.Api/Controllers/TenantsController.cs` | **Modificar.** Restringe criação; endpoint de suporte. |
| `Prumo.Tests/Api/TenantModuleAttributeTests.cs` | **Criar.** Os seis ramos de decisão do filtro. |
| `Prumo.Tests/Architecture/TenantCoverageTests.cs` | **Criar.** As duas asserções de arquitetura. |
| `Prumo.Tests/Infrastructure/TenantQueryFilterTests.cs` | **Criar.** Fail-closed. |

**Ordem obrigatória:** Task 1 → 2 → 3 antes das demais. As tasks 4 a 8 são independentes entre si depois disso.

---

### Task 1: O atributo `[TenantModule]`

**Files:**
- Modify: `Prumo.Tests/Prumo.Tests.csproj`
- Create: `Prumo.Api/Attributes/TenantModuleAttribute.cs`
- Test: `Prumo.Tests/Api/TenantModuleAttributeTests.cs`

- [ ] **Step 1: Dar ao projeto de testes acesso à API**

`Prumo.Tests` hoje referencia Application, Domain e Infrastructure, mas **não** a Api. Sem isso nada desta task compila. Em `Prumo.Tests/Prumo.Tests.csproj`, no `ItemGroup` de `ProjectReference`, adicione:

```xml
    <ProjectReference Include="..\Prumo.Api\Prumo.Api.csproj" />
```

Referenciar um projeto Web SDK traz o `FrameworkReference` do `Microsoft.AspNetCore.App` de forma transitiva, que é o que dá acesso a `DefaultHttpContext` e `AuthorizationFilterContext`.

- [ ] **Step 2: Verificar que o projeto ainda compila**

Run: `dotnet build Prumo.Tests/Prumo.Tests.csproj`
Expected: `0 Erro(s)`. Se falhar com tipo `DefaultHttpContext` não encontrado numa etapa posterior, acrescente também `<FrameworkReference Include="Microsoft.AspNetCore.App" />` num `ItemGroup` do mesmo csproj.

- [ ] **Step 3: Escrever os testes que falham**

Crie `Prumo.Tests/Api/TenantModuleAttributeTests.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Prumo.Api.Attributes;
using Prumo.Application.Services;
using Prumo.Domain.Common;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Multitenancy;
using System.Security.Claims;

namespace Prumo.Tests.Api
{
    public class TenantModuleAttributeTests
    {
        private const string Code = "HR.Employees";

        private static AuthorizationFilterContext BuildContext(
            Guid? userId,
            Guid? claimTenantId,
            Guid? routeTenantId,
            string method,
            ITenantService tenantService,
            IResourcePermissionService permissions,
            ITenantContext tenantContext)
        {
            var claims = new List<Claim>();
            if (userId.HasValue)
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            if (claimTenantId.HasValue)
                claims.Add(new Claim("tenant_id", claimTenantId.Value.ToString()));

            var services = new ServiceCollection();
            services.AddSingleton(tenantService);
            services.AddSingleton(permissions);
            services.AddSingleton(tenantContext);

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
                RequestServices = services.BuildServiceProvider()
            };
            httpContext.Request.Method = method;

            var routeData = new RouteData();
            if (routeTenantId.HasValue)
                routeData.Values["tenantId"] = routeTenantId.Value.ToString();

            var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
            return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        }

        private static (ITenantService, IResourcePermissionService) Allowing(
            Guid tenantId, Guid userId, TenantRole role = TenantRole.Member)
        {
            var tenants = Substitute.For<ITenantService>();
            tenants.GetUserRoleAsync(tenantId, userId, Arg.Any<CancellationToken>())
                   .Returns(Task.FromResult<TenantRole?>(role));

            var permissions = Substitute.For<IResourcePermissionService>();
            permissions.UserHasAccessAsync(userId, Code, Arg.Any<PermissionLevel>())
                       .Returns(Task.FromResult(true));

            return (tenants, permissions);
        }

        [Fact]
        public async Task Member_with_matching_route_and_claim_is_allowed()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var (tenants, permissions) = Allowing(tenantId, userId);
            var tenantContext = new TenantContext();

            var ctx = BuildContext(userId, tenantId, tenantId, "GET", tenants, permissions, tenantContext);
            await new TenantModuleAttribute(Code).OnAuthorizationAsync(ctx);

            Assert.Null(ctx.Result);
            Assert.Equal(tenantId, tenantContext.TenantId);
            Assert.Equal(TenantRole.Member, ctx.HttpContext.Items[TenantModuleAttribute.TenantRoleItemKey]);
        }

        [Fact]
        public async Task Claim_tenant_different_from_route_is_forbidden()
        {
            var routeTenant = Guid.NewGuid();
            var claimTenant = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var (tenants, permissions) = Allowing(routeTenant, userId);

            var ctx = BuildContext(userId, claimTenant, routeTenant, "GET", tenants, permissions, new TenantContext());
            await new TenantModuleAttribute(Code).OnAuthorizationAsync(ctx);

            Assert.IsType<ForbidResult>(ctx.Result);
        }

        [Fact]
        public async Task Missing_tenant_claim_is_forbidden()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var (tenants, permissions) = Allowing(tenantId, userId);

            var ctx = BuildContext(userId, null, tenantId, "GET", tenants, permissions, new TenantContext());
            await new TenantModuleAttribute(Code).OnAuthorizationAsync(ctx);

            Assert.IsType<ForbidResult>(ctx.Result);
        }

        [Fact]
        public async Task Non_member_is_forbidden()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var tenants = Substitute.For<ITenantService>();
            tenants.GetUserRoleAsync(tenantId, userId, Arg.Any<CancellationToken>())
                   .Returns(Task.FromResult<TenantRole?>(null));
            var permissions = Substitute.For<IResourcePermissionService>();
            permissions.UserHasAccessAsync(userId, Code, Arg.Any<PermissionLevel>())
                       .Returns(Task.FromResult(true));

            var ctx = BuildContext(userId, tenantId, tenantId, "GET", tenants, permissions, new TenantContext());
            await new TenantModuleAttribute(Code).OnAuthorizationAsync(ctx);

            Assert.IsType<ForbidResult>(ctx.Result);
        }

        [Fact]
        public async Task Member_without_resource_permission_is_forbidden()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var tenants = Substitute.For<ITenantService>();
            tenants.GetUserRoleAsync(tenantId, userId, Arg.Any<CancellationToken>())
                   .Returns(Task.FromResult<TenantRole?>(TenantRole.Member));
            var permissions = Substitute.For<IResourcePermissionService>();
            permissions.UserHasAccessAsync(userId, Code, Arg.Any<PermissionLevel>())
                       .Returns(Task.FromResult(false));

            var ctx = BuildContext(userId, tenantId, tenantId, "GET", tenants, permissions, new TenantContext());
            await new TenantModuleAttribute(Code).OnAuthorizationAsync(ctx);

            Assert.IsType<ForbidResult>(ctx.Result);
        }

        [Fact]
        public async Task Unauthenticated_user_is_unauthorized()
        {
            var tenantId = Guid.NewGuid();
            var tenants = Substitute.For<ITenantService>();
            var permissions = Substitute.For<IResourcePermissionService>();

            var ctx = BuildContext(null, tenantId, tenantId, "GET", tenants, permissions, new TenantContext());
            await new TenantModuleAttribute(Code).OnAuthorizationAsync(ctx);

            Assert.IsType<UnauthorizedObjectResult>(ctx.Result);
        }

        [Theory]
        [InlineData("GET", PermissionLevel.Read)]
        [InlineData("POST", PermissionLevel.Write)]
        [InlineData("PUT", PermissionLevel.Write)]
        [InlineData("PATCH", PermissionLevel.Write)]
        [InlineData("DELETE", PermissionLevel.Full)]
        public async Task Required_level_is_derived_from_the_http_verb(string method, PermissionLevel expected)
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var (tenants, permissions) = Allowing(tenantId, userId);

            var ctx = BuildContext(userId, tenantId, tenantId, method, tenants, permissions, new TenantContext());
            await new TenantModuleAttribute(Code).OnAuthorizationAsync(ctx);

            await permissions.Received(1).UserHasAccessAsync(userId, Code, expected);
        }

        [Fact]
        public async Task Explicit_level_overrides_the_verb()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var (tenants, permissions) = Allowing(tenantId, userId);

            var ctx = BuildContext(userId, tenantId, tenantId, "GET", tenants, permissions, new TenantContext());
            await new TenantModuleAttribute(Code, PermissionLevel.Full).OnAuthorizationAsync(ctx);

            await permissions.Received(1).UserHasAccessAsync(userId, Code, PermissionLevel.Full);
        }
    }
}
```

- [ ] **Step 4: Rodar e confirmar que falha**

Run: `dotnet test --filter "FullyQualifiedName~TenantModuleAttributeTests"`
Expected: falha de compilação — `TenantModuleAttribute` não existe.

- [ ] **Step 5: Implementar o atributo**

Crie `Prumo.Api/Attributes/TenantModuleAttribute.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Prumo.Application.Services;
using Prumo.Domain.Common;
using Prumo.Domain.Enums;
using System.Security.Claims;

namespace Prumo.Api.Attributes
{
    /// <summary>
    /// Gate único dos controllers roteados por tenant. Prova a associação contra o
    /// tenantId da ROTA, rejeita divergência com o claim, preenche o TenantContext a
    /// partir da rota e só então checa a permissão de recurso — nessa ordem, porque a
    /// checagem de recurso resolve as roles usando o TenantContext.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class TenantModuleAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public const string TenantRoleItemKey = "Prumo.TenantRole";

        private const string TenantIdClaim = "tenant_id";
        private const string TenantIdRouteKey = "tenantId";

        public string ResourceCode { get; }
        private readonly PermissionLevel? _explicitLevel;

        public TenantModuleAttribute(string resourceCode)
        {
            ResourceCode = resourceCode;
        }

        public TenantModuleAttribute(string resourceCode, PermissionLevel level)
        {
            ResourceCode = resourceCode;
            _explicitLevel = level;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var http = context.HttpContext;

            // 1. Quem é o usuário — o único dado confiável, porque vem do token assinado.
            var userIdRaw = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdRaw, out var userId))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "User not authenticated" });
                return;
            }

            // 2. Qual tenant está sendo pedido. Entrada não confiável: alvo da prova.
            if (!context.RouteData.Values.TryGetValue(TenantIdRouteKey, out var routeRaw)
                || !Guid.TryParse(routeRaw?.ToString(), out var routeTenantId))
            {
                context.Result = new ForbidResult();
                return;
            }

            // 3. O tenant selecionado no token tem que existir e ser o mesmo da rota.
            var claimRaw = http.User.FindFirst(TenantIdClaim)?.Value;
            if (!Guid.TryParse(claimRaw, out var claimTenantId) || claimTenantId != routeTenantId)
            {
                context.Result = new ForbidResult();
                return;
            }

            // 4. Prova de associação contra o banco.
            var tenantService = http.RequestServices.GetRequiredService<ITenantService>();
            var role = await tenantService.GetUserRoleAsync(routeTenantId, userId, http.RequestAborted);
            if (role is null)
            {
                context.Result = new ForbidResult();
                return;
            }

            http.Items[TenantRoleItemKey] = role.Value;

            // 5. TenantContext passa a vir da rota. Daqui em diante contexto e rota são
            //    iguais por construção — é isso que torna o passo 6 correto.
            http.RequestServices.GetRequiredService<ITenantContext>().SetTenant(routeTenantId);

            // 6. Permissão de recurso, resolvida no tenant certo.
            var level = _explicitLevel ?? LevelForMethod(http.Request.Method);
            var permissions = http.RequestServices.GetRequiredService<IResourcePermissionService>();
            if (!await permissions.UserHasAccessAsync(userId, ResourceCode, level))
            {
                context.Result = new ForbidResult();
            }
        }

        internal static PermissionLevel LevelForMethod(string method) =>
            method.ToUpperInvariant() switch
            {
                "GET" or "HEAD" or "OPTIONS" => PermissionLevel.Read,
                "POST" or "PUT" or "PATCH" => PermissionLevel.Write,
                _ => PermissionLevel.Full
            };
    }
}
```

- [ ] **Step 6: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~TenantModuleAttributeTests"`
Expected: `Aprovado: 12` (8 fatos + 5 casos do Theory, menos sobreposição — o número exato importa menos que `Com falha: 0`).

- [ ] **Step 7: Commit**

```bash
git add Prumo.Api/Attributes/TenantModuleAttribute.cs Prumo.Tests/Api/TenantModuleAttributeTests.cs Prumo.Tests/Prumo.Tests.csproj
git commit -m "feat(security): add [TenantModule], the single tenant gate"
```

---

### Task 2: Aplicar o atributo nos 9 controllers

**Files:**
- Modify: `Prumo.Api/Controllers/EmployeesController.cs`, `WorkLogsController.cs`, `PaymentsController.cs`, `PaymentPeriodsController.cs`, `ChartOfAccountsController.cs`, `GeneralLedgerController.cs`, `AccountsPayableEntriesController.cs`, `AccountsPayableCategoriesController.cs`, `AccountsPayableReportsController.cs`

- [ ] **Step 1: Adicionar o atributo em cada controller**

Em cada arquivo, acrescente `using Prumo.Api.Attributes;` e ponha o atributo junto dos demais atributos de classe. Mapeamento exato, conferido contra `TenantBootstrapSeeder.DefaultResources`:

| Controller | Linha a adicionar |
|---|---|
| `EmployeesController` | `[TenantModule("HR.Employees")]` |
| `WorkLogsController` | `[TenantModule("HR.WorkLogs")]` |
| `PaymentsController` | `[TenantModule("HR.Payments")]` |
| `PaymentPeriodsController` | `[TenantModule("HR.PaymentPeriods")]` |
| `ChartOfAccountsController` | `[TenantModule("ChartOfAccounts.Management")]` |
| `GeneralLedgerController` | `[TenantModule("GeneralLedger.Management")]` |
| `AccountsPayableEntriesController` | `[TenantModule("AccountsPayable.Entries")]` |
| `AccountsPayableCategoriesController` | `[TenantModule("AccountsPayable.Entries")]` |
| `AccountsPayableReportsController` | `[TenantModule("AccountsPayable.Entries")]` |

Exemplo, em `EmployeesController.cs`:

```csharp
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/employees")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [TenantModule("HR.Employees")]
    public class EmployeesController : ControllerBase
```

**Não remova** nenhuma checagem `CanAccess`/`CanManage`/`GetRoleAsync` existente. Esta fase é aditiva de propósito: as proteções antigas ficam como redundância para que qualquer quebra tenha origem identificável.

Os três controllers de Contas a Pagar dividem `AccountsPayable.Entries` porque o catálogo não tem recursos separados para categorias e relatórios. É decisão registrada no spec, não omissão.

- [ ] **Step 2: Verificar compilação e suíte inteira**

Run: `dotnet build && dotnet test --nologo`
Expected: `0 Erro(s)` e `Com falha: 0`. A suíte não exercita HTTP, então os 63 testes existentes continuam verdes.

- [ ] **Step 3: Commit**

```bash
git add Prumo.Api/Controllers/
git commit -m "feat(security): declare the tenant module gate on all nine module controllers"
```

---

### Task 3: Testes de arquitetura

**Files:**
- Modify: `Prumo.Infrastructure/Data/Seeders/TenantBootstrapSeeder.cs`
- Test: `Prumo.Tests/Architecture/TenantCoverageTests.cs`

- [ ] **Step 1: Expor os códigos do catálogo**

`DefaultResources` é `private static readonly`. O teste precisa dos códigos. Em `TenantBootstrapSeeder.cs`, logo abaixo da declaração de `DefaultResources`, acrescente:

```csharp
        /// <summary>
        /// Códigos do catálogo padrão. Existe para o teste de arquitetura poder provar
        /// que todo [TenantModule] declara um recurso que realmente existe.
        /// </summary>
        public static IReadOnlyList<string> DefaultResourceCodes =>
            DefaultResources.Select(r => r.Code).ToList();
```

Se o arquivo ainda não tiver, acrescente `using System.Linq;` ao topo.

- [ ] **Step 2: Escrever os testes que falham**

Crie `Prumo.Tests/Architecture/TenantCoverageTests.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Prumo.Api.Attributes;
using Prumo.Infrastructure.Data.Seeders;
using System.Reflection;

namespace Prumo.Tests.Architecture
{
    public class TenantCoverageTests
    {
        /// <summary>
        /// Exceções declaradas de propósito, para que sejam visíveis e revisáveis.
        /// TenantsController gerencia associação — exigir associação provada nele seria
        /// circular. Os outros três não são roteados por tenant.
        /// </summary>
        private static readonly string[] Exempt =
        {
            "TenantsController",
            "AuthController",
            "PermissionsController",
            "ResourcesController"
        };

        private static IEnumerable<Type> Controllers() =>
            typeof(TenantModuleAttribute).Assembly
                .GetTypes()
                .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        [Fact]
        public void Every_tenant_routed_action_is_covered_by_TenantModule()
        {
            var offenders = new List<string>();

            foreach (var controller in Controllers())
            {
                if (Exempt.Contains(controller.Name)) continue;

                var controllerTemplate = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;
                var coveredAtClassLevel = controller.GetCustomAttribute<TenantModuleAttribute>() != null;

                var actions = controller
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

                foreach (var action in actions)
                {
                    var actionTemplate = action.GetCustomAttributes<HttpMethodAttribute>()
                        .Select(a => a.Template)
                        .FirstOrDefault(t => !string.IsNullOrEmpty(t)) ?? string.Empty;

                    var effective = $"{controllerTemplate}/{actionTemplate}";
                    if (!effective.Contains("{tenantId")) continue;

                    var covered = coveredAtClassLevel
                                  || action.GetCustomAttribute<TenantModuleAttribute>() != null;

                    if (!covered) offenders.Add($"{controller.Name}.{action.Name}");
                }
            }

            Assert.True(
                offenders.Count == 0,
                "Actions roteadas por tenant sem [TenantModule] e fora da lista de exceções: "
                + string.Join(", ", offenders));
        }

        [Fact]
        public void Every_declared_resource_code_exists_in_the_catalog()
        {
            var known = TenantBootstrapSeeder.DefaultResourceCodes;
            var unknown = new List<string>();

            foreach (var controller in Controllers())
            {
                foreach (var attribute in controller.GetCustomAttributes<TenantModuleAttribute>())
                {
                    if (!known.Contains(attribute.ResourceCode))
                        unknown.Add($"{controller.Name} → '{attribute.ResourceCode}'");
                }

                foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    foreach (var attribute in action.GetCustomAttributes<TenantModuleAttribute>())
                    {
                        if (!known.Contains(attribute.ResourceCode))
                            unknown.Add($"{controller.Name}.{action.Name} → '{attribute.ResourceCode}'");
                    }
                }
            }

            Assert.True(
                unknown.Count == 0,
                "resourceCode declarado que não existe no catálogo — negaria o endpoint para todos, "
                + "para sempre: " + string.Join(", ", unknown));
        }
    }
}
```

- [ ] **Step 3: Rodar e confirmar que passam**

Run: `dotnet test --filter "FullyQualifiedName~TenantCoverageTests"`
Expected: `Com falha: 0`. Se o primeiro teste falhar, a mensagem nomeia a action descoberta — é o comportamento pretendido, não um defeito do teste.

- [ ] **Step 4: Provar que o teste realmente pega o esquecimento**

Remova temporariamente `[TenantModule("HR.Employees")]` de `EmployeesController.cs`.

Run: `dotnet test --filter "FullyQualifiedName~Every_tenant_routed_action_is_covered_by_TenantModule"`
Expected: FALHA, listando `EmployeesController.List`, `EmployeesController.GetById` etc.

Recoloque o atributo e rode de novo. Expected: PASS.

Este passo não é cerimônia: um teste de arquitetura que nunca foi visto falhando pode estar vazio por engano — por exemplo, se `Controllers()` retornar lista vazia por assembly errado, ele passa sem provar nada.

- [ ] **Step 5: Commit**

```bash
git add Prumo.Tests/Architecture/TenantCoverageTests.cs Prumo.Infrastructure/Data/Seeders/TenantBootstrapSeeder.cs
git commit -m "test(security): fail the build when a tenant-routed action is left uncovered"
```

---

### Task 4: Filtro global fail-closed

**Files:**
- Modify: `Prumo.Infrastructure/Data/ApplicationDbContext.cs:75-81`
- Test: `Prumo.Tests/Infrastructure/TenantQueryFilterTests.cs`

- [ ] **Step 1: Escrever o teste que falha**

Crie `Prumo.Tests/Infrastructure/TenantQueryFilterTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Infrastructure
{
    public class TenantQueryFilterTests
    {
        private static ApplicationDbContext NewDb(ITenantContext ctx, string dbName) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName).Options, ctx);

        [Fact]
        public async Task Without_a_resolved_tenant_scoped_entities_return_nothing()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();

            var seeding = new TenantContext();
            seeding.SetTenant(tenantA);
            await using (var db = NewDb(seeding, dbName))
            {
                db.Resources.Add(new Resource { TenantId = tenantA, Code = "a", Name = "A", IsActive = true });
                db.Resources.Add(new Resource { TenantId = tenantB, Code = "b", Name = "B", IsActive = true });
                await db.SaveChangesAsync();
            }

            // Contexto sem tenant: fail-closed devolve vazio, não tudo.
            await using var noTenant = NewDb(new TenantContext(), dbName);
            var visible = await noTenant.Resources.ToListAsync();

            Assert.Empty(visible);
        }

        [Fact]
        public async Task With_a_resolved_tenant_only_that_tenants_rows_are_visible()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();

            var seeding = new TenantContext();
            seeding.SetTenant(tenantA);
            await using (var db = NewDb(seeding, dbName))
            {
                db.Resources.Add(new Resource { TenantId = tenantA, Code = "a", Name = "A", IsActive = true });
                db.Resources.Add(new Resource { TenantId = tenantB, Code = "b", Name = "B", IsActive = true });
                await db.SaveChangesAsync();
            }

            var scoped = new TenantContext();
            scoped.SetTenant(tenantB);
            await using var db2 = NewDb(scoped, dbName);
            var visible = await db2.Resources.ToListAsync();

            Assert.Single(visible);
            Assert.Equal("b", visible[0].Code);
        }
    }
}
```

- [ ] **Step 2: Rodar e confirmar que o primeiro falha**

Run: `dotnet test --filter "FullyQualifiedName~TenantQueryFilterTests"`
Expected: `Without_a_resolved_tenant_scoped_entities_return_nothing` FALHA — hoje o filtro é fail-open e devolve as duas linhas. O segundo teste já passa.

- [ ] **Step 3: Inverter o filtro**

Em `Prumo.Infrastructure/Data/ApplicationDbContext.cs`, no método `SetTenantQueryFilter<TEntity>`, troque:

```csharp
            modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
                _tenantContext == null
                || !_tenantContext.HasTenant
                || e.TenantId == _tenantContext.TenantId);
```

por:

```csharp
            // Fail-closed: sem tenant resolvido não volta linha nenhuma. O construtor sem
            // ITenantContext (usado pelo ApplicationDbContextFactory em design-time) deixa
            // _tenantContext nulo e portanto filtra tudo — inofensivo, porque design-time só
            // roda migration e migration não faz query. Quem precisa ler cross-tenant de
            // propósito usa IgnoreQueryFilters() explicitamente.
            modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
                _tenantContext != null
                && _tenantContext.HasTenant
                && e.TenantId == _tenantContext.TenantId);
```

- [ ] **Step 4: Rodar a suíte inteira**

Run: `dotnet test --nologo`
Expected: `Com falha: 0`. Se algum teste existente quebrar, ele depende do comportamento fail-open — investigue antes de ajustar o teste, porque pode ser um vazamento real que só agora ficou visível.

- [ ] **Step 5: Commit**

```bash
git add Prumo.Infrastructure/Data/ApplicationDbContext.cs Prumo.Tests/Infrastructure/TenantQueryFilterTests.cs
git commit -m "fix(security): make the global tenant query filter fail closed"
```

---

### Task 5: Item 12 — factory lendo configuração e arquivo morto apagado

**Files:**
- Modify: `Prumo.Infrastructure/Data/ApplicationDbContextFactory.cs`
- Delete: `Prumo.Api/appsettings.ConnectionStrings.json`
- Modify: `README.md`, `CLAUDE.md`

- [ ] **Step 1: Fazer o factory ler a configuração**

Substitua o conteúdo de `Prumo.Infrastructure/Data/ApplicationDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Prumo.Infrastructure.Data
{
    /// <summary>
    /// Usado só pelo `dotnet ef` em design-time. Lê a mesma configuração da aplicação
    /// para que migration e runtime nunca apontem para bases diferentes — a divergência
    /// que o item 12 do backlog descreve.
    /// </summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Prumo.Api");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.GetFullPath(basePath))
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection não encontrada. O design-time factory lê a "
                    + "configuração de Prumo.Api; rode o comando a partir da raiz da solution.");

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
```

Se `Microsoft.Extensions.Configuration.Json` não estiver referenciado em `Prumo.Infrastructure.csproj`, acrescente:

```xml
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.3" />
```

- [ ] **Step 2: Provar que o `dotnet ef` continua funcionando**

Run: `dotnet ef migrations list -p Prumo.Infrastructure -s Prumo.Api`
Expected: lista as migrations existentes sem erro. Isso prova que o factory acha a configuração.

- [ ] **Step 3: Apagar o arquivo morto e corrigir a documentação**

```bash
git rm Prumo.Api/appsettings.ConnectionStrings.json
```

Nenhum `AddJsonFile` o carrega e ele nem é copiado para o output — é morto duas vezes. Em `README.md`, na seção *Configuration*, troque a frase que diz que as connection strings ficam nesse arquivo por:

```markdown
Base config is in `appsettings.json`, including `ConnectionStrings:DefaultConnection`. Environment overlays: `appsettings.Development.json`, `appsettings.Production.json`, `appsettings.Demo.json`. In Production the connection string must come from the environment (`ConnectionStrings__DefaultConnection`) — the file ships a placeholder on purpose, so a missing variable fails at startup instead of silently falling back.
```

Em `CLAUDE.md`, na seção *Configuration files*, remova a menção a `appsettings.ConnectionStrings.json`.

- [ ] **Step 4: Rodar a suíte e commitar**

Run: `dotnet test --nologo`
Expected: `Com falha: 0`

```bash
git add Prumo.Infrastructure/Data/ApplicationDbContextFactory.cs README.md CLAUDE.md Prumo.Infrastructure/Prumo.Infrastructure.csproj
git commit -m "fix(config): make the design-time factory read configuration, drop the dead file"
```

---

### Task 6: Semeadura — apagar o re-seed de startup e criar o backfill

**Files:**
- Modify: `Prumo.Infrastructure/Data/DbInitializer.cs`
- Create: uma migration de backfill

- [ ] **Step 1: Escrever o teste de regressão que falha**

Acrescente a `Prumo.Tests/Infrastructure/TenantQueryFilterTests.cs` — ou crie `Prumo.Tests/Infrastructure/SeederIdempotenceTests.cs` com os mesmos `using` da Task 4 mais `using Prumo.Infrastructure.Data.Seeders;`:

```csharp
        [Fact]
        public async Task Re_running_the_bootstrap_seeder_does_not_resurrect_a_revoked_grant()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantId = Guid.NewGuid();

            var ctx = new TenantContext();
            ctx.SetTenant(tenantId);

            await using var db = NewDb(ctx, dbName);
            await TenantBootstrapSeeder.SeedAsync(db, tenantId);

            var granted = await db.ResourcePermissions.IgnoreQueryFilters()
                .Where(rp => rp.TenantId == tenantId).ToListAsync();
            Assert.NotEmpty(granted);

            // Um admin revoga um grant.
            var revoked = granted[0];
            db.ResourcePermissions.Remove(revoked);
            await db.SaveChangesAsync();

            // Reaplicar o seeder é o que o startup faz hoje.
            await TenantBootstrapSeeder.SeedAsync(db, tenantId);

            var stillRevoked = !await db.ResourcePermissions.IgnoreQueryFilters()
                .AnyAsync(rp => rp.TenantId == tenantId
                             && rp.RoleId == revoked.RoleId
                             && rp.ResourceId == revoked.ResourceId);

            Assert.True(stillRevoked,
                "O seeder ressuscitou um grant revogado — revogação não sobrevive a um restart.");
        }
```

Run: `dotnet test --filter "Re_running_the_bootstrap_seeder"`
Expected: FALHA. É o achado (a) do item 14 do backlog, reproduzido.

- [ ] **Step 2: Apagar o re-seed de startup**

Em `Prumo.Infrastructure/Data/DbInitializer.cs`:

1. Remova a chamada `await EnsureTenantBootstrapAsync(context, logger);` (por volta da linha 114).
2. Remova o método `EnsureTenantBootstrapAsync` inteiro (por volta das linhas 298-313).

O bootstrap continua rodando onde deve: em `TenantService.CreateAsync`, uma vez por tenant, no nascimento dele.

- [ ] **Step 3: Ajustar o teste ao novo contrato**

O teste do Step 1 provava um bug no seeder. Com o re-seed apagado, o seeder em si continua idempotente-mas-ressuscitador — ele só não é mais chamado repetidamente. Troque a asserção final por uma que descreva o contrato real:

```csharp
            // Contrato: o seeder é chamado UMA vez, na criação do tenant. Reaplicá-lo
            // ressuscitaria grants revogados, e é por isso que EnsureTenantBootstrapAsync
            // foi removido do startup. Este teste existe para que ninguém o traga de volta.
            Assert.False(stillRevoked,
                "O seeder continua ressuscitando grants ao ser reaplicado — por isso ele NUNCA "
                + "pode voltar a rodar no startup. Se este teste falhar porque o seeder passou a "
                + "ser seguro para reaplicação, ótimo: ajuste a asserção e registre a mudança.");
```

Run: `dotnet test --filter "Re_running_the_bootstrap_seeder"`
Expected: PASS.

- [ ] **Step 4: Criar a migration de backfill do catálogo**

Run: `dotnet ef migrations add BackfillTenantResources -p Prumo.Infrastructure -s Prumo.Api`

No arquivo gerado, no método `Up`, insira — repetindo o bloco para **cada** código de `DefaultResourceCodes` que os tenants antigos possam não ter:

```csharp
            migrationBuilder.Sql(@"
                INSERT INTO ""Resources"" (""Id"", ""TenantId"", ""Code"", ""Name"", ""Description"", ""Module"", ""IsActive"", ""DisplayOrder"", ""CreatedAt"")
                SELECT gen_random_uuid(), t.""Id"", 'HR.Employees', 'Funcionários', 'Cadastro de funcionários', 'RH', TRUE, 0, NOW() AT TIME ZONE 'utc'
                FROM ""Tenants"" t
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""Resources"" r
                    WHERE r.""TenantId"" = t.""Id"" AND r.""Code"" = 'HR.Employees'
                );
            ");
```

Antes de escrever os blocos, confira os nomes reais de coluna:

Run: `dotnet ef migrations script -p Prumo.Infrastructure -s Prumo.Api | Select-String -Pattern 'CREATE TABLE .Resources' -Context 0,20`

Ajuste colunas e valores de `Name`, `Description`, `Module` e `DisplayOrder` para bater com o que está em `TenantBootstrapSeeder.DefaultResources`. No `Down`, deixe vazio com um comentário: remover recursos apagaria em cascata os grants que os admins criaram depois, o que é pior que a migration ser irreversível.

- [ ] **Step 5: Verificar contra um Postgres real**

```bash
docker run -d --name prumo-pg-verify -e POSTGRES_PASSWORD=postgres -e POSTGRES_USER=postgres -p 5432:5432 postgres:16
dotnet ef database update -p Prumo.Infrastructure -s Prumo.Api
```

Expected: aplica sem erro. Confirme que os recursos apareceram para todos os tenants:

```bash
docker exec prumo-pg-verify psql -U postgres -d PrumoDb -c "SELECT t.\"Slug\", count(r.*) FROM \"Tenants\" t LEFT JOIN \"Resources\" r ON r.\"TenantId\" = t.\"Id\" GROUP BY t.\"Slug\";"
```

Expected: mesma contagem para todos os tenants.

> Nota: a base se chama `SaaSBasePlatformDb` até o item 12 unificar os nomes; use o nome que estiver em `appsettings.json`.

- [ ] **Step 6: Commit**

```bash
git add Prumo.Infrastructure/Data/DbInitializer.cs Prumo.Infrastructure/Migrations/ Prumo.Tests/Infrastructure/
git commit -m "fix(security): stop the startup re-seed from resurrecting revoked grants"
```

---

### Task 7: Credencial do admin por ambiente

**Files:**
- Modify: `Prumo.Infrastructure/Data/DbInitializer.cs:166-194`

- [ ] **Step 1: Trocar a senha chumbada por configuração**

`DbInitializer.InitializeAsync` precisa de acesso a `IConfiguration` e ao ambiente. Se a assinatura ainda não os recebe, acrescente os parâmetros e atualize a chamada em `Program.cs`/`DatabaseConfiguration`.

Substitua o bloco de criação do admin (por volta das linhas 166-194) por:

```csharp
                // Criar usuário admin
                var seedPassword = configuration["Seed:AdminPassword"];
                var isDevelopment = environment.IsDevelopment() || environment.IsEnvironment("Demo");

                if (string.IsNullOrWhiteSpace(seedPassword))
                {
                    if (isDevelopment)
                    {
                        logger.LogWarning(
                            "Admin não criado: defina Seed:AdminPassword (user secrets) para semear o admin local.");
                        logger.LogInformation("=== Inicialização concluída ===");
                        return;
                    }

                    throw new InvalidOperationException(
                        "Seed:AdminPassword não configurada. Em Production o admin master nunca é criado "
                        + "com senha padrão — forneça Seed__AdminPassword por variável de ambiente ou "
                        + "remova o seeding de admin deste ambiente.");
                }

                logger.LogInformation("Criando usuário administrador...");
                adminUser = new ApplicationUser
                {
                    UserName = "admin@SBP.com",
                    Email = "admin@SBP.com",
                    EmailConfirmed = true,
                    FullName = "Administrador do Sistema",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(adminUser, seedPassword);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrador");
                    await EnsureDefaultTenantAsync(context, adminUser, logger);
                    // A senha NUNCA vai para o log: o Serilog tem sink para tabela, e isso
                    // depositaria a credencial do admin master no armazenamento de log.
                    logger.LogInformation("✓ Usuário admin criado: {Email}", adminUser.Email);
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger.LogError("✗ Erro ao criar usuário admin: {Errors}", errors);
                }
```

- [ ] **Step 2: Registrar a senha de desenvolvimento em user secrets**

```bash
dotnet user-secrets set "Seed:AdminPassword" "Admin@123" --project Prumo.Api
```

- [ ] **Step 3: Provar que a senha sumiu do repositório**

Run: `git grep -n "Admin@123" -- . ":(exclude)docs"`
Expected: **nenhuma saída.**

- [ ] **Step 4: Rodar a suíte e commitar**

Run: `dotnet test --nologo`
Expected: `Com falha: 0`

```bash
git add Prumo.Infrastructure/Data/DbInitializer.cs Prumo.Api/
git commit -m "fix(security): stop hardcoding and logging the seeded admin password"
```

---

### Task 8: Criação de tenant restrita e endpoint de suporte

**Files:**
- Modify: `Prumo.Api/Controllers/TenantsController.cs`

- [ ] **Step 1: Restringir a criação de tenant**

Em `TenantsController.cs`, na action `Create` (por volta da linha 31), acrescente o atributo de role:

```csharp
        [HttpPost]
        [Authorize(Roles = "Administrador")]
        [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<TenantDto>> Create([FromBody] CreateTenantRequestDto request, CancellationToken ct)
```

Hoje qualquer usuário autenticado cria tenant. Como tenant é vendido, só o master admin cria.

- [ ] **Step 2: Adicionar o endpoint de suporte auditado**

No mesmo controller:

```csharp
        /// <summary>
        /// Insere o master admin como membro de um tenant que ele não criou, para suporte.
        /// Não existe bypass da checagem de associação: um bypass reintroduziria o vazamento
        /// cross-tenant que o trabalho de RBAC removeu, e seria um caminho que o teste de
        /// arquitetura não consegue ver. Aqui o acesso vira uma linha no banco, auditada.
        /// </summary>
        [HttpPost("{tenantId:guid}/support-access")]
        [Authorize(Roles = "Administrador")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GrantSupportAccess(Guid tenantId, CancellationToken ct)
        {
            var granted = await _tenantService.GrantSupportAccessAsync(tenantId, CurrentUserId, ct);
            return granted ? NoContent() : NotFound();
        }
```

- [ ] **Step 3: Implementar o método no serviço**

Em `Prumo.Application/Services/ITenantService.cs`, acrescente à interface:

```csharp
        Task<bool> GrantSupportAccessAsync(Guid tenantId, Guid masterAdminUserId, CancellationToken ct = default);
```

Em `Prumo.Application/Services/TenantService.cs`:

```csharp
        public async Task<bool> GrantSupportAccessAsync(
            Guid tenantId, Guid masterAdminUserId, CancellationToken ct = default)
        {
            var tenantExists = await _db.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Id == tenantId, ct);

            if (!tenantExists) return false;

            var alreadyMember = await _db.TenantUsers
                .IgnoreQueryFilters()
                .AnyAsync(tu => tu.TenantId == tenantId && tu.UserId == masterAdminUserId, ct);

            if (alreadyMember) return true;

            _db.TenantUsers.Add(new TenantUser
            {
                TenantId = tenantId,
                UserId = masterAdminUserId,
                Role = TenantRole.Admin
            });

            _db.PermissionAuditLogs.Add(new PermissionAuditLog
            {
                TenantId = tenantId,
                UserId = masterAdminUserId,
                Action = "SupportAccessGranted",
                Details = $"Master admin {masterAdminUserId} inseriu-se como Admin do tenant {tenantId} para suporte.",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);
            return true;
        }
```

Confira os nomes reais das propriedades de `PermissionAuditLog` em `Prumo.Domain/Entities/PermissionAuditLog.cs` e ajuste — se algum campo não existir, use os equivalentes em vez de criar campos novos.

- [ ] **Step 4: Escrever o teste**

Crie `Prumo.Tests/Services/TenantSupportAccessTests.cs`, usando o mesmo padrão `NewDb` de `TenantRoleServiceTests`:

```csharp
        [Fact]
        public async Task Support_access_is_idempotent_and_makes_the_master_admin_a_member()
        {
            var tenantId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            await using var db = NewDb();
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t", OwnerUserId = Guid.NewGuid() });
            await db.SaveChangesAsync();

            var service = new TenantService(db, MockUserManager());

            Assert.True(await service.GrantSupportAccessAsync(tenantId, adminId));
            Assert.True(await service.GrantSupportAccessAsync(tenantId, adminId));

            var memberships = await db.TenantUsers.IgnoreQueryFilters()
                .Where(tu => tu.TenantId == tenantId && tu.UserId == adminId).ToListAsync();

            Assert.Single(memberships);
            Assert.Equal(TenantRole.Admin, memberships[0].Role);
        }

        [Fact]
        public async Task Support_access_to_an_unknown_tenant_returns_false()
        {
            await using var db = NewDb();
            var service = new TenantService(db, MockUserManager());

            Assert.False(await service.GrantSupportAccessAsync(Guid.NewGuid(), Guid.NewGuid()));
        }
```

O construtor é `TenantService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)`. O `UserManager` não é usado por `GrantSupportAccessAsync`, então basta um substituto. Acrescente ao mesmo arquivo de teste:

```csharp
        private static UserManager<ApplicationUser> MockUserManager() =>
            Substitute.For<UserManager<ApplicationUser>>(
                Substitute.For<IUserStore<ApplicationUser>>(),
                null, null, null, null, null, null, null, null);
```

Requer `using Microsoft.AspNetCore.Identity;` e `using NSubstitute;`. É o mesmo padrão de `ResourcePermissionTenantTests.MockUserManager`.

- [ ] **Step 5: Rodar tudo e commitar**

Run: `dotnet build && dotnet test --nologo`
Expected: `0 Erro(s)`, `Com falha: 0`

```bash
git add Prumo.Api/Controllers/TenantsController.cs Prumo.Application/Services/ Prumo.Tests/Services/TenantSupportAccessTests.cs
git commit -m "feat(security): restrict tenant creation and add audited support access"
```

---

## Verificação final da fase 1

- [ ] **Suíte completa**

Run: `dotnet build && dotnet test --nologo`
Expected: `0 Erro(s)`, `Com falha: 0`, e o total **acima** de 63 — os testes novos precisam aparecer.

- [ ] **Prova de que o teste de arquitetura morde**

Remova qualquer `[TenantModule]` de um controller, rode `dotnet test`, veja falhar nomeando a action, e recoloque.

- [ ] **Verificação manual contra a API viva**

Reinicie a API (o processo em `localhost:5201` roda binário antigo) e faça logout/login — as chaves do `localStorage` mudaram para `prumo_*` no rename, então o navegador precisa limpar o estado velho. Depois:

1. Login, selecionar tenant A, `GET /api/tenants/{A}/employees` → 200
2. Mesmo token, `GET /api/tenants/{B}/employees` com B de outro tenant → 403
3. Token sem tenant selecionado em `GET /api/tenants/{A}/employees` → 403 (**mudança de comportamento deliberada**)
4. Revogar um grant, reiniciar a API, conferir que **continua revogado**

O item 4 é a prova do achado (a): antes desta fase, o grant voltava.

## Um desvio do spec, para decisão do Nickolas

A seção 3 do spec diz que **as roles globais do Identity e o catálogo de `Permission`** também deveriam virar migration. **Este plano não faz isso**, e o desvio é consciente:

- O problema que motivou mover a semeadura para migration eram os **grants** sendo reaplicados e a **senha** sendo recriada. Ambos estão resolvidos nas tasks 6 e 7.
- Roles e catálogo de permissão são estáticos e o `DbInitializer` já os cria só quando não existem. Reaplicá-los é inofensivo — não há estado do usuário para sobrescrever.
- Semear roles do Identity via SQL exige acertar `NormalizedName` e `ConcurrencyStamp` na mão, que é justamente onde esse tipo de migration costuma errar em silêncio.

Custo de deixar como está: o startup continua fazendo algumas leituras a mais. Se o Nickolas quiser o spec cumprido à risca, isso vira uma task adicional — é trabalho pequeno, mas com risco desproporcional ao ganho, e por isso não entrou sem ele decidir.

## O que esta fase deliberadamente NÃO faz

Nada é removido. Os ~87 `IgnoreQueryFilters`, as checagens `CanAccess`/`CanManage` por action e o `[RequireResourceAccess]` do `PermissionsController` continuam todos no lugar, como redundância — para que qualquer quebra tenha origem identificável. A remoção é a fase 2 e vira um plano separado, escrito só depois desta estar de pé e rodada de verdade.

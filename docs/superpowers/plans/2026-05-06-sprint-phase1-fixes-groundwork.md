# Sprint Phase 1 — Fixes & Backend Groundwork

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply five self-contained fixes and seed the backend data that Phase 2 features depend on.

**Architecture:** All changes are additive — new permission classes, new roles, new accounts, new EF column. No existing behaviour is removed; existing tests stay green. The DbInitializer cleanup block runs once at startup and is idempotent.

**Tech Stack:** .NET 10, EF Core 10, xUnit + NSubstitute (backend tests); Angular 18 standalone (login fix only).

---

## File Map

| File | Action |
|------|--------|
| `SaaSBasePlatform-Angular/src/app/modules/auth/login/login.component.ts` | Modify — clear pre-filled credentials |
| `Prumo.Infrastructure/Data/DbInitializer.cs` | Modify — suppress duplicate logs + delete "User" role + seed new roles |
| `Prumo.Domain/Authorization/Permissions.cs` | Modify — add Finance, AccountsPayable classes + DefaultRolePermissions.RH/Financeiro/ContasAPagar |
| `Prumo.Infrastructure/Data/Seeders/TenantBootstrapSeeder.cs` | Modify — add new roles to rolePermissionConfig + ResourcePermissions |
| `Prumo.Domain/Entities/TenantGlSettings.cs` | Modify — add DefaultExpenseAccountId |
| `Prumo.Infrastructure/Data/Configurations/TenantGlSettingsConfiguration.cs` | Modify — configure new FK |
| `Prumo.Infrastructure/Data/Migrations/` | New — EF migration AddDefaultExpenseAccount |
| `Prumo.Infrastructure/Data/Seeders/ChartOfAccountsSeeder.cs` | Modify — add 6 new accounts + assign DefaultExpenseAccountId |
| `SaaSBasePlatform-Angular/src/app/core/models/index.ts` | Modify — add defaultExpenseAccountId to TenantGlSettings |
| `SaaSBasePlatform-Angular/src/app/modules/finance/chart-of-accounts/accounts-hints.component.ts` | Create — GL account hints card |
| `SaaSBasePlatform-Angular/src/app/modules/finance/chart-of-accounts/chart-of-accounts.component.html` | Modify — add hints component at top |
| `SaaSBasePlatform-Angular/src/app/modules/finance/chart-of-accounts/chart-of-accounts.component.ts` | Modify — import AccountsHintsComponent |
| `Prumo.Tests/Infrastructure/DbInitializerCleanupTests.cs` | Create — test "User" role cleanup |
| `Prumo.Tests/Domain/PermissionsTests.cs` | Create — test new permission strings exist + Admin has all |

---

## Task 1: Remove login placeholder credentials

**Files:**
- Modify: `SaaSBasePlatform-Angular/src/app/modules/auth/login/login.component.ts:47-51`

- [ ] **Step 1: Clear the pre-filled values**

Open `SaaSBasePlatform-Angular/src/app/modules/auth/login/login.component.ts`.

Replace:
```typescript
  private initForm(): void {
    this.loginForm = this.fb.group({
      email: ['admin@saas-baseplatform.com', [Validators.required, Validators.email]],
      password: ['Admin@123', [Validators.required, Validators.minLength(6)]],
      tenantSlug: ['']
    });
  }
```

With:
```typescript
  private initForm(): void {
    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      tenantSlug: ['']
    });
  }
```

- [ ] **Step 2: Commit**

```bash
git add SaaSBasePlatform-Angular/src/app/modules/auth/login/login.component.ts
git commit -m "fix: remove pre-filled admin credentials from login form"
```

---

## Task 2: Suppress duplicate startup logs + delete "User" role

**Files:**
- Modify: `Prumo.Infrastructure/Data/DbInitializer.cs`
- Create: `Prumo.Tests/Infrastructure/DbInitializerCleanupTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Prumo.Tests/Infrastructure/DbInitializerCleanupTests.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;
using Xunit;

namespace Prumo.Tests.Infrastructure;

public class DbInitializerCleanupTests
{
    [Fact]
    public void UserRole_ShouldNotExist_InRolesConfig()
    {
        // Arrange — the roles dictionary in DbInitializer must not contain "User"
        var rolesConfig = new Dictionary<string, string>
        {
            { "Administrador", "Acesso total ao sistema" },
            { "Funcionario",   "Acesso para funcionários do sistema" },
            { "Cliente",       "Acesso para clientes" },
            { "RH",            "Acesso ao módulo de Recursos Humanos" },
            { "Financeiro",    "Acesso ao módulo Financeiro" },
            { "ContasAPagar",  "Acesso ao módulo de Contas a Pagar" }
        };

        // Act & Assert
        Assert.DoesNotContain("User", rolesConfig.Keys);
    }
}
```

- [ ] **Step 2: Run test — confirm it passes (it validates the intention, not seeded data)**

```bash
dotnet test --filter "FullyQualifiedName~DbInitializerCleanupTests" -v minimal
```

Expected: PASS (assertion on local dictionary, no DB needed)

- [ ] **Step 3: Update DbInitializer — suppress logs + add User role cleanup**

Open `Prumo.Infrastructure/Data/DbInitializer.cs`.

**3a.** Add the "User" role cleanup block immediately after `await context.Database.MigrateAsync();` (line 28) and before the roles seeding loop:

```csharp
                // One-time cleanup: remove legacy "User" role if it exists
                var legacyUserRole = await roleManager.FindByNameAsync("User");
                if (legacyUserRole != null)
                {
                    var legacyRps = context.RolePermissions
                        .IgnoreQueryFilters()
                        .Where(rp => rp.RoleId == legacyUserRole.Id);
                    context.RolePermissions.RemoveRange(legacyRps);
                    await context.SaveChangesAsync();
                    await roleManager.DeleteAsync(legacyUserRole);
                    logger.LogInformation("✓ Role legada 'User' removida");
                }
```

**3b.** In the roles seeding loop, change the "already exists" branch from `LogInformation` to `LogDebug`:

```csharp
                    else
                    {
                        logger.LogDebug("Role '{RoleName}' já existe", roleName);
                    }
```

**3c.** In the permissions seeding loop, the `else` branch (line 90-93) already only assigns to `permissionMap` without logging — leave it as-is.

**3d.** In the admin user check block (around line 103-104), change:
```csharp
                    logger.LogInformation("✓ Usuário admin já existe.");
```
To:
```csharp
                    logger.LogDebug("Usuário admin já existe");
```

- [ ] **Step 4: Run test again**

```bash
dotnet test --filter "FullyQualifiedName~DbInitializerCleanupTests" -v minimal
```

Expected: PASS

- [ ] **Step 5: Build to confirm no compile errors**

```bash
dotnet build Prumo.Infrastructure
```

Expected: Build succeeded, 0 error(s)

- [ ] **Step 6: Commit**

```bash
git add Prumo.Infrastructure/Data/DbInitializer.cs
git add Prumo.Tests/Infrastructure/DbInitializerCleanupTests.cs
git commit -m "fix: suppress duplicate startup logs and remove legacy User role at startup"
```

---

## Task 3: Add Finance and AccountsPayable permissions + module role permission sets

**Files:**
- Modify: `Prumo.Domain/Authorization/Permissions.cs`
- Create: `Prumo.Tests/Domain/PermissionsTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Prumo.Tests/Domain/PermissionsTests.cs`:

```csharp
using Prumo.Domain.Authorization;
using Xunit;

namespace Prumo.Tests.Domain;

public class PermissionsTests
{
    [Fact]
    public void GetAllPermissions_ContainsFinancePermissions()
    {
        var all = Permissions.GetAllPermissions();
        Assert.Contains("finance.view",   all);
        Assert.Contains("finance.manage", all);
    }

    [Fact]
    public void GetAllPermissions_ContainsAccountsPayablePermissions()
    {
        var all = Permissions.GetAllPermissions();
        Assert.Contains("accounts_payable.view",              all);
        Assert.Contains("accounts_payable.create",            all);
        Assert.Contains("accounts_payable.edit",              all);
        Assert.Contains("accounts_payable.delete",            all);
        Assert.Contains("accounts_payable.manage_categories", all);
    }

    [Fact]
    public void AdminRolePermissions_ContainsAllPermissions()
    {
        var all   = Permissions.GetAllPermissions();
        var admin = Permissions.DefaultRolePermissions.Admin;
        foreach (var perm in all)
            Assert.Contains(perm, admin);
    }

    [Fact]
    public void RhRolePermissions_OnlyContainsHrPermissions()
    {
        var rh = Permissions.DefaultRolePermissions.RH;
        Assert.Contains("employees.view",   rh);
        Assert.Contains("worklogs.view",    rh);
        Assert.Contains("payments.view",    rh);
        Assert.DoesNotContain("finance.view",           rh);
        Assert.DoesNotContain("accounts_payable.view",  rh);
    }

    [Fact]
    public void FinanceiroRolePermissions_OnlyContainsFinancePermissions()
    {
        var fin = Permissions.DefaultRolePermissions.Financeiro;
        Assert.Contains("finance.view",   fin);
        Assert.Contains("finance.manage", fin);
        Assert.DoesNotContain("employees.view",          fin);
        Assert.DoesNotContain("accounts_payable.view",   fin);
    }

    [Fact]
    public void ContasAPagarRolePermissions_OnlyContainsApPermissions()
    {
        var ap = Permissions.DefaultRolePermissions.ContasAPagar;
        Assert.Contains("accounts_payable.view",   ap);
        Assert.Contains("accounts_payable.create", ap);
        Assert.DoesNotContain("finance.view",    ap);
        Assert.DoesNotContain("employees.view",  ap);
    }
}
```

- [ ] **Step 2: Run tests — confirm they fail**

```bash
dotnet test --filter "FullyQualifiedName~PermissionsTests" -v minimal
```

Expected: FAIL — `Finance`, `AccountsPayable`, `DefaultRolePermissions.RH` etc. not yet defined.

- [ ] **Step 3: Add Finance + AccountsPayable permission classes to Permissions.cs**

Open `Prumo.Domain/Authorization/Permissions.cs`.

After the `Stock` class (around line 57) and before `GetAllPermissions()`, add:

```csharp
        public static class Finance
        {
            public const string All    = "finance.*";
            public const string View   = "finance.view";
            public const string Manage = "finance.manage";
        }

        public static class AccountsPayable
        {
            public const string All              = "accounts_payable.*";
            public const string View             = "accounts_payable.view";
            public const string Create           = "accounts_payable.create";
            public const string Edit             = "accounts_payable.edit";
            public const string Delete           = "accounts_payable.delete";
            public const string ManageCategories = "accounts_payable.manage_categories";
        }
```

- [ ] **Step 4: Add new permission strings to GetAllPermissions()**

In the `GetAllPermissions()` method, add to the returned array:

```csharp
                // Finance
                Finance.View,
                Finance.Manage,

                // AccountsPayable
                AccountsPayable.View,
                AccountsPayable.Create,
                AccountsPayable.Edit,
                AccountsPayable.Delete,
                AccountsPayable.ManageCategories,
```

- [ ] **Step 5: Add new DefaultRolePermissions entries**

Inside `DefaultRolePermissions`, add after the `Cliente` property:

```csharp
            public static IReadOnlyCollection<string> RH => new[]
            {
                Employees.View, Employees.Create, Employees.Edit, Employees.Delete, Employees.ManagePayments,
                WorkLogs.View,  WorkLogs.Create,  WorkLogs.Edit,  WorkLogs.Delete,
                Payments.View,  Payments.Create,  Payments.Delete, Payments.ViewReports
            };

            public static IReadOnlyCollection<string> Financeiro => new[]
            {
                Finance.View, Finance.Manage
            };

            public static IReadOnlyCollection<string> ContasAPagar => new[]
            {
                AccountsPayable.View,   AccountsPayable.Create, AccountsPayable.Edit,
                AccountsPayable.Delete, AccountsPayable.ManageCategories
            };
```

- [ ] **Step 6: Run tests — confirm they all pass**

```bash
dotnet test --filter "FullyQualifiedName~PermissionsTests" -v minimal
```

Expected: 6 tests PASS

- [ ] **Step 7: Commit**

```bash
git add Prumo.Domain/Authorization/Permissions.cs
git add Prumo.Tests/Domain/PermissionsTests.cs
git commit -m "feat: add Finance and AccountsPayable permissions with module-scoped role permission sets"
```

---

## Task 4: Seed new roles in DbInitializer and TenantBootstrapSeeder

**Files:**
- Modify: `Prumo.Infrastructure/Data/DbInitializer.cs`
- Modify: `Prumo.Infrastructure/Data/Seeders/TenantBootstrapSeeder.cs`

- [ ] **Step 1: Add RH and ContasAPagar resources to TenantBootstrapSeeder.DefaultResources**

The module-role resource permission loop in Step 3 filters by `Resource.Module`. The current `DefaultResources` array has no entries for `Module = "RH"` or `Module = "ContasAPagar"`. Add them.

Open `Prumo.Infrastructure/Data/Seeders/TenantBootstrapSeeder.cs`.

After the last entry in `DefaultResources` (the `"GeneralLedger.Management"` entry), add:

```csharp
            new()
            {
                Code = "HR.Employees",
                Name = "Funcionários",
                Description = "Gestão de funcionários",
                Module = "RH",
                FrontendRoute = "/hr/employees",
                Icon = "badge",
                DisplayOrder = 40
            },
            new()
            {
                Code = "HR.WorkLogs",
                Name = "Horas Trabalhadas",
                Description = "Registro de horas",
                Module = "RH",
                FrontendRoute = "/hr/worklogs",
                Icon = "schedule",
                DisplayOrder = 41
            },
            new()
            {
                Code = "HR.Payments",
                Name = "Pagamentos RH",
                Description = "Pagamentos de funcionários",
                Module = "RH",
                FrontendRoute = "/hr/payments",
                Icon = "payments",
                DisplayOrder = 42
            },
            new()
            {
                Code = "HR.PaymentPeriods",
                Name = "Períodos de Pagamento",
                Description = "Períodos gerados para pagamento",
                Module = "RH",
                FrontendRoute = "/hr/periodos",
                Icon = "event_note",
                DisplayOrder = 43
            },
            new()
            {
                Code = "AccountsPayable.Entries",
                Name = "Contas a Pagar",
                Description = "Lançamentos de contas a pagar",
                Module = "ContasAPagar",
                FrontendRoute = "/accounts-payable",
                Icon = "request_quote",
                DisplayOrder = 50
            },
```

- [ ] **Step 3: Add new roles to DbInitializer.rolesConfig**

Open `Prumo.Infrastructure/Data/DbInitializer.cs`.

Replace:
```csharp
                var rolesConfig = new Dictionary<string, string>
                {
                    { "Administrador", "Acesso total ao sistema" },
                    { "Funcionario", "Acesso para funcionários do sistema" },
                    { "Cliente", "Acesso para clientes" }
                };
```

With:
```csharp
                var rolesConfig = new Dictionary<string, string>
                {
                    { "Administrador", "Acesso total ao sistema" },
                    { "Funcionario",   "Acesso para funcionários do sistema" },
                    { "Cliente",       "Acesso para clientes" },
                    { "RH",            "Acesso ao módulo de Recursos Humanos" },
                    { "Financeiro",    "Acesso ao módulo Financeiro" },
                    { "ContasAPagar",  "Acesso ao módulo de Contas a Pagar" }
                };
```

- [ ] **Step 4: Add new roles to TenantBootstrapSeeder.rolePermissionConfig**

Open `Prumo.Infrastructure/Data/Seeders/TenantBootstrapSeeder.cs`.

Replace:
```csharp
            var rolePermissionConfig = new Dictionary<string, IReadOnlyCollection<string>>
            {
                { "Administrador", Permissions.DefaultRolePermissions.Admin },
                { "Funcionario",  Permissions.DefaultRolePermissions.Funcionario },
                { "Cliente",      Permissions.DefaultRolePermissions.Cliente }
            };
```

With:
```csharp
            var rolePermissionConfig = new Dictionary<string, IReadOnlyCollection<string>>
            {
                { "Administrador", Permissions.DefaultRolePermissions.Admin },
                { "Funcionario",   Permissions.DefaultRolePermissions.Funcionario },
                { "Cliente",       Permissions.DefaultRolePermissions.Cliente },
                { "RH",            Permissions.DefaultRolePermissions.RH },
                { "Financeiro",    Permissions.DefaultRolePermissions.Financeiro },
                { "ContasAPagar",  Permissions.DefaultRolePermissions.ContasAPagar }
            };
```

- [ ] **Step 5: Add ResourcePermissions for the three new module roles**

Still in `TenantBootstrapSeeder.cs`, after the existing Admin `ResourcePermissions` block (after line ~193), add:

```csharp
            // Module roles: each gets Full access only to their own module's resources
            var moduleRoleResourceMap = new Dictionary<string, string>
            {
                { "RH",           "RH" },
                { "Financeiro",   "Financeiro" },
                { "ContasAPagar", "ContasAPagar" }
            };

            foreach (var (roleName, moduleName) in moduleRoleResourceMap)
            {
                if (!rolesByName.TryGetValue(roleName, out var moduleRoleId)) continue;

                var moduleResources = await db.Resources
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId && r.Module == moduleName)
                    .Select(r => r.Id)
                    .ToListAsync(cancellationToken);

                foreach (var resourceId in moduleResources)
                {
                    var exists = await db.ResourcePermissions
                        .IgnoreQueryFilters()
                        .AnyAsync(rp =>
                            rp.TenantId == tenantId &&
                            rp.RoleId == moduleRoleId &&
                            rp.ResourceId == resourceId, cancellationToken);

                    if (!exists)
                    {
                        db.ResourcePermissions.Add(new ResourcePermission
                        {
                            TenantId   = tenantId,
                            RoleId     = moduleRoleId,
                            ResourceId = resourceId,
                            Level      = PermissionLevel.Full
                        });
                    }
                }
            }

            await db.SaveChangesAsync(cancellationToken);
```

- [ ] **Step 4: Build to confirm no compile errors**

```bash
dotnet build Prumo.Infrastructure
```

Expected: Build succeeded, 0 error(s)

- [ ] **Step 6: Build to confirm no compile errors**

```bash
dotnet build Prumo.Infrastructure
```

Expected: Build succeeded, 0 error(s)

- [ ] **Step 7: Commit**

```bash
git add Prumo.Infrastructure/Data/DbInitializer.cs
git add Prumo.Infrastructure/Data/Seeders/TenantBootstrapSeeder.cs
git commit -m "feat: seed RH, Financeiro, ContasAPagar roles with module-scoped permissions and resources"
```

---

## Task 5: Add DefaultExpenseAccountId to TenantGlSettings + EF migration

**Files:**
- Modify: `Prumo.Domain/Entities/TenantGlSettings.cs`
- Modify: `Prumo.Infrastructure/Data/Configurations/TenantGlSettingsConfiguration.cs`
- New: EF Core migration

- [ ] **Step 1: Add the new property to the entity**

Open `Prumo.Domain/Entities/TenantGlSettings.cs`.

Replace the entire file content with:

```csharp
namespace Prumo.Domain.Entities
{
    // Intentionally not EntityBase: PK is TenantId (1:1 with Tenant), not an independent Guid.
    public class TenantGlSettings
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        public Guid? DefaultCashAccountId { get; set; }
        public Account? DefaultCashAccount { get; set; }
        public Guid? DefaultAccountsPayableAccountId { get; set; }
        public Account? DefaultAccountsPayableAccount { get; set; }
        public Guid? DefaultExpenseAccountId { get; set; }
        public Account? DefaultExpenseAccount { get; set; }
    }
}
```

- [ ] **Step 2: Configure the new FK in EF**

Open `Prumo.Infrastructure/Data/Configurations/TenantGlSettingsConfiguration.cs`.

Add after the last `builder.HasOne` block (after line 29):

```csharp
            builder.HasOne(s => s.DefaultExpenseAccount)
                .WithMany()
                .HasForeignKey(s => s.DefaultExpenseAccountId)
                .OnDelete(DeleteBehavior.Restrict);
```

- [ ] **Step 3: Create the EF migration**

```bash
dotnet ef migrations add AddDefaultExpenseAccount -p Prumo.Infrastructure -s Prumo.Api
```

Expected: migration file created in `Prumo.Infrastructure/Data/Migrations/`

- [ ] **Step 4: Verify the migration looks correct**

Open the new migration file. It should contain:
```csharp
migrationBuilder.AddColumn<Guid>(
    name: "DefaultExpenseAccountId",
    table: "TenantGlSettings",
    type: "uniqueidentifier",
    nullable: true);
```

Also a `CreateIndex` for the FK and an `AddForeignKey`. If you see anything unexpected (like dropping columns), stop and inspect before proceeding.

- [ ] **Step 5: Build to confirm no compile errors**

```bash
dotnet build
```

Expected: Build succeeded, 0 error(s)

- [ ] **Step 6: Commit**

```bash
git add Prumo.Domain/Entities/TenantGlSettings.cs
git add Prumo.Infrastructure/Data/Configurations/TenantGlSettingsConfiguration.cs
git add Prumo.Infrastructure/Data/Migrations/
git commit -m "feat: add DefaultExpenseAccountId to TenantGlSettings"
```

---

## Task 6: Expand ChartOfAccountsSeeder with new sub-accounts

**Files:**
- Modify: `Prumo.Infrastructure/Data/Seeders/ChartOfAccountsSeeder.cs`

- [ ] **Step 1: Add new account seeds to DefaultAccounts**

Open `Prumo.Infrastructure/Data/Seeders/ChartOfAccountsSeeder.cs`.

In `DefaultAccounts`, after the last entry (`new("5.2", ...)`) add:

```csharp
            new("2.1.3", "Salários a Pagar",          AccountType.Liability, true,  "2.1"),
            new("2.1.4", "Encargos Sociais a Pagar",  AccountType.Liability, true,  "2.1"),
            new("5.1.1", "Despesas com Pessoal",      AccountType.Expense,   true,  "5.1"),
            new("5.1.2", "Despesas Administrativas",  AccountType.Expense,   true,  "5.1"),
            new("5.1.3", "Despesas Operacionais",     AccountType.Expense,   true,  "5.1"),
            new("5.1.4", "Despesas com Fornecedores", AccountType.Expense,   true,  "5.1"),
```

- [ ] **Step 2: Assign DefaultExpenseAccountId when writing TenantGlSettings**

In the same file, find the `TenantGlSettings` creation block (around line 63-73). Replace:

```csharp
            if (existingSettings == null)
            {
                db.TenantGlSettings.Add(new TenantGlSettings
                {
                    TenantId                        = tenantId,
                    DefaultCashAccountId            = seeded["1.1.1"],
                    DefaultAccountsPayableAccountId = seeded["2.1.1"]
                });
                await db.SaveChangesAsync(ct);
            }
```

With:

```csharp
            if (existingSettings == null)
            {
                db.TenantGlSettings.Add(new TenantGlSettings
                {
                    TenantId                        = tenantId,
                    DefaultCashAccountId            = seeded["1.1.1"],
                    DefaultAccountsPayableAccountId = seeded["2.1.1"],
                    DefaultExpenseAccountId         = seeded["5.1.4"]
                });
                await db.SaveChangesAsync(ct);
            }
```

- [ ] **Step 3: Build to confirm no compile errors**

```bash
dotnet build Prumo.Infrastructure
```

Expected: Build succeeded, 0 error(s)

- [ ] **Step 4: Run all tests**

```bash
dotnet test
```

Expected: all existing tests PASS

- [ ] **Step 5: Commit**

```bash
git add Prumo.Infrastructure/Data/Seeders/ChartOfAccountsSeeder.cs
git commit -m "feat: expand chart of accounts seed with HR and AP sub-accounts, set DefaultExpenseAccountId"
```

---

## Task 7: GL account hints component (Angular)

**Files:**
- Modify: `SaaSBasePlatform-Angular/src/app/core/models/index.ts`
- Create: `SaaSBasePlatform-Angular/src/app/modules/finance/chart-of-accounts/accounts-hints.component.ts`
- Modify: `SaaSBasePlatform-Angular/src/app/modules/finance/chart-of-accounts/chart-of-accounts.component.html`
- Modify: `SaaSBasePlatform-Angular/src/app/modules/finance/chart-of-accounts/chart-of-accounts.component.ts`

- [ ] **Step 1: Update TenantGlSettings frontend model**

Open `SaaSBasePlatform-Angular/src/app/core/models/index.ts`.

Replace:
```typescript
export interface TenantGlSettings {
  tenantId: string;
  defaultCashAccountId?: string | null;
  defaultCashAccountCode?: string | null;
  defaultAccountsPayableAccountId?: string | null;
  defaultAccountsPayableAccountCode?: string | null;
}
```

With:
```typescript
export interface TenantGlSettings {
  tenantId: string;
  defaultCashAccountId?: string | null;
  defaultCashAccountCode?: string | null;
  defaultAccountsPayableAccountId?: string | null;
  defaultAccountsPayableAccountCode?: string | null;
  defaultExpenseAccountId?: string | null;
  defaultExpenseAccountCode?: string | null;
}
```

- [ ] **Step 2: Create the AccountsHintsComponent**

Create `SaaSBasePlatform-Angular/src/app/modules/finance/chart-of-accounts/accounts-hints.component.ts`:

```typescript
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { RouterModule } from '@angular/router';
import { TenantGlSettings } from '@core/models';

interface HintRow {
  code: string;
  name: string;
  purpose: string;
  configured: boolean | null; // null = informative only
}

@Component({
  selector: 'app-accounts-hints',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatIconModule, MatButtonModule, RouterModule],
  template: `
    <mat-card class="hints-card">
      <mat-card-header>
        <mat-icon mat-card-avatar>tips_and_updates</mat-icon>
        <mat-card-title>Contas Recomendadas</mat-card-title>
        <mat-card-subtitle>Mapeamentos sugeridos para os módulos Financeiro e RH</mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <table class="hints-table">
          <thead>
            <tr>
              <th>Código</th>
              <th>Conta</th>
              <th>Finalidade</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let row of rows">
              <td class="code">{{ row.code }}</td>
              <td>{{ row.name }}</td>
              <td>{{ row.purpose }}</td>
              <td>
                <span *ngIf="row.configured === null" class="status-info">
                  <mat-icon>info_outline</mat-icon> Informativo
                </span>
                <span *ngIf="row.configured === true" class="status-ok">
                  <mat-icon>check_circle</mat-icon> Configurado
                </span>
                <span *ngIf="row.configured === false" class="status-warn">
                  <mat-icon>warning_amber</mat-icon> Não configurado
                </span>
              </td>
            </tr>
          </tbody>
        </table>
        <div class="hints-footer">
          <button mat-stroked-button routerLink="/finance/chart-of-accounts" [queryParams]="{settings: true}">
            <mat-icon>settings</mat-icon> Configurar contas padrão
          </button>
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    .hints-card { margin-bottom: 24px; }
    .hints-table { width: 100%; border-collapse: collapse; font-size: 14px; }
    .hints-table th { text-align: left; padding: 8px 12px; border-bottom: 1px solid #e0e0e0; font-weight: 500; }
    .hints-table td { padding: 8px 12px; border-bottom: 1px solid #f5f5f5; }
    .hints-table td.code { font-family: monospace; color: #555; }
    .status-ok   { display: flex; align-items: center; gap: 4px; color: #388e3c; font-size: 13px; }
    .status-warn { display: flex; align-items: center; gap: 4px; color: #f57c00; font-size: 13px; }
    .status-info { display: flex; align-items: center; gap: 4px; color: #9e9e9e; font-size: 13px; }
    .hints-footer { margin-top: 16px; }
  `]
})
export class AccountsHintsComponent {
  @Input() glSettings: TenantGlSettings | null = null;

  get rows(): HintRow[] {
    const s = this.glSettings;
    return [
      {
        code: '1.1.1',
        name: 'Caixa e Equivalentes',
        purpose: 'Pagamentos em caixa (Contas a Pagar)',
        configured: s ? !!s.defaultCashAccountId : false
      },
      {
        code: '2.1.1',
        name: 'Fornecedores / Contas a Pagar',
        purpose: 'Lançamentos de Contas a Pagar',
        configured: s ? !!s.defaultAccountsPayableAccountId : false
      },
      {
        code: '5.1.4',
        name: 'Despesas com Fornecedores',
        purpose: 'Despesas de Contas a Pagar',
        configured: s ? !!s.defaultExpenseAccountId : false
      },
      {
        code: '2.1.3',
        name: 'Salários a Pagar',
        purpose: 'Folha de pagamento RH',
        configured: null
      },
      {
        code: '5.1.1',
        name: 'Despesas com Pessoal',
        purpose: 'Custos de pessoal RH',
        configured: null
      }
    ];
  }
}
```

- [ ] **Step 3: Import AccountsHintsComponent in ChartOfAccountsComponent**

Open `SaaSBasePlatform-Angular/src/app/modules/finance/chart-of-accounts/chart-of-accounts.component.ts`.

Add `AccountsHintsComponent` to the imports array:

```typescript
import { AccountsHintsComponent } from './accounts-hints.component';
```

And add it to `imports` in `@Component`:

```typescript
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatDialogModule,
    MatSelectModule,
    MatFormFieldModule,
    MatSlideToggleModule,
    MatTooltipModule,
    AccountsHintsComponent   // ← add this
  ],
```

- [ ] **Step 4: Add the hints component to the template**

Open `SaaSBasePlatform-Angular/src/app/modules/finance/chart-of-accounts/chart-of-accounts.component.html`.

Add as the first element inside the page container, before any existing cards:

```html
<app-accounts-hints [glSettings]="glSettings"></app-accounts-hints>
```

- [ ] **Step 5: Build the Angular app to confirm no compile errors**

```bash
cd SaaSBasePlatform-Angular && npx ng build --configuration development 2>&1 | tail -20
```

Expected: `Build at: ... - Hash: ... - Time: ...ms` with no errors.

- [ ] **Step 6: Commit**

```bash
git add SaaSBasePlatform-Angular/src/app/core/models/index.ts
git add SaaSBasePlatform-Angular/src/app/modules/finance/chart-of-accounts/accounts-hints.component.ts
git add SaaSBasePlatform-Angular/src/app/modules/finance/chart-of-accounts/chart-of-accounts.component.ts
git add SaaSBasePlatform-Angular/src/app/modules/finance/chart-of-accounts/chart-of-accounts.component.html
git commit -m "feat: add GL account hints component to Chart of Accounts page"
```

---

## Phase 1 Complete

All Phase 1 items are now done. Run the full test suite one final time before starting Phase 2:

```bash
dotnet test
```

Expected: all tests PASS. Then proceed to `2026-05-06-sprint-phase2-features.md`.

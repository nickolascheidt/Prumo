# Sprint Spec: Roles, Dashboards & HR Periods

**Date:** 2026-05-06
**Approach:** Two-phase — Phase 1 (fixes + backend groundwork) must be committed before Phase 2 (new features) begins.

---

## Scope

| # | Item | Phase |
|---|------|-------|
| 1 | Remove admin placeholder from login form | 1 |
| 2 | Suppress duplicate startup logs in DbInitializer | 1 |
| 3 | Delete "User" role from the system | 1 |
| 4 | Seed module-specific roles (RH, Financeiro, ContasAPagar) with scoped permissions | 1 |
| 5 | Expand Chart of Accounts seeder + add GL hints UI | 1 |
| 6 | Tenant members UI (execute existing plan) | 2 |
| 7 | Finance dashboard (replace placeholder) | 2 |
| 8 | HR dashboard tab | 2 |
| 9 | HR Payment Periods view | 2 |

---

## Phase 1: Fixes & Backend Groundwork

### 1. Login placeholder removal

**File:** `SaaSBasePlatform-Angular/src/app/modules/auth/login/login.component.ts`

`initForm()` currently pre-fills `email: 'admin@saas-baseplatform.com'` and `password: 'Admin@123'`. Both fields change to `''` (empty string). The `tenantSlug` field stays as `''`.

---

### 2. Startup log suppression

**File:** `SaaS_BasePlatform.Infrastructure/Data/DbInitializer.cs`

Any `logger.LogInformation` call inside an "already exists" branch changes to `logger.LogDebug`. Affected branches:
- Role already exists check
- Permission already exists check
- Admin user already exists check

The `LogInformation` calls for *newly created* resources stay as-is.

---

### 3. "User" role cleanup

**File:** `SaaS_BasePlatform.Infrastructure/Data/DbInitializer.cs`

Add a one-time cleanup block at the top of `InitializeAsync`, before the role seeding loop:

```csharp
var userRole = await roleManager.FindByNameAsync("User");
if (userRole != null)
{
    // Remove associated RolePermissions first (FK constraint)
    var rps = context.RolePermissions.Where(rp => rp.RoleId == userRole.Id);
    context.RolePermissions.RemoveRange(rps);
    await context.SaveChangesAsync();
    await roleManager.DeleteAsync(userRole);
    logger.LogInformation("✓ Role 'User' removida");
}
```

No migration required — pure data cleanup at startup.

---

### 4. Module-specific roles + permissions

#### 4a. New permission classes — `Permissions.cs`

**File:** `SaaS_BasePlatform.Domain/Authorization/Permissions.cs`

Add two new static nested classes alongside the existing ones:

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

Add all new permission strings to `GetAllPermissions()`.

#### 4b. New default role permission sets — `Permissions.cs`

Add to `DefaultRolePermissions`:

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
    AccountsPayable.View, AccountsPayable.Create, AccountsPayable.Edit,
    AccountsPayable.Delete, AccountsPayable.ManageCategories
};
```

`Admin` stays as `GetAllPermissions()` — the new permissions are automatically included.

#### 4c. Seed new roles — `DbInitializer.cs`

Add to `rolesConfig`:

```csharp
{ "RH",          "Acesso ao módulo de Recursos Humanos" },
{ "Financeiro",  "Acesso ao módulo Financeiro" },
{ "ContasAPagar","Acesso ao módulo de Contas a Pagar" }
```

#### 4d. Tenant bootstrap — `TenantBootstrapSeeder.cs`

Add to `rolePermissionConfig`:

```csharp
{ "RH",           Permissions.DefaultRolePermissions.RH },
{ "Financeiro",   Permissions.DefaultRolePermissions.Financeiro },
{ "ContasAPagar", Permissions.DefaultRolePermissions.ContasAPagar }
```

Also add `ResourcePermissions` for the three new roles. Use the `Resource.Module` field for the mapping:

| Role | `PermissionLevel.Full` for Module | `PermissionLevel.Read` for other modules |
|---|---|---|
| RH | `"RH"` | no access to other modules |
| Financeiro | `"Financeiro"` | no access to other modules |
| ContasAPagar | `"ContasAPagar"` | no access to other modules |

Each new role only receives `ResourcePermission` rows for resources whose `Module` matches their own. Resources from other modules are not granted at all (no row = no access).

---

### 5. Chart of Accounts seeder expansion + GL hints UI

#### 5a. Expanded accounts — `ChartOfAccountsSeeder.cs`

Add to `DefaultAccounts` array:

```
2.1.3  Salários a Pagar           Liability  analytic  parent: 2.1
2.1.4  Encargos Sociais a Pagar   Liability  analytic  parent: 2.1
5.1.1  Despesas com Pessoal       Expense    analytic  parent: 5.1
5.1.2  Despesas Administrativas   Expense    analytic  parent: 5.1
5.1.3  Despesas Operacionais      Expense    analytic  parent: 5.1
5.1.4  Despesas com Fornecedores  Expense    analytic  parent: 5.1
```

#### 5b. TenantGlSettings — new field

**Files:** `Domain/Entities/TenantGlSettings.cs`, EF configuration, and a new EF migration.

Add: `public Guid? DefaultExpenseAccountId { get; set; }`

`ChartOfAccountsSeeder.cs` assigns it to the seeded `5.1.4` ID when writing `TenantGlSettings`.

#### 5c. GL account hints UI

**File (new):** `SaaSBasePlatform-Angular/src/app/modules/finance/chart-of-accounts/accounts-hints.component.ts`

A read-only standalone `mat-card` component added at the top of the Chart of Accounts page. It calls the existing GL settings endpoint to read `TenantGlSettings`, then renders a table:

| Conta recomendada | Finalidade | Status |
|---|---|---|
| Caixa e Equivalentes (1.1.1) | Pagamentos em caixa | Configurado / Não configurado |
| Fornecedores / Contas a Pagar (2.1.1) | Lançamentos de AP | Configurado / Não configurado |
| Despesas com Fornecedores (5.1.4) | Despesas de AP | Configurado / Não configurado |
| Salários a Pagar (2.1.3) | Folha de pagamento RH | Informativo |
| Despesas com Pessoal (5.1.1) | Custos de RH | Informativo |

Unconfigured mappings render with an amber `mat-icon` warning. "Informativo" rows render in muted text (not a warning). A "Configurar" button links to the GL Settings page.

---

## Phase 2: New Features

> Phase 2 starts only after Phase 1 is committed and passing.

### 6. Tenant members UI

Execute the existing plan verbatim:
`docs/superpowers/plans/2026-05-05-user-management.md`

No design changes. The plan covers: backend endpoints (lookup by email, update member role) + `TenantMembersComponent` with add/change-role/remove actions.

---

### 7. Finance dashboard

**Files:**
- `SaaSBasePlatform-Angular/src/app/modules/dashboard/finance/dashboard-finance.component.ts`
- `SaaSBasePlatform-Angular/src/app/modules/dashboard/finance/dashboard-finance.component.html`

Replace the placeholder with:

**Summary tiles (3x `mat-card`):**
- Total a Pagar — sum of AP entries with status `Pending`
- Vencidas — count/sum of AP entries past due date with status `Pending`
- Pagas este mês — sum of AP entries marked paid in the current calendar month

Data source: `GET /api/tenants/{tenantId}/accounts-payable/entries` — fetch all entries (no pagination, large page size) and compute tiles client-side:
- **Total a Pagar**: sum `amount` where `status === 'Pending'`
- **Vencidas**: sum `amount` where `status === 'Pending' && dueDate < today`
- **Pagas este mês**: sum `amount` where `status === 'Paid' && paidAt` is within the current calendar month

**Category breakdown (simple bar chart):**
Use Angular Material's `mat-progress-bar` per category as a lightweight chart (no additional chart dependency). Each bar shows category name + amount + percentage of total.

Data source: existing `GET /api/tenants/{tenantId}/accounts-payable/entries` grouped client-side by category.

---

### 8. HR dashboard tab

**Files:**
- `SaaSBasePlatform-Angular/src/app/modules/dashboard/dashboard.component.ts` — add tab
- New: `SaaSBasePlatform-Angular/src/app/modules/dashboard/hr/dashboard-hr.component.ts`
- New: `SaaSBasePlatform-Angular/src/app/modules/dashboard/hr/dashboard-hr.component.html`
- `app.routes.ts` — add child route `hr` under `dashboard`

**Tab entry added to `dashboard.component.ts`:**
```typescript
{ label: 'RH', route: 'hr', roles: ['Administrador', 'RH', 'Funcionario'] }
```

**`DashboardHrComponent` content:**

Summary tiles (3x `mat-card`):
- Total de Funcionários — count from `GET /employees`
- Pagamentos este período — sum of payments in the current payment period
- Horas registradas este mês — sum of work log hours in current calendar month

Recent payments list — last 5 HR payments in a `mat-list`, showing employee name, amount, date.

---

### 9. HR Payment Periods view

**Files:**
- New: `SaaSBasePlatform-Angular/src/app/modules/hr/payment-periods/payment-periods.component.ts`
- `app.routes.ts` — add route `/hr/periodos`
- Sidebar/nav — add "Períodos" link under HR section

**Component:** Standalone `mat-table` listing all `PaymentPeriod` records with columns: Nome, Período (start–end), Total, Status. Read-only. The existing "Gerar Período" dialog button from `payments.component.ts` is not duplicated here — this view is browse-only.

API: `GET /api/tenants/{tenantId}/payment-periods` (existing endpoint in `PaymentPeriodsController`).

---

## Key constraints

- **No new external dependencies** — charts use Angular Material primitives only.
- **No changes to existing GL posting flow** — AP mark-paid GL posting stays as-is.
- **Admin always has all permissions** — `DefaultRolePermissions.Admin = GetAllPermissions()`, which auto-includes new permission strings.
- **Existing roles (`Funcionario`, `Cliente`) are unchanged** — new module roles are additive.
- **`TenantBootstrapSeeder` is idempotent** — all inserts guard with `AnyAsync` checks; re-running is safe.

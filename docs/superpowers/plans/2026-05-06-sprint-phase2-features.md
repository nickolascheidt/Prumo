# Sprint Phase 2 — New Features

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add four new user-facing features: tenant member management UI, a Finance dashboard, an HR dashboard tab, and an HR Payment Periods view.

**Architecture:** All four tasks are independent — they touch different files and can be worked in parallel if using subagent-driven-development. Each task produces a self-contained, working feature. Backend additions are minimal (one new endpoint for all payment periods); the rest is purely frontend.

**Tech Stack:** Angular 18 standalone, Angular Material 18, `@core/services/api.service.ts`, `@core/models/index.ts`, `app.routes.ts`, `layout.component.ts`. .NET 10 for the payment periods endpoint.

**Prerequisite:** Phase 1 plan (`2026-05-06-sprint-phase1-fixes-groundwork.md`) must be fully committed before starting this plan.

---

## File Map

| File | Action |
|------|--------|
| *(See existing plan)* `docs/superpowers/plans/2026-05-05-user-management.md` | Execute verbatim |
| `SaaSBasePlatform-Angular/src/app/modules/dashboard/finance/dashboard-finance.component.ts` | Modify — replace placeholder with real data |
| `SaaSBasePlatform-Angular/src/app/modules/dashboard/finance/dashboard-finance.component.html` | Modify — replace placeholder template |
| `SaaSBasePlatform-Angular/src/app/modules/dashboard/dashboard.component.ts` | Modify — add HR tab |
| `SaaSBasePlatform-Angular/src/app/modules/dashboard/hr/dashboard-hr.component.ts` | Create — HR summary tiles + recent payments |
| `SaaSBasePlatform-Angular/src/app/modules/dashboard/hr/dashboard-hr.component.html` | Create |
| `Prumo.Application/Services/IPaymentPeriodService.cs` | Modify — add ListAllByTenantAsync |
| `Prumo.Application/Services/PaymentPeriodService.cs` | Modify — implement ListAllByTenantAsync |
| `Prumo.Api/Controllers/PaymentPeriodsController.cs` | Modify — add tenant-wide GET endpoint |
| `SaaSBasePlatform-Angular/src/app/core/services/api.service.ts` | Modify — add getAllPaymentPeriods |
| `SaaSBasePlatform-Angular/src/app/modules/hr/payment-periods/payment-periods.component.ts` | Create — read-only table of all periods |
| `SaaSBasePlatform-Angular/src/app/app.routes.ts` | Modify — add hr dashboard child route + /hr/periodos |
| `SaaSBasePlatform-Angular/src/app/shared/components/layout/layout.component.ts` | Modify — add Períodos nav item |

---

## Task 1: Tenant Members UI

This task is fully specified in an existing plan. Execute it verbatim.

**Files:** As listed in `docs/superpowers/plans/2026-05-05-user-management.md`

- [ ] **Step 1: Execute the existing user-management plan**

Open `docs/superpowers/plans/2026-05-05-user-management.md` and follow every step exactly as written.

- [ ] **Step 2: Verify the feature works**

Start the API and Angular dev server. Navigate to the Admin section. Confirm:
- The "Membros do Tenant" page lists current members
- You can look up a user by email and add them
- You can change a member's role
- You can remove a member

---

## Task 2: Finance Dashboard

**Files:**
- Modify: `SaaSBasePlatform-Angular/src/app/modules/dashboard/finance/dashboard-finance.component.ts`
- Modify: `SaaSBasePlatform-Angular/src/app/modules/dashboard/finance/dashboard-finance.component.html`

- [ ] **Step 1: Replace the Finance dashboard component**

Replace the entire contents of `SaaSBasePlatform-Angular/src/app/modules/dashboard/finance/dashboard-finance.component.ts`:

```typescript
import { Component, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService, AuthService } from '@core/services';
import { AccountsPayableSummary } from '@core/models';

@Component({
  selector: 'app-dashboard-finance',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatIconModule, MatProgressBarModule, MatProgressSpinnerModule, CurrencyPipe, DatePipe],
  templateUrl: './dashboard-finance.component.html'
})
export class DashboardFinanceComponent implements OnInit {
  summary: AccountsPayableSummary | null = null;
  isLoading = false;

  private tenantId: string | null = null;

  constructor(private api: ApiService, private auth: AuthService) {}

  ngOnInit(): void {
    this.tenantId = this.auth.getCurrentTenantId();
    if (!this.tenantId) return;
    this.load();
  }

  private load(): void {
    if (!this.tenantId) return;
    this.isLoading = true;
    const now = new Date();
    const firstOfMonth = new Date(now.getFullYear(), now.getMonth(), 1).toISOString().split('T')[0];
    const lastOfMonth  = new Date(now.getFullYear(), now.getMonth() + 1, 0).toISOString().split('T')[0];

    this.api.getAccountsPayableSummary(this.tenantId, firstOfMonth, lastOfMonth).subscribe({
      next: s => { this.summary = s; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  get totalPending(): number { return this.summary?.totalPending ?? 0; }
  get totalPaid(): number    { return this.summary?.totalPaid    ?? 0; }
  get grandTotal(): number   { return this.totalPending + this.totalPaid; }

  get overdueEstimate(): number {
    // summary doesn't break out overdue separately; show totalPending as proxy
    return this.totalPending;
  }

  get categoryRows(): { name: string; amount: number; pct: number }[] {
    if (!this.summary?.totalsByCategory?.length) return [];
    const cats = this.summary.totalsByCategory;
    const max = Math.max(...cats.map(c => c.totalPending + c.totalPaid), 1);
    return cats
      .map(c => ({
        name:   c.categoryName,
        amount: c.totalPending + c.totalPaid,
        pct:    Math.round(((c.totalPending + c.totalPaid) / max) * 100)
      }))
      .sort((a, b) => b.amount - a.amount)
      .slice(0, 8);
  }
}
```

- [ ] **Step 2: Replace the Finance dashboard template**

Replace the entire contents of `SaaSBasePlatform-Angular/src/app/modules/dashboard/finance/dashboard-finance.component.html`:

```html
<div class="finance-dash">
  <div *ngIf="isLoading" class="spinner-wrap">
    <mat-spinner diameter="48"></mat-spinner>
  </div>

  <ng-container *ngIf="!isLoading">
    <!-- Summary tiles -->
    <div class="tiles">
      <mat-card class="tile">
        <mat-card-content>
          <mat-icon class="tile-icon pending">pending_actions</mat-icon>
          <div class="tile-value">{{ totalPending | currency:'BRL':'symbol':'1.2-2' }}</div>
          <div class="tile-label">A Pagar (mês)</div>
        </mat-card-content>
      </mat-card>

      <mat-card class="tile">
        <mat-card-content>
          <mat-icon class="tile-icon paid">check_circle</mat-icon>
          <div class="tile-value">{{ totalPaid | currency:'BRL':'symbol':'1.2-2' }}</div>
          <div class="tile-label">Pago (mês)</div>
        </mat-card-content>
      </mat-card>

      <mat-card class="tile">
        <mat-card-content>
          <mat-icon class="tile-icon total">account_balance_wallet</mat-icon>
          <div class="tile-value">{{ grandTotal | currency:'BRL':'symbol':'1.2-2' }}</div>
          <div class="tile-label">Total (mês)</div>
        </mat-card-content>
      </mat-card>
    </div>

    <!-- Category breakdown -->
    <mat-card *ngIf="categoryRows.length > 0" class="category-card">
      <mat-card-header>
        <mat-card-title>Despesas por Categoria</mat-card-title>
      </mat-card-header>
      <mat-card-content>
        <div *ngFor="let row of categoryRows" class="cat-row">
          <div class="cat-name">{{ row.name }}</div>
          <mat-progress-bar mode="determinate" [value]="row.pct" class="cat-bar"></mat-progress-bar>
          <div class="cat-amount">{{ row.amount | currency:'BRL':'symbol':'1.2-2' }}</div>
        </div>
      </mat-card-content>
    </mat-card>

    <p *ngIf="!summary" class="no-data">Nenhum dado disponível para este período.</p>
  </ng-container>
</div>

<style>
  .finance-dash { padding: 16px 0; }
  .spinner-wrap { display: flex; justify-content: center; padding: 48px; }
  .tiles { display: flex; gap: 16px; flex-wrap: wrap; margin-bottom: 24px; }
  .tile { flex: 1; min-width: 180px; }
  .tile mat-card-content { display: flex; flex-direction: column; align-items: center; padding: 16px; gap: 8px; }
  .tile-icon { font-size: 36px; width: 36px; height: 36px; }
  .tile-icon.pending { color: #f57c00; }
  .tile-icon.paid    { color: #388e3c; }
  .tile-icon.total   { color: #1976d2; }
  .tile-value { font-size: 22px; font-weight: 600; }
  .tile-label { font-size: 13px; color: #757575; }
  .category-card { margin-bottom: 24px; }
  .cat-row { display: grid; grid-template-columns: 180px 1fr 120px; align-items: center; gap: 12px; margin-bottom: 10px; }
  .cat-name { font-size: 14px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .cat-bar { height: 8px; border-radius: 4px; }
  .cat-amount { font-size: 13px; text-align: right; color: #424242; }
  .no-data { color: #9e9e9e; text-align: center; padding: 32px; }
</style>
```

- [ ] **Step 3: Build to confirm no compile errors**

```bash
cd SaaSBasePlatform-Angular && npx ng build --configuration development 2>&1 | tail -20
```

Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add SaaSBasePlatform-Angular/src/app/modules/dashboard/finance/
git commit -m "feat: implement Finance dashboard with AP summary tiles and category breakdown"
```

---

## Task 3: Backend — ListAllByTenantAsync + tenant-wide payment periods endpoint

**Files:**
- Modify: `Prumo.Application/Services/IPaymentPeriodService.cs`
- Modify: `Prumo.Application/Services/PaymentPeriodService.cs`
- Modify: `Prumo.Api/Controllers/PaymentPeriodsController.cs`

The existing `PaymentPeriodsController` route is `api/tenants/{tenantId}/employees/{employeeId}/payment-periods`. A new action with an absolute route override provides `GET api/tenants/{tenantId}/payment-periods` without changing the controller class.

- [ ] **Step 1: Add ListAllByTenantAsync to the service interface**

Open `Prumo.Application/Services/IPaymentPeriodService.cs`.

Add to the interface:

```csharp
    Task<IReadOnlyList<PaymentPeriodSummaryDto>> ListAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
```

- [ ] **Step 2: Implement ListAllByTenantAsync in PaymentPeriodService**

Open `Prumo.Application/Services/PaymentPeriodService.cs`.

Add the method before `UpdateStatusAsync`:

```csharp
        public async Task<IReadOnlyList<PaymentPeriodSummaryDto>> ListAllByTenantAsync(
            Guid tenantId, CancellationToken ct = default)
        {
            return await _db.PaymentPeriods
                .IgnoreQueryFilters()
                .Include(p => p.Employee)
                .Where(p => p.Employee.TenantId == tenantId)
                .OrderByDescending(p => p.StartDate)
                .Select(p => ToSummaryDto(p))
                .ToListAsync(ct);
        }
```

- [ ] **Step 3: Add the tenant-wide GET endpoint to PaymentPeriodsController**

Open `Prumo.Api/Controllers/PaymentPeriodsController.cs`.

Add the following action before the existing `List` action. The `[HttpGet]` with an absolute path overrides the controller-level route for this action only:

```csharp
        [HttpGet("/api/tenants/{tenantId:guid}/payment-periods")]
        [ProducesResponseType(typeof(IReadOnlyList<PaymentPeriodSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<PaymentPeriodSummaryDto>>> ListAll(
            Guid tenantId, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.ListAllByTenantAsync(tenantId, ct));
        }
```

- [ ] **Step 4: Build to confirm no compile errors**

```bash
dotnet build Prumo.Api
```

Expected: Build succeeded, 0 error(s)

- [ ] **Step 5: Add getAllPaymentPeriods to api.service.ts**

Open `SaaSBasePlatform-Angular/src/app/core/services/api.service.ts`.

After the existing `getPaymentPeriods` method (around line 543), add:

```typescript
  getAllPaymentPeriods(tenantId: string): Observable<PaymentPeriodSummary[]> {
    return this.http.get<PaymentPeriodSummary[]>(
      `${this.apiUrl}/tenants/${encodeURIComponent(tenantId)}/payment-periods`
    );
  }
```

- [ ] **Step 6: Commit**

```bash
git add Prumo.Application/Services/IPaymentPeriodService.cs
git add Prumo.Application/Services/PaymentPeriodService.cs
git add Prumo.Api/Controllers/PaymentPeriodsController.cs
git add SaaSBasePlatform-Angular/src/app/core/services/api.service.ts
git commit -m "feat: add ListAllByTenantAsync and tenant-wide payment periods endpoint"
```

---

## Task 4: HR Dashboard tab

**Files:**
- Create: `SaaSBasePlatform-Angular/src/app/modules/dashboard/hr/dashboard-hr.component.ts`
- Create: `SaaSBasePlatform-Angular/src/app/modules/dashboard/hr/dashboard-hr.component.html`
- Modify: `SaaSBasePlatform-Angular/src/app/modules/dashboard/dashboard.component.ts`
- Modify: `SaaSBasePlatform-Angular/src/app/app.routes.ts`

- [ ] **Step 1: Create the DashboardHrComponent**

Create `SaaSBasePlatform-Angular/src/app/modules/dashboard/hr/dashboard-hr.component.ts`:

```typescript
import { Component, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService, AuthService } from '@core/services';
import { Employee, HrPayment } from '@core/models';
import { forkJoin } from 'rxjs';

@Component({
  selector: 'app-dashboard-hr',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatIconModule, MatListModule, MatProgressSpinnerModule, CurrencyPipe, DatePipe],
  templateUrl: './dashboard-hr.component.html'
})
export class DashboardHrComponent implements OnInit {
  employees: Employee[] = [];
  recentPayments: HrPayment[] = [];
  isLoading = false;

  private tenantId: string | null = null;

  constructor(private api: ApiService, private auth: AuthService) {}

  ngOnInit(): void {
    this.tenantId = this.auth.getCurrentTenantId();
    if (!this.tenantId) return;
    this.load();
  }

  private load(): void {
    if (!this.tenantId) return;
    this.isLoading = true;
    forkJoin({
      employees: this.api.getEmployees(this.tenantId!),
      payments:  this.api.getRecentHrPayments(this.tenantId!, 5)
    }).subscribe({
      next: ({ employees, payments }) => {
        this.employees      = employees;
        this.recentPayments = payments;
        this.isLoading      = false;
      },
      error: () => { this.isLoading = false; }
    });
  }

  get activeEmployeeCount(): number {
    return this.employees.filter(e => e.isActive).length;
  }

  get recentPaymentsTotal(): number {
    return this.recentPayments.reduce((sum, p) => sum + p.amount, 0);
  }
}
```

- [ ] **Step 2: Create the HR dashboard template**

Create `SaaSBasePlatform-Angular/src/app/modules/dashboard/hr/dashboard-hr.component.html`:

```html
<div class="hr-dash">
  <div *ngIf="isLoading" class="spinner-wrap">
    <mat-spinner diameter="48"></mat-spinner>
  </div>

  <ng-container *ngIf="!isLoading">
    <!-- Summary tiles -->
    <div class="tiles">
      <mat-card class="tile">
        <mat-card-content>
          <mat-icon class="tile-icon emp">badge</mat-icon>
          <div class="tile-value">{{ activeEmployeeCount }}</div>
          <div class="tile-label">Funcionários ativos</div>
        </mat-card-content>
      </mat-card>

      <mat-card class="tile">
        <mat-card-content>
          <mat-icon class="tile-icon pay">payments</mat-icon>
          <div class="tile-value">{{ recentPaymentsTotal | currency:'BRL':'symbol':'1.2-2' }}</div>
          <div class="tile-label">Últimos pagamentos</div>
        </mat-card-content>
      </mat-card>
    </div>

    <!-- Recent payments list -->
    <mat-card *ngIf="recentPayments.length > 0" class="payments-card">
      <mat-card-header>
        <mat-card-title>Pagamentos Recentes</mat-card-title>
      </mat-card-header>
      <mat-card-content>
        <mat-list>
          <mat-list-item *ngFor="let p of recentPayments">
            <mat-icon matListItemIcon>person</mat-icon>
            <span matListItemTitle>{{ p.employeeName }}</span>
            <span matListItemLine>
              {{ p.amount | currency:'BRL':'symbol':'1.2-2' }} · {{ p.paymentDate | date:'dd/MM/yyyy' }}
            </span>
          </mat-list-item>
        </mat-list>
      </mat-card-content>
    </mat-card>

    <p *ngIf="!isLoading && employees.length === 0" class="no-data">
      Nenhum funcionário cadastrado.
    </p>
  </ng-container>
</div>

<style>
  .hr-dash { padding: 16px 0; }
  .spinner-wrap { display: flex; justify-content: center; padding: 48px; }
  .tiles { display: flex; gap: 16px; flex-wrap: wrap; margin-bottom: 24px; }
  .tile { flex: 1; min-width: 180px; }
  .tile mat-card-content { display: flex; flex-direction: column; align-items: center; padding: 16px; gap: 8px; }
  .tile-icon { font-size: 36px; width: 36px; height: 36px; }
  .tile-icon.emp { color: #5c6bc0; }
  .tile-icon.pay { color: #26a69a; }
  .tile-value { font-size: 22px; font-weight: 600; }
  .tile-label { font-size: 13px; color: #757575; }
  .payments-card { margin-bottom: 24px; }
  .no-data { color: #9e9e9e; text-align: center; padding: 32px; }
</style>
```

- [ ] **Step 3: Add the RH tab to DashboardComponent**

Open `SaaSBasePlatform-Angular/src/app/modules/dashboard/dashboard.component.ts`.

Replace:
```typescript
  readonly tabs: DashTab[] = [
    { label: 'Visão Geral', route: 'overview' },
    { label: 'Contabilidade', route: 'accounting', roles: ['Administrador', 'Funcionario'] },
    { label: 'Financeiro', route: 'finance' },
    { label: 'Administração', route: 'admin', roles: ['Administrador'] }
  ];
```

With:
```typescript
  readonly tabs: DashTab[] = [
    { label: 'Visão Geral', route: 'overview' },
    { label: 'Contabilidade', route: 'accounting', roles: ['Administrador', 'Funcionario'] },
    { label: 'Financeiro', route: 'finance' },
    { label: 'RH', route: 'hr', roles: ['Administrador', 'RH', 'Funcionario'] },
    { label: 'Administração', route: 'admin', roles: ['Administrador'] }
  ];
```

- [ ] **Step 4: Add the hr child route to app.routes.ts**

Open `SaaSBasePlatform-Angular/src/app/app.routes.ts`.

Add the import at the top:
```typescript
import { DashboardHrComponent } from './modules/dashboard/hr/dashboard-hr.component';
```

Add the child route inside the `dashboard` children array, after the `finance` route:
```typescript
          { path: 'hr', component: DashboardHrComponent, data: { roles: ['Administrador', 'RH', 'Funcionario'] } },
```

- [ ] **Step 5: Build to confirm no compile errors**

```bash
cd SaaSBasePlatform-Angular && npx ng build --configuration development 2>&1 | tail -20
```

Expected: no errors.

- [ ] **Step 6: Commit**

```bash
git add SaaSBasePlatform-Angular/src/app/modules/dashboard/hr/
git add SaaSBasePlatform-Angular/src/app/modules/dashboard/dashboard.component.ts
git add SaaSBasePlatform-Angular/src/app/app.routes.ts
git commit -m "feat: add HR dashboard tab with employee count and recent payments"
```

---

## Task 5: HR Payment Periods view

**Files:**
- Create: `SaaSBasePlatform-Angular/src/app/modules/hr/payment-periods/payment-periods.component.ts`
- Modify: `SaaSBasePlatform-Angular/src/app/app.routes.ts`
- Modify: `SaaSBasePlatform-Angular/src/app/shared/components/layout/layout.component.ts`

- [ ] **Step 1: Create the PaymentPeriodsComponent**

Create `SaaSBasePlatform-Angular/src/app/modules/hr/payment-periods/payment-periods.component.ts`:

```typescript
import { Component, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService, AuthService } from '@core/services';
import { PaymentPeriodSummary, HrPaymentStatus } from '@core/models';

@Component({
  selector: 'app-payment-periods',
  standalone: true,
  imports: [
    CommonModule, MatTableModule, MatCardModule, MatChipsModule,
    MatIconModule, MatProgressSpinnerModule, CurrencyPipe, DatePipe
  ],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h1>Períodos de Pagamento</h1>
          <p class="subtitle">Todos os períodos gerados para funcionários</p>
        </div>
      </div>

      <mat-card>
        <mat-card-content>
          <div *ngIf="isLoading" class="spinner-wrap">
            <mat-spinner diameter="48"></mat-spinner>
          </div>
          <p *ngIf="!isLoading && periods.length === 0" class="no-data">
            Nenhum período gerado.
          </p>
          <table mat-table [dataSource]="periods" *ngIf="!isLoading && periods.length > 0" class="full-table">

            <ng-container matColumnDef="employeeName">
              <th mat-header-cell *matHeaderCellDef>Funcionário</th>
              <td mat-cell *matCellDef="let p">{{ p.employeeName }}</td>
            </ng-container>

            <ng-container matColumnDef="period">
              <th mat-header-cell *matHeaderCellDef>Período</th>
              <td mat-cell *matCellDef="let p">
                {{ p.startDate | date:'dd/MM/yyyy' }} – {{ p.endDate | date:'dd/MM/yyyy' }}
              </td>
            </ng-container>

            <ng-container matColumnDef="totalHours">
              <th mat-header-cell *matHeaderCellDef>Horas</th>
              <td mat-cell *matCellDef="let p">{{ p.totalHours | number:'1.1-1' }}h</td>
            </ng-container>

            <ng-container matColumnDef="totalAmount">
              <th mat-header-cell *matHeaderCellDef>Total</th>
              <td mat-cell *matCellDef="let p">{{ p.totalAmount | currency:'BRL':'symbol':'1.2-2' }}</td>
            </ng-container>

            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let p">
                <mat-chip [color]="statusColor(p.status)" highlighted>{{ p.statusName }}</mat-chip>
              </td>
            </ng-container>

            <ng-container matColumnDef="createdAt">
              <th mat-header-cell *matHeaderCellDef>Criado em</th>
              <td mat-cell *matCellDef="let p">{{ p.createdAt | date:'dd/MM/yyyy' }}</td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
          </table>
        </mat-card-content>
      </mat-card>
    </div>
  `
})
export class PaymentPeriodsComponent implements OnInit {
  periods: PaymentPeriodSummary[] = [];
  isLoading = false;

  readonly displayedColumns = ['employeeName', 'period', 'totalHours', 'totalAmount', 'status', 'createdAt'];

  private tenantId: string | null = null;

  constructor(private api: ApiService, private auth: AuthService) {}

  ngOnInit(): void {
    this.tenantId = this.auth.getCurrentTenantId();
    if (!this.tenantId) return;
    this.isLoading = true;
    this.api.getAllPaymentPeriods(this.tenantId).subscribe({
      next: periods => { this.periods = periods; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  statusColor(status: HrPaymentStatus): 'primary' | 'accent' | 'warn' {
    switch (status) {
      case HrPaymentStatus.Paid:      return 'primary';
      case HrPaymentStatus.Overdue:   return 'warn';
      default:                        return 'accent';
    }
  }
}
```

- [ ] **Step 2: Add /hr/periodos route to app.routes.ts**

Open `SaaSBasePlatform-Angular/src/app/app.routes.ts`.

Add the import:
```typescript
import { PaymentPeriodsComponent } from './modules/hr/payment-periods/payment-periods.component';
```

Add the route inside the authenticated children array, after the `hr/payments` route:
```typescript
      { path: 'hr/periodos', component: PaymentPeriodsComponent }
```

- [ ] **Step 3: Add "Períodos" nav item to the sidebar**

Open `SaaSBasePlatform-Angular/src/app/shared/components/layout/layout.component.ts`.

Find the `RH` section in `menuSections`:
```typescript
    {
      title: 'RH',
      items: [
        { label: 'Funcionários', icon: 'badge', route: '/hr/employees' },
        { label: 'Horas', icon: 'schedule', route: '/hr/worklogs' },
        { label: 'Pagamentos', icon: 'payments', route: '/hr/payments' }
      ]
    },
```

Replace with:
```typescript
    {
      title: 'RH',
      items: [
        { label: 'Funcionários', icon: 'badge',        route: '/hr/employees' },
        { label: 'Horas',        icon: 'schedule',     route: '/hr/worklogs' },
        { label: 'Pagamentos',   icon: 'payments',     route: '/hr/payments' },
        { label: 'Períodos',     icon: 'event_note',   route: '/hr/periodos' }
      ]
    },
```

- [ ] **Step 4: Build to confirm no compile errors**

```bash
cd SaaSBasePlatform-Angular && npx ng build --configuration development 2>&1 | tail -20
```

Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add SaaSBasePlatform-Angular/src/app/modules/hr/payment-periods/
git add SaaSBasePlatform-Angular/src/app/app.routes.ts
git add SaaSBasePlatform-Angular/src/app/shared/components/layout/layout.component.ts
git commit -m "feat: add HR payment periods view with tenant-wide listing"
```

---

## Phase 2 Complete

Run the full test suite:

```bash
dotnet test
cd SaaSBasePlatform-Angular && npx ng build --configuration development 2>&1 | tail -5
```

Both should pass with 0 errors. Start both servers and manually verify:

1. Login page — fields are empty, no pre-filled credentials
2. Dashboard → RH tab — shows employee count and recent payments
3. Dashboard → Financeiro tab — shows AP summary tiles and category bars
4. `/hr/periodos` — table of all payment periods across all employees
5. Sidebar RH section — "Períodos" link is present
6. Chart of Accounts page — GL hints card appears at top with configured/unconfigured status
7. Admin → Membros do Tenant — can add, change role, and remove members

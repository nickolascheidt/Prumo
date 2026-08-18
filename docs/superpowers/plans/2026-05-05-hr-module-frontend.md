# HR Module Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an Angular 18 HR module (Funcionários, Horas, Pagamentos) to `SaaSBasePlatform-Angular`, adapted from the BiomePampa single-tenant source at `D:\Backup\Angular\BiomePampa\src\app\modules`.

**Architecture:** Three standalone Angular 18 components (Employees, WorkLogs, Payments) each with a mat-dialog form companion, following the existing Finance module pattern. All API calls use `auth.getCurrentTenantId()` for the `tenantId` route segment. The BiomePampa `clockIn`/`clockOut` UX is preserved but converted to a `hoursWorked` decimal before posting to the backend.

**Tech Stack:** Angular 18 (standalone), Angular Material 18, ReactiveFormsModule, `@core/services/api.service.ts`, `@core/models/index.ts`, `app.routes.ts`, `layout.component.ts`

**Angular frontend path:** `C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular`

---

## Key Adaptation Notes (read before implementing any task)

### 1. API URL shape
BiomePampa: `GET /api/employees`
SaaSBasePlatform: `GET /api/tenants/{tenantId}/employees`

Every HR API method in `api.service.ts` receives `tenantId: string` as its first argument.

### 2. Getting tenantId in components
```typescript
private tenantId: string | null = null;

constructor(private auth: AuthService, private api: ApiService, ...) {}

ngOnInit(): void {
  this.tenantId = this.auth.getCurrentTenantId();
  if (!this.tenantId) return;
  this.loadEmployees();
}
```

### 3. WorkLog hours: clockIn/clockOut → hoursWorked
Backend expects `hoursWorked: number` (decimal, e.g. `8.5`). The form uses `clockIn`/`clockOut` time strings for UX. Convert on save:
```typescript
const [inH, inM] = clockIn.split(':').map(Number);
const [outH, outM] = clockOut.split(':').map(Number);
const hoursWorked = ((outH * 60 + outM) - (inH * 60 + inM)) / 60;
```

### 4. Enum values
- `ContractType`: CLT=1, Temporary=2, Daily=3
- `HrPaymentMethod`: Cash=1, Pix=2, BankTransfer=3, Check=4
- `HrPaymentStatus`: Pending=1, Paid=2, Cancelled=3, Overdue=4

### 5. No console.log in production code
Remove all `console.log/warn/error` from BiomePampa source when adapting.

---

## File Map

### New files
- `src/app/modules/hr/employees/employees.component.ts`
- `src/app/modules/hr/employees/employee-form-dialog.component.ts`
- `src/app/modules/hr/worklogs/worklogs.component.ts`
- `src/app/modules/hr/worklogs/worklog-form-dialog.component.ts`
- `src/app/modules/hr/payments/payments.component.ts`
- `src/app/modules/hr/payments/payment-form-dialog.component.ts`
- `src/app/modules/hr/payments/generate-payment-period-dialog.component.ts`

### Modified files
- `src/app/core/models/index.ts` — add HR interfaces and enums
- `src/app/core/services/api.service.ts` — add HR API methods
- `src/app/app.routes.ts` — add 3 HR routes
- `src/app/shared/components/layout/layout.component.ts` — add "RH" nav section

---

## Task 1: HR Models

**Files:**
- Modify: `src/app/core/models/index.ts`

- [ ] **Step 1: Add HR enums and interfaces at the bottom of `index.ts`**

```typescript
// ─── HR Module ───────────────────────────────────────────────────────────────

export enum ContractType {
  CLT = 1,
  Temporary = 2,
  Daily = 3
}

export enum HrPaymentMethod {
  Cash = 1,
  Pix = 2,
  BankTransfer = 3,
  Check = 4
}

export enum HrPaymentStatus {
  Pending = 1,
  Paid = 2,
  Cancelled = 3,
  Overdue = 4
}

export interface Employee {
  id: string;
  tenantId: string;
  fullName: string;
  cpf: string;
  phone?: string | null;
  email?: string | null;
  hireDate: string;
  terminationDate?: string | null;
  isActive: boolean;
  contractType: ContractType;
  contractTypeName: string;
  hourlyRate: number;
  preferredPaymentMethod: HrPaymentMethod;
  preferredPaymentMethodName: string;
  pixKey?: string | null;
  bankName?: string | null;
  bankAccountNumber?: string | null;
  bankAgency?: string | null;
  hasSignedContract: boolean;
  contractSignedDate?: string | null;
  applicationUserId?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateEmployeeRequest {
  fullName: string;
  cpf: string;
  phone?: string | null;
  email?: string | null;
  hireDate: string;
  contractType: ContractType;
  hourlyRate: number;
  preferredPaymentMethod: HrPaymentMethod;
  pixKey?: string | null;
  bankName?: string | null;
  bankAccountNumber?: string | null;
  bankAgency?: string | null;
  hasSignedContract: boolean;
  applicationUserId?: string | null;
}

export interface UpdateEmployeeRequest {
  fullName: string;
  phone?: string | null;
  email?: string | null;
  isActive: boolean;
  contractType: ContractType;
  hourlyRate: number;
  preferredPaymentMethod: HrPaymentMethod;
  pixKey?: string | null;
  bankName?: string | null;
  bankAccountNumber?: string | null;
  bankAgency?: string | null;
  hasSignedContract: boolean;
  terminationDate?: string | null;
}

export interface WorkLog {
  id: string;
  employeeId: string;
  employeeName: string;
  workDate: string;
  hoursWorked: number;
  hourlyRateAtTime: number;
  totalAmount: number;
  notes?: string | null;
  paymentPeriodId?: string | null;
  createdAt: string;
}

export interface CreateWorkLogRequest {
  employeeId: string;
  workDate: string;
  hoursWorked: number;
  notes?: string | null;
}

export interface UpdateWorkLogRequest {
  workDate: string;
  hoursWorked: number;
  notes?: string | null;
}

export interface PaymentPeriodSummary {
  id: string;
  employeeId: string;
  employeeName: string;
  startDate: string;
  endDate: string;
  totalHours: number;
  totalAmount: number;
  status: HrPaymentStatus;
  statusName: string;
  createdAt: string;
}

export interface GeneratePaymentPeriodRequest {
  employeeId: string;
  startDate: string;
  endDate: string;
}

export interface HrPayment {
  id: string;
  employeeId: string;
  employeeName: string;
  paymentPeriodId: string;
  paymentDate: string;
  amount: number;
  paymentMethod: HrPaymentMethod;
  paymentMethodName: string;
  paymentProof?: string | null;
  notes?: string | null;
  paidByUserId: string;
  paidByUserName: string;
  createdAt: string;
}

export interface CreateHrPaymentRequest {
  paymentPeriodId: string;
  paymentDate: string;
  paymentMethod: HrPaymentMethod;
  paymentProof?: string | null;
  notes?: string | null;
}
```

- [ ] **Step 2: Verify TypeScript compiles**

```bash
cd "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" && npx tsc --noEmit
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
cd "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" && git add src/app/core/models/index.ts && git commit -m "feat(hr): add HR models and enums"
```

---

## Task 2: HR API Methods

**Files:**
- Modify: `src/app/core/services/api.service.ts`

- [ ] **Step 1: Add HR API helper and methods after the existing General Ledger section**

Open `api.service.ts`. After the last GL method, add:

```typescript
  // ─── HR Module ────────────────────────────────────────────────────────────

  private hrUrl(tenantId: string): string {
    return `${this.apiUrl}/tenants/${encodeURIComponent(tenantId)}`;
  }

  // Employees
  getEmployees(tenantId: string, includeInactive = false): Observable<Employee[]> {
    const params = includeInactive ? { includeInactive: 'true' } : {};
    return this.http.get<Employee[]>(`${this.hrUrl(tenantId)}/employees`, { params });
  }

  getEmployee(tenantId: string, id: string): Observable<Employee> {
    return this.http.get<Employee>(`${this.hrUrl(tenantId)}/employees/${encodeURIComponent(id)}`);
  }

  createEmployee(tenantId: string, data: CreateEmployeeRequest): Observable<Employee> {
    return this.http.post<Employee>(`${this.hrUrl(tenantId)}/employees`, data);
  }

  updateEmployee(tenantId: string, id: string, data: UpdateEmployeeRequest): Observable<Employee> {
    return this.http.put<Employee>(`${this.hrUrl(tenantId)}/employees/${encodeURIComponent(id)}`, data);
  }

  deactivateEmployee(tenantId: string, id: string): Observable<void> {
    return this.http.delete<void>(`${this.hrUrl(tenantId)}/employees/${encodeURIComponent(id)}`);
  }

  // WorkLogs
  getWorkLogs(tenantId: string, employeeId: string, from?: string, to?: string, onlyUnassigned = false): Observable<WorkLog[]> {
    let params: any = {};
    if (from) params['from'] = from;
    if (to) params['to'] = to;
    if (onlyUnassigned) params['onlyUnassigned'] = 'true';
    return this.http.get<WorkLog[]>(
      `${this.hrUrl(tenantId)}/employees/${encodeURIComponent(employeeId)}/worklogs`,
      { params }
    );
  }

  createWorkLog(tenantId: string, data: CreateWorkLogRequest): Observable<WorkLog> {
    return this.http.post<WorkLog>(
      `${this.hrUrl(tenantId)}/employees/${encodeURIComponent(data.employeeId)}/worklogs`,
      data
    );
  }

  updateWorkLog(tenantId: string, employeeId: string, id: string, data: UpdateWorkLogRequest): Observable<WorkLog> {
    return this.http.put<WorkLog>(
      `${this.hrUrl(tenantId)}/employees/${encodeURIComponent(employeeId)}/worklogs/${encodeURIComponent(id)}`,
      data
    );
  }

  deleteWorkLog(tenantId: string, employeeId: string, id: string): Observable<void> {
    return this.http.delete<void>(
      `${this.hrUrl(tenantId)}/employees/${encodeURIComponent(employeeId)}/worklogs/${encodeURIComponent(id)}`
    );
  }

  // Payment Periods
  getPaymentPeriods(tenantId: string, employeeId: string): Observable<PaymentPeriodSummary[]> {
    return this.http.get<PaymentPeriodSummary[]>(
      `${this.hrUrl(tenantId)}/employees/${encodeURIComponent(employeeId)}/payment-periods`
    );
  }

  generatePaymentPeriod(tenantId: string, data: GeneratePaymentPeriodRequest): Observable<PaymentPeriodSummary> {
    return this.http.post<PaymentPeriodSummary>(
      `${this.hrUrl(tenantId)}/employees/${encodeURIComponent(data.employeeId)}/payment-periods/generate`,
      data
    );
  }

  deletePaymentPeriod(tenantId: string, employeeId: string, id: string): Observable<void> {
    return this.http.delete<void>(
      `${this.hrUrl(tenantId)}/employees/${encodeURIComponent(employeeId)}/payment-periods/${encodeURIComponent(id)}`
    );
  }

  // HR Payments
  getRecentHrPayments(tenantId: string, count = 50): Observable<HrPayment[]> {
    return this.http.get<HrPayment[]>(`${this.hrUrl(tenantId)}/payments/recent`, { params: { count: String(count) } });
  }

  getHrPaymentsByEmployee(tenantId: string, employeeId: string): Observable<HrPayment[]> {
    return this.http.get<HrPayment[]>(
      `${this.hrUrl(tenantId)}/payments/employee/${encodeURIComponent(employeeId)}`
    );
  }

  createHrPayment(tenantId: string, data: CreateHrPaymentRequest): Observable<HrPayment> {
    return this.http.post<HrPayment>(`${this.hrUrl(tenantId)}/payments`, data);
  }

  deleteHrPayment(tenantId: string, id: string): Observable<void> {
    return this.http.delete<void>(`${this.hrUrl(tenantId)}/payments/${encodeURIComponent(id)}`);
  }
```

Also add the new HR model types to the existing `import` block at the top of `api.service.ts`:

```typescript
  Employee,
  CreateEmployeeRequest,
  UpdateEmployeeRequest,
  WorkLog,
  CreateWorkLogRequest,
  UpdateWorkLogRequest,
  PaymentPeriodSummary,
  GeneratePaymentPeriodRequest,
  HrPayment,
  CreateHrPaymentRequest,
```

- [ ] **Step 2: Verify TypeScript compiles**

```bash
cd "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" && npx tsc --noEmit
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
cd "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" && git add src/app/core/services/api.service.ts && git commit -m "feat(hr): add HR API methods to ApiService"
```

---

## Task 3: Employees Component + Form Dialog

**Files:**
- Create: `src/app/modules/hr/employees/employees.component.ts`
- Create: `src/app/modules/hr/employees/employee-form-dialog.component.ts`

- [ ] **Step 1: Create `src/app/modules/hr/employees/employee-form-dialog.component.ts`**

```typescript
import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule, MAT_DATE_LOCALE } from '@angular/material/core';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService, AuthService } from '@core/services';
import { Employee, ContractType, HrPaymentMethod, CreateEmployeeRequest, UpdateEmployeeRequest } from '@core/models';

@Component({
  selector: 'app-employee-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatButtonModule, MatDatepickerModule, MatNativeDateModule,
    MatSlideToggleModule, MatSelectModule, MatProgressSpinnerModule
  ],
  providers: [{ provide: MAT_DATE_LOCALE, useValue: 'pt-BR' }],
  template: `
    <h2 mat-dialog-title>{{ isEditing ? 'Editar Funcionário' : 'Novo Funcionário' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="employee-form">
        <div class="form-row">
          <mat-form-field appearance="outline">
            <mat-label>Nome Completo *</mat-label>
            <input matInput formControlName="fullName">
            <mat-error *ngIf="form.get('fullName')?.invalid">Nome é obrigatório</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>CPF *</mat-label>
            <input matInput formControlName="cpf" placeholder="000.000.000-00" [readonly]="isEditing">
            <mat-error *ngIf="form.get('cpf')?.invalid">CPF é obrigatório</mat-error>
          </mat-form-field>
        </div>
        <div class="form-row">
          <mat-form-field appearance="outline">
            <mat-label>Email</mat-label>
            <input matInput formControlName="email" type="email">
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Telefone</mat-label>
            <input matInput formControlName="phone" placeholder="(11) 99999-9999">
          </mat-form-field>
        </div>
        <div class="form-row">
          <mat-form-field appearance="outline">
            <mat-label>Data de Admissão *</mat-label>
            <input matInput formControlName="hireDate" [matDatepicker]="picker">
            <mat-datepicker-toggle matIconSuffix [for]="picker"></mat-datepicker-toggle>
            <mat-datepicker #picker></mat-datepicker>
            <mat-error *ngIf="form.get('hireDate')?.invalid">Data de admissão é obrigatória</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Tipo de Contrato *</mat-label>
            <mat-select formControlName="contractType">
              <mat-option [value]="ContractType.CLT">CLT</mat-option>
              <mat-option [value]="ContractType.Temporary">Temporário</mat-option>
              <mat-option [value]="ContractType.Daily">Diária</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
        <div class="form-row">
          <mat-form-field appearance="outline">
            <mat-label>Taxa Horária (R$) *</mat-label>
            <input matInput formControlName="hourlyRate" type="number" step="0.01">
            <mat-error *ngIf="form.get('hourlyRate')?.invalid">Taxa horária deve ser maior que zero</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Forma de Pagamento *</mat-label>
            <mat-select formControlName="preferredPaymentMethod">
              <mat-option [value]="HrPaymentMethod.BankTransfer">Transferência Bancária</mat-option>
              <mat-option [value]="HrPaymentMethod.Pix">PIX</mat-option>
              <mat-option [value]="HrPaymentMethod.Cash">Dinheiro</mat-option>
              <mat-option [value]="HrPaymentMethod.Check">Cheque</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
        <div *ngIf="form.get('preferredPaymentMethod')?.value === HrPaymentMethod.Pix">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Chave PIX</mat-label>
            <input matInput formControlName="pixKey">
          </mat-form-field>
        </div>
        <div class="form-row" *ngIf="form.get('preferredPaymentMethod')?.value === HrPaymentMethod.BankTransfer">
          <mat-form-field appearance="outline">
            <mat-label>Banco</mat-label>
            <input matInput formControlName="bankName">
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Agência</mat-label>
            <input matInput formControlName="bankAgency">
          </mat-form-field>
        </div>
        <div *ngIf="form.get('preferredPaymentMethod')?.value === HrPaymentMethod.BankTransfer">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Conta</mat-label>
            <input matInput formControlName="bankAccountNumber">
          </mat-form-field>
        </div>
        <div class="toggles">
          <mat-slide-toggle formControlName="hasSignedContract">Contrato Assinado</mat-slide-toggle>
          <mat-slide-toggle *ngIf="isEditing" formControlName="isActive">Ativo</mat-slide-toggle>
        </div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()">Cancelar</button>
      <button mat-raised-button color="primary" (click)="onSave()" [disabled]="form.invalid || isSaving">
        {{ isSaving ? 'Salvando...' : 'Salvar' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .employee-form { display: flex; flex-direction: column; gap: 12px; min-width: 520px; }
    .form-row { display: flex; gap: 16px; }
    .form-row mat-form-field { flex: 1; }
    .full-width { width: 100%; }
    .toggles { display: flex; gap: 24px; padding: 8px 0; }
  `]
})
export class EmployeeFormDialogComponent implements OnInit {
  form!: FormGroup;
  isEditing = false;
  isSaving = false;
  ContractType = ContractType;
  HrPaymentMethod = HrPaymentMethod;

  private tenantId: string | null = null;

  constructor(
    private fb: FormBuilder,
    private api: ApiService,
    private auth: AuthService,
    public dialogRef: MatDialogRef<EmployeeFormDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: Employee | null
  ) {}

  ngOnInit(): void {
    this.tenantId = this.auth.getCurrentTenantId();
    this.isEditing = !!this.data;
    this.form = this.fb.group({
      fullName: [this.data?.fullName ?? '', Validators.required],
      cpf: [{ value: this.data?.cpf ?? '', disabled: this.isEditing }, Validators.required],
      email: [this.data?.email ?? ''],
      phone: [this.data?.phone ?? ''],
      hireDate: [this.data ? new Date(this.data.hireDate) : new Date(), Validators.required],
      contractType: [this.data?.contractType ?? ContractType.CLT, Validators.required],
      hourlyRate: [this.data?.hourlyRate ?? 0, [Validators.required, Validators.min(0.01)]],
      preferredPaymentMethod: [this.data?.preferredPaymentMethod ?? HrPaymentMethod.BankTransfer, Validators.required],
      pixKey: [this.data?.pixKey ?? ''],
      bankName: [this.data?.bankName ?? ''],
      bankAgency: [this.data?.bankAgency ?? ''],
      bankAccountNumber: [this.data?.bankAccountNumber ?? ''],
      hasSignedContract: [this.data?.hasSignedContract ?? false],
      isActive: [this.data?.isActive ?? true]
    });
  }

  onSave(): void {
    if (this.form.invalid || !this.tenantId) return;
    this.isSaving = true;
    const v = this.form.getRawValue();

    if (this.isEditing) {
      const req: UpdateEmployeeRequest = {
        fullName: v.fullName,
        email: v.email || null,
        phone: v.phone || null,
        isActive: v.isActive,
        contractType: v.contractType,
        hourlyRate: v.hourlyRate,
        preferredPaymentMethod: v.preferredPaymentMethod,
        pixKey: v.pixKey || null,
        bankName: v.bankName || null,
        bankAgency: v.bankAgency || null,
        bankAccountNumber: v.bankAccountNumber || null,
        hasSignedContract: v.hasSignedContract,
        terminationDate: null
      };
      this.api.updateEmployee(this.tenantId, this.data!.id, req).subscribe({
        next: (result) => { this.isSaving = false; this.dialogRef.close(result); },
        error: () => { this.isSaving = false; alert('Erro ao atualizar funcionário.'); }
      });
    } else {
      const req: CreateEmployeeRequest = {
        fullName: v.fullName,
        cpf: v.cpf,
        email: v.email || null,
        phone: v.phone || null,
        hireDate: new Date(v.hireDate).toISOString(),
        contractType: v.contractType,
        hourlyRate: v.hourlyRate,
        preferredPaymentMethod: v.preferredPaymentMethod,
        pixKey: v.pixKey || null,
        bankName: v.bankName || null,
        bankAgency: v.bankAgency || null,
        bankAccountNumber: v.bankAccountNumber || null,
        hasSignedContract: v.hasSignedContract,
        applicationUserId: null
      };
      this.api.createEmployee(this.tenantId, req).subscribe({
        next: (result) => { this.isSaving = false; this.dialogRef.close(result); },
        error: () => { this.isSaving = false; alert('Erro ao criar funcionário. Verifique se o CPF já está cadastrado.'); }
      });
    }
  }

  onCancel(): void { this.dialogRef.close(); }
}
```

- [ ] **Step 2: Create `src/app/modules/hr/employees/employees.component.ts`**

```typescript
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatChipsModule } from '@angular/material/chips';
import { ApiService, AuthService } from '@core/services';
import { Employee } from '@core/models';
import { EmployeeFormDialogComponent } from './employee-form-dialog.component';

@Component({
  selector: 'app-employees',
  standalone: true,
  imports: [
    CommonModule, MatTableModule, MatCardModule, MatButtonModule, MatIconModule,
    MatProgressSpinnerModule, MatDialogModule, MatChipsModule
  ],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h1>Funcionários</h1>
          <p class="subtitle">Gestão de funcionários e contratos</p>
        </div>
        <button mat-raised-button color="primary" (click)="onNew()">
          <mat-icon>add</mat-icon> Novo Funcionário
        </button>
      </div>

      <mat-card>
        <mat-card-content>
          <div *ngIf="isLoading" class="spinner-wrap">
            <mat-spinner diameter="48"></mat-spinner>
          </div>
          <p *ngIf="!isLoading && employees.length === 0" class="no-data">Nenhum funcionário cadastrado</p>
          <table mat-table [dataSource]="employees" *ngIf="!isLoading && employees.length > 0" class="full-table">
            <ng-container matColumnDef="fullName">
              <th mat-header-cell *matHeaderCellDef>Nome</th>
              <td mat-cell *matCellDef="let e">{{ e.fullName }}</td>
            </ng-container>
            <ng-container matColumnDef="cpf">
              <th mat-header-cell *matHeaderCellDef>CPF</th>
              <td mat-cell *matCellDef="let e">{{ e.cpf }}</td>
            </ng-container>
            <ng-container matColumnDef="contractType">
              <th mat-header-cell *matHeaderCellDef>Contrato</th>
              <td mat-cell *matCellDef="let e">{{ e.contractTypeName }}</td>
            </ng-container>
            <ng-container matColumnDef="hourlyRate">
              <th mat-header-cell *matHeaderCellDef>Taxa/h</th>
              <td mat-cell *matCellDef="let e">R$ {{ e.hourlyRate | number:'1.2-2' }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let e">
                <mat-chip [color]="e.isActive ? 'primary' : 'warn'" highlighted>
                  {{ e.isActive ? 'Ativo' : 'Inativo' }}
                </mat-chip>
              </td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let e">
                <button mat-icon-button color="primary" (click)="onEdit(e)" title="Editar">
                  <mat-icon>edit</mat-icon>
                </button>
                <button mat-icon-button color="warn" (click)="onDeactivate(e)" title="Desativar"
                  [disabled]="!e.isActive">
                  <mat-icon>person_off</mat-icon>
                </button>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns;"></tr>
          </table>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .page-container { padding: 24px; max-width: 1200px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    h1 { margin: 0; font-size: 24px; font-weight: 600; color: var(--color-text); }
    .subtitle { margin: 4px 0 0; color: var(--color-text-muted); font-size: 14px; }
    .full-table { width: 100%; }
    .spinner-wrap { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; color: var(--color-text-muted); padding: 48px; }
  `]
})
export class EmployeesComponent implements OnInit {
  employees: Employee[] = [];
  isLoading = false;
  columns = ['fullName', 'cpf', 'contractType', 'hourlyRate', 'status', 'actions'];

  private tenantId: string | null = null;

  constructor(
    private api: ApiService,
    private auth: AuthService,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.tenantId = this.auth.getCurrentTenantId();
    if (!this.tenantId) return;
    this.load();
  }

  private load(): void {
    if (!this.tenantId) return;
    this.isLoading = true;
    this.api.getEmployees(this.tenantId, true).subscribe({
      next: (data) => { this.employees = data; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  onNew(): void {
    this.dialog.open(EmployeeFormDialogComponent, { width: '600px', data: null })
      .afterClosed().subscribe(result => { if (result) this.load(); });
  }

  onEdit(employee: Employee): void {
    this.dialog.open(EmployeeFormDialogComponent, { width: '600px', data: employee })
      .afterClosed().subscribe(result => { if (result) this.load(); });
  }

  onDeactivate(employee: Employee): void {
    if (!this.tenantId || !confirm(`Desativar ${employee.fullName}?`)) return;
    this.api.deactivateEmployee(this.tenantId, employee.id).subscribe({
      next: () => this.load(),
      error: () => alert('Erro ao desativar funcionário.')
    });
  }
}
```

- [ ] **Step 3: Verify TypeScript compiles**

```bash
cd "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" && npx tsc --noEmit
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
cd "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" && git add src/app/modules/hr/employees/ && git commit -m "feat(hr): add EmployeesComponent and EmployeeFormDialog"
```

---

## Task 4: WorkLogs Component + Form Dialog

**Files:**
- Create: `src/app/modules/hr/worklogs/worklog-form-dialog.component.ts`
- Create: `src/app/modules/hr/worklogs/worklogs.component.ts`

- [ ] **Step 1: Create `src/app/modules/hr/worklogs/worklog-form-dialog.component.ts`**

```typescript
import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule, MAT_DATE_LOCALE } from '@angular/material/core';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService, AuthService } from '@core/services';
import { Employee, WorkLog, CreateWorkLogRequest, UpdateWorkLogRequest } from '@core/models';

@Component({
  selector: 'app-worklog-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatButtonModule, MatDatepickerModule, MatNativeDateModule,
    MatSelectModule, MatProgressSpinnerModule
  ],
  providers: [{ provide: MAT_DATE_LOCALE, useValue: 'pt-BR' }],
  template: `
    <h2 mat-dialog-title>{{ isEditing ? 'Editar Registro' : 'Registrar Horas Trabalhadas' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="worklog-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Funcionário *</mat-label>
          <mat-select formControlName="employeeId" (selectionChange)="onEmployeeChange()">
            <mat-option *ngFor="let e of employees" [value]="e.id">
              {{ e.fullName }} — R$ {{ e.hourlyRate | number:'1.2-2' }}/h
            </mat-option>
          </mat-select>
          <mat-error>Selecione um funcionário</mat-error>
        </mat-form-field>

        <div class="info-box" *ngIf="selectedEmployee">
          <span>Taxa horária: <strong>R$ {{ selectedEmployee.hourlyRate | number:'1.2-2' }}</strong></span>
          <span *ngIf="calculatedAmount > 0">Valor estimado: <strong class="amount">R$ {{ calculatedAmount | number:'1.2-2' }}</strong></span>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Data *</mat-label>
          <input matInput formControlName="workDate" [matDatepicker]="picker">
          <mat-datepicker-toggle matIconSuffix [for]="picker"></mat-datepicker-toggle>
          <mat-datepicker #picker></mat-datepicker>
          <mat-error>Data é obrigatória</mat-error>
        </mat-form-field>

        <div class="form-row">
          <mat-form-field appearance="outline">
            <mat-label>Entrada *</mat-label>
            <input matInput formControlName="clockIn" type="time">
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Saída *</mat-label>
            <input matInput formControlName="clockOut" type="time">
          </mat-form-field>
        </div>

        <div class="summary-box" *ngIf="calculatedAmount > 0">
          <span>{{ form.get('clockIn')?.value }} → {{ form.get('clockOut')?.value }}</span>
          <span>×</span>
          <span>R$ {{ selectedEmployee?.hourlyRate | number:'1.2-2' }}/h</span>
          <span>=</span>
          <span class="total">R$ {{ calculatedAmount | number:'1.2-2' }}</span>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Observações</mat-label>
          <textarea matInput formControlName="notes" rows="2"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()">Cancelar</button>
      <button mat-raised-button color="primary" (click)="onSave()" [disabled]="form.invalid || isSaving">
        {{ isSaving ? 'Salvando...' : (isEditing ? 'Atualizar' : 'Registrar') }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .worklog-form { display: flex; flex-direction: column; gap: 14px; min-width: 480px; }
    .full-width { width: 100%; }
    .form-row { display: flex; gap: 16px; }
    .form-row mat-form-field { flex: 1; }
    .info-box {
      display: flex; justify-content: space-between; padding: 10px 14px;
      background: #e3f2fd; border-radius: 6px; font-size: 14px;
    }
    .amount { color: #2e7d32; }
    .summary-box {
      display: flex; align-items: center; justify-content: space-around;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white; padding: 14px; border-radius: 8px; font-weight: 600;
    }
    .total { font-size: 20px; }
  `]
})
export class WorklogFormDialogComponent implements OnInit {
  form!: FormGroup;
  isEditing = false;
  isSaving = false;
  employees: Employee[] = [];
  selectedEmployee: Employee | null = null;
  calculatedAmount = 0;

  private tenantId: string | null = null;

  constructor(
    private fb: FormBuilder,
    private api: ApiService,
    private auth: AuthService,
    public dialogRef: MatDialogRef<WorklogFormDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: WorkLog | null
  ) {}

  ngOnInit(): void {
    this.tenantId = this.auth.getCurrentTenantId();
    this.isEditing = !!this.data;

    this.form = this.fb.group({
      employeeId: [this.data?.employeeId ?? '', Validators.required],
      workDate: [this.data ? new Date(this.data.workDate) : new Date(), Validators.required],
      clockIn: ['08:00', Validators.required],
      clockOut: ['17:00', Validators.required],
      notes: [this.data?.notes ?? '']
    });

    this.form.get('clockIn')?.valueChanges.subscribe(() => this.calculateAmount());
    this.form.get('clockOut')?.valueChanges.subscribe(() => this.calculateAmount());

    if (this.tenantId) {
      this.api.getEmployees(this.tenantId).subscribe({
        next: (data) => {
          this.employees = data;
          if (this.data) {
            this.selectedEmployee = data.find(e => e.id === this.data!.employeeId) ?? null;
            this.calculateAmount();
          }
        }
      });
    }
  }

  onEmployeeChange(): void {
    const id = this.form.get('employeeId')?.value;
    this.selectedEmployee = this.employees.find(e => e.id === id) ?? null;
    this.calculateAmount();
  }

  calculateAmount(): void {
    const clockIn = this.form.get('clockIn')?.value as string;
    const clockOut = this.form.get('clockOut')?.value as string;
    if (!clockIn || !clockOut || !this.selectedEmployee) { this.calculatedAmount = 0; return; }
    const [inH, inM] = clockIn.split(':').map(Number);
    const [outH, outM] = clockOut.split(':').map(Number);
    const hours = ((outH * 60 + outM) - (inH * 60 + inM)) / 60;
    this.calculatedAmount = hours > 0 ? Math.round(hours * this.selectedEmployee.hourlyRate * 100) / 100 : 0;
  }

  private getHoursWorked(): number {
    const clockIn = this.form.get('clockIn')?.value as string;
    const clockOut = this.form.get('clockOut')?.value as string;
    const [inH, inM] = clockIn.split(':').map(Number);
    const [outH, outM] = clockOut.split(':').map(Number);
    return ((outH * 60 + outM) - (inH * 60 + inM)) / 60;
  }

  onSave(): void {
    if (this.form.invalid || !this.tenantId || !this.selectedEmployee) return;
    const hoursWorked = this.getHoursWorked();
    if (hoursWorked <= 0) { alert('Horário de saída deve ser após o de entrada.'); return; }
    this.isSaving = true;
    const v = this.form.value;
    const workDate = new Date(v.workDate).toISOString().split('T')[0];

    if (this.isEditing) {
      const req: UpdateWorkLogRequest = { workDate, hoursWorked, notes: v.notes || null };
      this.api.updateWorkLog(this.tenantId, this.data!.employeeId, this.data!.id, req).subscribe({
        next: (r) => { this.isSaving = false; this.dialogRef.close(r); },
        error: (err) => { this.isSaving = false; alert(err.error?.message ?? 'Erro ao atualizar.'); }
      });
    } else {
      const req: CreateWorkLogRequest = { employeeId: v.employeeId, workDate, hoursWorked, notes: v.notes || null };
      this.api.createWorkLog(this.tenantId, req).subscribe({
        next: (r) => { this.isSaving = false; this.dialogRef.close(r); },
        error: (err) => { this.isSaving = false; alert(err.error?.message ?? 'Erro ao registrar horas.'); }
      });
    }
  }

  onCancel(): void { this.dialogRef.close(); }
}
```

- [ ] **Step 2: Create `src/app/modules/hr/worklogs/worklogs.component.ts`**

```typescript
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { ApiService, AuthService } from '@core/services';
import { Employee, WorkLog } from '@core/models';
import { WorklogFormDialogComponent } from './worklog-form-dialog.component';

@Component({
  selector: 'app-worklogs',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatTableModule, MatCardModule, MatButtonModule,
    MatIconModule, MatProgressSpinnerModule, MatDialogModule,
    MatSelectModule, MatFormFieldModule
  ],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h1>Horas Trabalhadas</h1>
          <p class="subtitle">Registros de horas por funcionário</p>
        </div>
        <button mat-raised-button color="primary" (click)="onNew()">
          <mat-icon>add</mat-icon> Registrar Horas
        </button>
      </div>

      <mat-card class="filter-card">
        <mat-card-content>
          <mat-form-field appearance="outline">
            <mat-label>Funcionário</mat-label>
            <mat-select [(ngModel)]="selectedEmployeeId" (ngModelChange)="onFilterChange()">
              <mat-option value="">Todos</mat-option>
              <mat-option *ngFor="let e of employees" [value]="e.id">{{ e.fullName }}</mat-option>
            </mat-select>
          </mat-form-field>
        </mat-card-content>
      </mat-card>

      <mat-card>
        <mat-card-content>
          <div *ngIf="isLoading" class="spinner-wrap"><mat-spinner diameter="48"></mat-spinner></div>
          <p *ngIf="!isLoading && workLogs.length === 0" class="no-data">Nenhum registro encontrado</p>
          <table mat-table [dataSource]="workLogs" *ngIf="!isLoading && workLogs.length > 0" class="full-table">
            <ng-container matColumnDef="employeeName">
              <th mat-header-cell *matHeaderCellDef>Funcionário</th>
              <td mat-cell *matCellDef="let w">{{ w.employeeName }}</td>
            </ng-container>
            <ng-container matColumnDef="workDate">
              <th mat-header-cell *matHeaderCellDef>Data</th>
              <td mat-cell *matCellDef="let w">{{ w.workDate | date:'dd/MM/yyyy' }}</td>
            </ng-container>
            <ng-container matColumnDef="hoursWorked">
              <th mat-header-cell *matHeaderCellDef>Horas</th>
              <td mat-cell *matCellDef="let w">{{ w.hoursWorked | number:'1.1-2' }}h</td>
            </ng-container>
            <ng-container matColumnDef="totalAmount">
              <th mat-header-cell *matHeaderCellDef>Valor</th>
              <td mat-cell *matCellDef="let w">R$ {{ w.totalAmount | number:'1.2-2' }}</td>
            </ng-container>
            <ng-container matColumnDef="period">
              <th mat-header-cell *matHeaderCellDef>Período</th>
              <td mat-cell *matCellDef="let w">
                <span [class.assigned]="w.paymentPeriodId">
                  {{ w.paymentPeriodId ? 'Atribuído' : 'Livre' }}
                </span>
              </td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let w">
                <button mat-icon-button color="primary" (click)="onEdit(w)" title="Editar"
                  [disabled]="!!w.paymentPeriodId">
                  <mat-icon>edit</mat-icon>
                </button>
                <button mat-icon-button color="warn" (click)="onDelete(w)" title="Excluir"
                  [disabled]="!!w.paymentPeriodId">
                  <mat-icon>delete</mat-icon>
                </button>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns;"></tr>
          </table>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .page-container { padding: 24px; max-width: 1200px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    h1 { margin: 0; font-size: 24px; font-weight: 600; color: var(--color-text); }
    .subtitle { margin: 4px 0 0; color: var(--color-text-muted); font-size: 14px; }
    .filter-card { margin-bottom: 16px; }
    .filter-card mat-card-content { display: flex; gap: 16px; padding: 16px; }
    .full-table { width: 100%; }
    .spinner-wrap { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; color: var(--color-text-muted); padding: 48px; }
    .assigned { color: #2e7d32; font-weight: 500; font-size: 12px; }
  `]
})
export class WorklogsComponent implements OnInit {
  employees: Employee[] = [];
  workLogs: WorkLog[] = [];
  isLoading = false;
  selectedEmployeeId = '';
  columns = ['employeeName', 'workDate', 'hoursWorked', 'totalAmount', 'period', 'actions'];

  private tenantId: string | null = null;

  constructor(private api: ApiService, private auth: AuthService, private dialog: MatDialog) {}

  ngOnInit(): void {
    this.tenantId = this.auth.getCurrentTenantId();
    if (!this.tenantId) return;
    this.api.getEmployees(this.tenantId).subscribe({ next: (data) => { this.employees = data; this.load(); } });
  }

  private load(): void {
    if (!this.tenantId) return;
    if (this.selectedEmployeeId) {
      this.isLoading = true;
      this.api.getWorkLogs(this.tenantId, this.selectedEmployeeId).subscribe({
        next: (data) => { this.workLogs = data; this.isLoading = false; },
        error: () => this.isLoading = false
      });
    } else {
      this.loadAllEmployees();
    }
  }

  private loadAllEmployees(): void {
    if (!this.tenantId || this.employees.length === 0) { this.workLogs = []; return; }
    this.isLoading = true;
    const tenantId = this.tenantId;
    let completed = 0;
    const all: WorkLog[] = [];
    this.employees.forEach(emp => {
      this.api.getWorkLogs(tenantId, emp.id).subscribe({
        next: (data) => {
          all.push(...data);
          if (++completed === this.employees.length) {
            this.workLogs = all.sort((a, b) => new Date(b.workDate).getTime() - new Date(a.workDate).getTime());
            this.isLoading = false;
          }
        },
        error: () => { if (++completed === this.employees.length) { this.workLogs = all; this.isLoading = false; } }
      });
    });
  }

  onFilterChange(): void { this.load(); }

  onNew(): void {
    this.dialog.open(WorklogFormDialogComponent, { width: '560px', data: null })
      .afterClosed().subscribe(result => { if (result) this.load(); });
  }

  onEdit(w: WorkLog): void {
    this.dialog.open(WorklogFormDialogComponent, { width: '560px', data: w })
      .afterClosed().subscribe(result => { if (result) this.load(); });
  }

  onDelete(w: WorkLog): void {
    if (!this.tenantId || !confirm('Excluir este registro de horas?')) return;
    this.api.deleteWorkLog(this.tenantId, w.employeeId, w.id).subscribe({
      next: () => this.load(),
      error: (err) => alert(err.error?.message ?? 'Erro ao excluir registro.')
    });
  }
}
```

- [ ] **Step 3: Verify TypeScript compiles**

```bash
cd "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" && npx tsc --noEmit
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
cd "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" && git add src/app/modules/hr/worklogs/ && git commit -m "feat(hr): add WorklogsComponent and WorklogFormDialog"
```

---

## Task 5: Payments Component + Dialogs

**Files:**
- Create: `src/app/modules/hr/payments/generate-payment-period-dialog.component.ts`
- Create: `src/app/modules/hr/payments/payment-form-dialog.component.ts`
- Create: `src/app/modules/hr/payments/payments.component.ts`

- [ ] **Step 1: Create `src/app/modules/hr/payments/generate-payment-period-dialog.component.ts`**

```typescript
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule, MAT_DATE_LOCALE } from '@angular/material/core';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { ApiService, AuthService } from '@core/services';
import { Employee, GeneratePaymentPeriodRequest, PaymentPeriodSummary } from '@core/models';

@Component({
  selector: 'app-generate-payment-period-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatButtonModule, MatDatepickerModule, MatNativeDateModule,
    MatSelectModule, MatProgressSpinnerModule, MatIconModule
  ],
  providers: [{ provide: MAT_DATE_LOCALE, useValue: 'pt-BR' }],
  template: `
    <h2 mat-dialog-title>
      <mat-icon style="vertical-align:middle;margin-right:8px">event_note</mat-icon>
      Gerar Período de Pagamento
    </h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="gen-form">
        <p class="hint">Selecione o funcionário e o período. O sistema irá agrupar os registros de horas não atribuídos.</p>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Funcionário *</mat-label>
          <mat-select formControlName="employeeId" (selectionChange)="onEmployeeChange()">
            <mat-option *ngFor="let e of employees" [value]="e.id">{{ e.fullName }}</mat-option>
          </mat-select>
          <mat-error>Selecione um funcionário</mat-error>
        </mat-form-field>
        <div class="date-row">
          <mat-form-field appearance="outline">
            <mat-label>Data Inicial *</mat-label>
            <input matInput [matDatepicker]="startPicker" formControlName="startDate" readonly>
            <mat-datepicker-toggle matIconSuffix [for]="startPicker"></mat-datepicker-toggle>
            <mat-datepicker #startPicker></mat-datepicker>
            <mat-error>Data inicial é obrigatória</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Data Final *</mat-label>
            <input matInput [matDatepicker]="endPicker" formControlName="endDate" readonly>
            <mat-datepicker-toggle matIconSuffix [for]="endPicker"></mat-datepicker-toggle>
            <mat-datepicker #endPicker></mat-datepicker>
            <mat-error>Data final é obrigatória</mat-error>
          </mat-form-field>
        </div>
        <div class="employee-summary" *ngIf="selectedEmployee">
          <div><strong>{{ selectedEmployee.fullName }}</strong></div>
          <div>CPF: {{ selectedEmployee.cpf }}</div>
          <div>Taxa: R$ {{ selectedEmployee.hourlyRate | number:'1.2-2' }}/h</div>
        </div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()" [disabled]="isGenerating">Cancelar</button>
      <button mat-raised-button color="primary" (click)="onGenerate()"
        [disabled]="form.invalid || isGenerating || form.hasError('dateRange')">
        <mat-spinner diameter="18" *ngIf="isGenerating" style="display:inline-block;margin-right:6px"></mat-spinner>
        {{ isGenerating ? 'Gerando...' : 'Gerar Período' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .gen-form { display: flex; flex-direction: column; gap: 14px; min-width: 480px; }
    .full-width { width: 100%; }
    .date-row { display: flex; gap: 16px; }
    .date-row mat-form-field { flex: 1; }
    .hint { margin: 0; color: var(--color-text-muted); font-size: 13px; }
    .employee-summary {
      background: #f5f5f5; border-left: 4px solid var(--color-primary);
      padding: 12px 16px; border-radius: 4px; font-size: 13px; display: flex; flex-direction: column; gap: 4px;
    }
  `]
})
export class GeneratePaymentPeriodDialogComponent implements OnInit {
  form!: FormGroup;
  employees: Employee[] = [];
  selectedEmployee: Employee | null = null;
  isGenerating = false;

  private tenantId: string | null = null;

  constructor(
    private fb: FormBuilder,
    private api: ApiService,
    private auth: AuthService,
    private dialogRef: MatDialogRef<GeneratePaymentPeriodDialogComponent>
  ) {}

  ngOnInit(): void {
    this.tenantId = this.auth.getCurrentTenantId();
    this.form = this.fb.group({
      employeeId: ['', Validators.required],
      startDate: [null, Validators.required],
      endDate: [null, Validators.required]
    }, { validators: (g) => {
      const s = g.get('startDate')?.value, e = g.get('endDate')?.value;
      return (s && e && new Date(s) >= new Date(e)) ? { dateRange: true } : null;
    }});
    if (this.tenantId) {
      this.api.getEmployees(this.tenantId).subscribe({ next: d => this.employees = d });
    }
  }

  onEmployeeChange(): void {
    const id = this.form.get('employeeId')?.value;
    this.selectedEmployee = this.employees.find(e => e.id === id) ?? null;
  }

  onGenerate(): void {
    if (this.form.invalid || !this.tenantId) return;
    this.isGenerating = true;
    const v = this.form.value;
    const req: GeneratePaymentPeriodRequest = {
      employeeId: v.employeeId,
      startDate: new Date(v.startDate).toISOString().split('T')[0],
      endDate: new Date(v.endDate).toISOString().split('T')[0]
    };
    this.api.generatePaymentPeriod(this.tenantId, req).subscribe({
      next: (result: PaymentPeriodSummary) => {
        this.isGenerating = false;
        alert(`Período gerado!\n${this.selectedEmployee?.fullName}\n${result.totalHours}h — R$ ${result.totalAmount}`);
        this.dialogRef.close(result);
      },
      error: (err) => { this.isGenerating = false; alert(err.error?.message ?? 'Erro ao gerar período.'); }
    });
  }

  onCancel(): void { this.dialogRef.close(); }
}
```

- [ ] **Step 2: Create `src/app/modules/hr/payments/payment-form-dialog.component.ts`**

```typescript
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule, MAT_DATE_LOCALE } from '@angular/material/core';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { ApiService, AuthService } from '@core/services';
import { Employee, PaymentPeriodSummary, HrPaymentMethod, HrPaymentStatus, CreateHrPaymentRequest } from '@core/models';

@Component({
  selector: 'app-payment-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatButtonModule, MatDatepickerModule, MatNativeDateModule,
    MatSelectModule, MatProgressSpinnerModule, MatIconModule
  ],
  providers: [{ provide: MAT_DATE_LOCALE, useValue: 'pt-BR' }],
  template: `
    <h2 mat-dialog-title>Registrar Pagamento</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="payment-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Funcionário *</mat-label>
          <mat-select formControlName="employeeId" (selectionChange)="onEmployeeChange()">
            <mat-option *ngFor="let e of employees" [value]="e.id">{{ e.fullName }}</mat-option>
          </mat-select>
          <mat-error>Selecione um funcionário</mat-error>
        </mat-form-field>

        <div *ngIf="loadingPeriods" class="loading-row">
          <mat-spinner diameter="24"></mat-spinner>
          <span>Carregando períodos...</span>
        </div>

        <mat-form-field appearance="outline" class="full-width"
          *ngIf="!loadingPeriods && pendingPeriods.length > 0">
          <mat-label>Período de Pagamento *</mat-label>
          <mat-select formControlName="paymentPeriodId" (selectionChange)="onPeriodChange()">
            <mat-option *ngFor="let p of pendingPeriods" [value]="p.id">
              {{ p.startDate | date:'dd/MM/yyyy' }} → {{ p.endDate | date:'dd/MM/yyyy' }}
              — R$ {{ p.totalAmount | number:'1.2-2' }}
            </mat-option>
          </mat-select>
          <mat-error>Selecione um período</mat-error>
        </mat-form-field>

        <div class="no-periods" *ngIf="!loadingPeriods && form.get('employeeId')?.value && pendingPeriods.length === 0">
          <mat-icon>info</mat-icon>
          <p>Nenhum período pendente. Registre horas e gere um período primeiro.</p>
        </div>

        <div class="period-info" *ngIf="selectedPeriod">
          <span>{{ selectedPeriod.totalHours }}h trabalhadas</span>
          <span class="amount">R$ {{ selectedPeriod.totalAmount | number:'1.2-2' }}</span>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Data de Pagamento *</mat-label>
          <input matInput formControlName="paymentDate" [matDatepicker]="picker">
          <mat-datepicker-toggle matIconSuffix [for]="picker"></mat-datepicker-toggle>
          <mat-datepicker #picker></mat-datepicker>
          <mat-error>Data de pagamento é obrigatória</mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Método de Pagamento *</mat-label>
          <mat-select formControlName="paymentMethod">
            <mat-option [value]="HrPaymentMethod.Pix">PIX</mat-option>
            <mat-option [value]="HrPaymentMethod.BankTransfer">Transferência Bancária</mat-option>
            <mat-option [value]="HrPaymentMethod.Cash">Dinheiro</mat-option>
            <mat-option [value]="HrPaymentMethod.Check">Cheque</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Observações</mat-label>
          <textarea matInput formControlName="notes" rows="2"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()">Cancelar</button>
      <button mat-raised-button color="primary" (click)="onSave()"
        [disabled]="form.invalid || isSaving || pendingPeriods.length === 0">
        {{ isSaving ? 'Registrando...' : 'Registrar Pagamento' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .payment-form { display: flex; flex-direction: column; gap: 14px; min-width: 480px; }
    .full-width { width: 100%; }
    .loading-row { display: flex; align-items: center; gap: 10px; color: var(--color-text-muted); font-size: 13px; }
    .no-periods {
      display: flex; align-items: center; gap: 10px; padding: 16px;
      background: #fff3cd; border: 1px solid #ffc107; border-radius: 6px; color: #555;
    }
    .period-info {
      display: flex; justify-content: space-between; padding: 10px 16px;
      background: #e8f5e9; border-left: 4px solid #2e7d32; border-radius: 4px;
    }
    .amount { font-weight: 700; color: #2e7d32; font-size: 16px; }
  `]
})
export class PaymentFormDialogComponent implements OnInit {
  form!: FormGroup;
  isSaving = false;
  loadingPeriods = false;
  employees: Employee[] = [];
  pendingPeriods: PaymentPeriodSummary[] = [];
  selectedPeriod: PaymentPeriodSummary | null = null;
  HrPaymentMethod = HrPaymentMethod;

  private tenantId: string | null = null;

  constructor(
    private fb: FormBuilder,
    private api: ApiService,
    private auth: AuthService,
    private dialogRef: MatDialogRef<PaymentFormDialogComponent>
  ) {}

  ngOnInit(): void {
    this.tenantId = this.auth.getCurrentTenantId();
    this.form = this.fb.group({
      employeeId: ['', Validators.required],
      paymentPeriodId: ['', Validators.required],
      paymentDate: [new Date(), Validators.required],
      paymentMethod: [HrPaymentMethod.Pix, Validators.required],
      notes: ['']
    });
    if (this.tenantId) {
      this.api.getEmployees(this.tenantId).subscribe({ next: d => this.employees = d });
    }
  }

  onEmployeeChange(): void {
    const id = this.form.get('employeeId')?.value;
    if (!id || !this.tenantId) return;
    this.loadingPeriods = true;
    this.pendingPeriods = [];
    this.selectedPeriod = null;
    this.form.get('paymentPeriodId')?.reset('');
    this.api.getPaymentPeriods(this.tenantId, id).subscribe({
      next: (periods) => {
        this.pendingPeriods = periods.filter(p => p.status === HrPaymentStatus.Pending);
        this.loadingPeriods = false;
      },
      error: () => this.loadingPeriods = false
    });
  }

  onPeriodChange(): void {
    const id = this.form.get('paymentPeriodId')?.value;
    this.selectedPeriod = this.pendingPeriods.find(p => p.id === id) ?? null;
  }

  onSave(): void {
    if (this.form.invalid || !this.tenantId) return;
    this.isSaving = true;
    const v = this.form.value;
    const req: CreateHrPaymentRequest = {
      paymentPeriodId: v.paymentPeriodId,
      paymentDate: new Date(v.paymentDate).toISOString(),
      paymentMethod: v.paymentMethod,
      notes: v.notes || null
    };
    this.api.createHrPayment(this.tenantId, req).subscribe({
      next: (r) => { this.isSaving = false; this.dialogRef.close(r); },
      error: (err) => { this.isSaving = false; alert(err.error?.message ?? 'Erro ao registrar pagamento.'); }
    });
  }

  onCancel(): void { this.dialogRef.close(); }
}
```

- [ ] **Step 3: Create `src/app/modules/hr/payments/payments.component.ts`**

```typescript
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ApiService, AuthService } from '@core/services';
import { HrPayment } from '@core/models';
import { PaymentFormDialogComponent } from './payment-form-dialog.component';
import { GeneratePaymentPeriodDialogComponent } from './generate-payment-period-dialog.component';

@Component({
  selector: 'app-hr-payments',
  standalone: true,
  imports: [
    CommonModule, MatTableModule, MatCardModule, MatButtonModule,
    MatIconModule, MatProgressSpinnerModule, MatDialogModule
  ],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h1>Pagamentos</h1>
          <p class="subtitle">Histórico de pagamentos de funcionários</p>
        </div>
        <div class="button-group">
          <button mat-stroked-button color="accent" (click)="onGeneratePeriod()">
            <mat-icon>event_note</mat-icon> Gerar Período
          </button>
          <button mat-raised-button color="primary" (click)="onNew()">
            <mat-icon>add</mat-icon> Novo Pagamento
          </button>
        </div>
      </div>

      <mat-card>
        <mat-card-content>
          <div *ngIf="isLoading" class="spinner-wrap"><mat-spinner diameter="48"></mat-spinner></div>
          <p *ngIf="!isLoading && payments.length === 0" class="no-data">Nenhum pagamento registrado</p>
          <table mat-table [dataSource]="payments" *ngIf="!isLoading && payments.length > 0" class="full-table">
            <ng-container matColumnDef="employeeName">
              <th mat-header-cell *matHeaderCellDef>Funcionário</th>
              <td mat-cell *matCellDef="let p">{{ p.employeeName }}</td>
            </ng-container>
            <ng-container matColumnDef="paymentDate">
              <th mat-header-cell *matHeaderCellDef>Data</th>
              <td mat-cell *matCellDef="let p">{{ p.paymentDate | date:'dd/MM/yyyy' }}</td>
            </ng-container>
            <ng-container matColumnDef="amount">
              <th mat-header-cell *matHeaderCellDef>Valor</th>
              <td mat-cell *matCellDef="let p">R$ {{ p.amount | number:'1.2-2' }}</td>
            </ng-container>
            <ng-container matColumnDef="paymentMethod">
              <th mat-header-cell *matHeaderCellDef>Método</th>
              <td mat-cell *matCellDef="let p">{{ p.paymentMethodName }}</td>
            </ng-container>
            <ng-container matColumnDef="paidBy">
              <th mat-header-cell *matHeaderCellDef>Pago por</th>
              <td mat-cell *matCellDef="let p">{{ p.paidByUserName }}</td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let p">
                <button mat-icon-button color="warn" (click)="onDelete(p)" title="Excluir">
                  <mat-icon>delete</mat-icon>
                </button>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns;"></tr>
          </table>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .page-container { padding: 24px; max-width: 1200px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    h1 { margin: 0; font-size: 24px; font-weight: 600; color: var(--color-text); }
    .subtitle { margin: 4px 0 0; color: var(--color-text-muted); font-size: 14px; }
    .button-group { display: flex; gap: 12px; }
    .full-table { width: 100%; }
    .spinner-wrap { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; color: var(--color-text-muted); padding: 48px; }
  `]
})
export class HrPaymentsComponent implements OnInit {
  payments: HrPayment[] = [];
  isLoading = false;
  columns = ['employeeName', 'paymentDate', 'amount', 'paymentMethod', 'paidBy', 'actions'];

  private tenantId: string | null = null;

  constructor(private api: ApiService, private auth: AuthService, private dialog: MatDialog) {}

  ngOnInit(): void {
    this.tenantId = this.auth.getCurrentTenantId();
    if (!this.tenantId) return;
    this.load();
  }

  private load(): void {
    if (!this.tenantId) return;
    this.isLoading = true;
    this.api.getRecentHrPayments(this.tenantId, 50).subscribe({
      next: (data) => { this.payments = data; this.isLoading = false; },
      error: () => this.isLoading = false
    });
  }

  onNew(): void {
    this.dialog.open(PaymentFormDialogComponent, { width: '560px' })
      .afterClosed().subscribe(result => { if (result) this.load(); });
  }

  onGeneratePeriod(): void {
    this.dialog.open(GeneratePaymentPeriodDialogComponent, { width: '560px' })
      .afterClosed().subscribe(result => { if (result) this.load(); });
  }

  onDelete(p: HrPayment): void {
    if (!this.tenantId || !confirm(`Excluir pagamento de R$ ${p.amount} para ${p.employeeName}?\nO período voltará para Pendente.`)) return;
    this.api.deleteHrPayment(this.tenantId, p.id).subscribe({
      next: () => this.load(),
      error: (err) => alert(err.error?.message ?? 'Erro ao excluir pagamento.')
    });
  }
}
```

- [ ] **Step 4: Verify TypeScript compiles**

```bash
cd "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" && npx tsc --noEmit
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
cd "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" && git add src/app/modules/hr/payments/ && git commit -m "feat(hr): add PaymentsComponent with GeneratePeriodDialog and PaymentFormDialog"
```

---

## Task 6: Routes + Navigation

**Files:**
- Modify: `src/app/app.routes.ts`
- Modify: `src/app/shared/components/layout/layout.component.ts`

- [ ] **Step 1: Add 3 HR routes to `app.routes.ts`**

Open `src/app/app.routes.ts`. Add these imports at the top:

```typescript
import { EmployeesComponent } from './modules/hr/employees/employees.component';
import { WorklogsComponent } from './modules/hr/worklogs/worklogs.component';
import { HrPaymentsComponent } from './modules/hr/payments/payments.component';
```

Inside the `children` array of the `LayoutComponent` route, after the last `finance/...` entry, add:

```typescript
      {
        path: 'hr/employees',
        component: EmployeesComponent
      },
      {
        path: 'hr/worklogs',
        component: WorklogsComponent
      },
      {
        path: 'hr/payments',
        component: HrPaymentsComponent
      },
```

- [ ] **Step 2: Add "RH" section to the layout sidenav**

Open `src/app/shared/components/layout/layout.component.ts`. Find the `navigationSections` (or `navSections`) array where the existing sections like "Contas a Pagar" and "Financeiro" are defined. Add a new section after "Financeiro":

```typescript
    {
      label: 'RH',
      items: [
        { label: 'Funcionários', icon: 'badge', route: '/hr/employees', permission: null },
        { label: 'Horas',        icon: 'schedule', route: '/hr/worklogs',   permission: null },
        { label: 'Pagamentos',   icon: 'payments', route: '/hr/payments',   permission: null }
      ]
    },
```

> **Note:** Read the actual layout component first to match the exact type signature used for sections/items (`NavigationSection`, `NavItem`, or similar). Use the same field names already in use.

- [ ] **Step 3: Verify TypeScript compiles**

```bash
cd "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" && npx tsc --noEmit
```

Expected: 0 errors.

- [ ] **Step 4: Build to confirm no Angular template errors**

```bash
cd "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" && npx ng build --configuration development 2>&1 | tail -20
```

Expected: `Application bundle generation complete.`

- [ ] **Step 5: Commit**

```bash
cd "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" && git add src/app/app.routes.ts src/app/shared/components/layout/layout.component.ts && git commit -m "feat(hr): add HR routes and RH sidenav section"
```

---

## Self-Review

**Spec coverage:**
- ✅ Employee list + create/edit form dialog — Task 3
- ✅ WorkLog list with employee filter + clockIn/clockOut form — Task 4
- ✅ Payments list + generate period dialog + payment form dialog — Task 5
- ✅ HR models added to `index.ts` — Task 1
- ✅ API methods with `tenantId` prefix — Task 2
- ✅ Routes `/hr/employees`, `/hr/worklogs`, `/hr/payments` — Task 6
- ✅ "RH" section in sidenav — Task 6

**Placeholder scan:** All code blocks are complete. No TBDs.

**Type consistency:** All types used in components (Employee, WorkLog, PaymentPeriodSummary, HrPayment, enums) are defined in Task 1 and referenced consistently throughout.

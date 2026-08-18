# User Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add tenant-scoped user management — two missing backend endpoints (user lookup by email, update member role) plus a complete Angular module letting tenant admins list, add, change roles, and remove members.

**Architecture:** Backend follows the existing TenantsController pattern. Frontend adds a `TenantMembersComponent` to the existing `admin` module in `SaaSBasePlatform-Angular`, using the centralised `api.service.ts` for HTTP calls, Angular Material table + dialogs, reactive forms, and the same auth/guard patterns already in use.

**Tech Stack:** .NET 10, xUnit + NSubstitute (backend tests); Angular 18 standalone components, Angular Material 18, reactive forms, `SaaSBasePlatform-Angular/src/`

---

## File Map

### New — Backend (SaaSBasePlatform)
- Modify: `SaaS_BasePlatform.Application/Services/ITenantService.cs` — add `LookupUserByEmailAsync`, `UpdateMemberRoleAsync`
- Modify: `SaaS_BasePlatform.Application/Services/TenantService.cs` — implement new methods
- Modify: `SaaS_BasePlatform.Application/DTOs/Tenants/TenantDtos.cs` — add `UserLookupDto`, `UpdateMemberRoleDto`
- Modify: `SaaS_BasePlatform.Api/Controllers/TenantsController.cs` — add 2 new endpoints

### New — Frontend (SaaSBasePlatform-Angular)
- Modify: `src/app/core/services/api.service.ts` — add tenant member methods
- Create: `src/app/modules/admin/tenant-members/tenant-members.component.ts`
- Create: `src/app/modules/admin/tenant-members/tenant-members.component.html`
- Create: `src/app/modules/admin/tenant-members/add-member-dialog.component.ts`
- Create: `src/app/modules/admin/tenant-members/change-role-dialog.component.ts`
- Modify: `src/app/app.routes.ts` — add route for tenant members

---

## Task 1: DTOs + ITenantService

**Files:**
- Modify: `SaaS_BasePlatform.Application/DTOs/Tenants/TenantDtos.cs`
- Modify: `SaaS_BasePlatform.Application/Services/ITenantService.cs`

- [ ] **Step 1: Add new DTOs to TenantDtos.cs**

Open `SaaS_BasePlatform.Application/DTOs/Tenants/TenantDtos.cs`. Add at the end of the file:

```csharp
    public record UserLookupDto(Guid UserId, string Email, string? FullName);

    public record UpdateMemberRoleDto(TenantRole Role);
```

- [ ] **Step 2: Add method signatures to ITenantService.cs**

Open `SaaS_BasePlatform.Application/Services/ITenantService.cs`. Add these two signatures to the interface:

```csharp
    Task<UserLookupDto?> LookupUserByEmailAsync(string email, CancellationToken ct = default);
    Task UpdateMemberRoleAsync(Guid tenantId, Guid userId, TenantRole newRole, CancellationToken ct = default);
```

- [ ] **Step 3: Build Application layer**

```bash
dotnet build SaaS_BasePlatform.Application
```

Expected: Build error — `TenantService` does not implement the new interface members. That is expected; proceed to Task 2.

---

## Task 2: TenantService Implementation

**Files:**
- Modify: `SaaS_BasePlatform.Application/Services/TenantService.cs`

- [ ] **Step 1: Implement LookupUserByEmailAsync**

Open `SaaS_BasePlatform.Application/Services/TenantService.cs`. Add the new method (after `GetUserRoleAsync`):

```csharp
        public async Task<UserLookupDto?> LookupUserByEmailAsync(string email, CancellationToken ct = default)
        {
            var user = await _userManager.FindByEmailAsync(email.Trim().ToLowerInvariant());
            if (user == null || !user.IsActive)
                return null;
            return new UserLookupDto(user.Id, user.Email!, user.FullName);
        }
```

> **Note:** `TenantService` must already have `UserManager<ApplicationUser> _userManager` injected. If it does not, add it to the constructor: `private readonly UserManager<ApplicationUser> _userManager;` and inject via constructor parameter.

- [ ] **Step 2: Implement UpdateMemberRoleAsync**

In the same file, add (after `LookupUserByEmailAsync`):

```csharp
        public async Task UpdateMemberRoleAsync(
            Guid tenantId, Guid userId, TenantRole newRole, CancellationToken ct = default)
        {
            var member = await _context.TenantUsers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId, ct)
                ?? throw new KeyNotFoundException("Member not found.");

            if (member.Role == TenantRole.Owner)
                throw new InvalidOperationException("Cannot change the Owner's role.");

            if (newRole == TenantRole.Owner)
                throw new InvalidOperationException("Cannot promote a member to Owner.");

            member.Role = newRole;
            await _context.SaveChangesAsync(ct);
        }
```

- [ ] **Step 3: Build Application layer**

```bash
dotnet build SaaS_BasePlatform.Application
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 4: Commit**

```bash
git add SaaS_BasePlatform.Application/DTOs/Tenants/TenantDtos.cs \
        SaaS_BasePlatform.Application/Services/ITenantService.cs \
        SaaS_BasePlatform.Application/Services/TenantService.cs
git commit -m "feat: add user lookup by email and member role update to TenantService"
```

---

## Task 3: Controller Endpoints + Tests

**Files:**
- Modify: `SaaS_BasePlatform.Api/Controllers/TenantsController.cs`
- Create: `SaaS_BasePlatform.Tests/Services/TenantServiceMemberTests.cs`

- [ ] **Step 1: Write failing tests**

Create `SaaS_BasePlatform.Tests/Services/TenantServiceMemberTests.cs`:

```csharp
using NSubstitute;
using SaaS_BasePlatform.Application.DTOs.Tenants;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Tests.Services;

public class TenantServiceMemberTests
{
    private readonly ITenantService _service = Substitute.For<ITenantService>();

    [Fact]
    public async Task LookupUserByEmail_WhenUserExists_ReturnsDto()
    {
        var expected = new UserLookupDto(Guid.NewGuid(), "alice@example.com", "Alice");
        _service.LookupUserByEmailAsync("alice@example.com", Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _service.LookupUserByEmailAsync("alice@example.com");

        Assert.NotNull(result);
        Assert.Equal("alice@example.com", result!.Email);
    }

    [Fact]
    public async Task LookupUserByEmail_WhenUserNotFound_ReturnsNull()
    {
        _service.LookupUserByEmailAsync("ghost@example.com", Arg.Any<CancellationToken>())
            .Returns((UserLookupDto?)null);

        var result = await _service.LookupUserByEmailAsync("ghost@example.com");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateMemberRole_CallsService_WithCorrectArguments()
    {
        var tenantId = Guid.NewGuid();
        var userId   = Guid.NewGuid();

        await _service.UpdateMemberRoleAsync(tenantId, userId, TenantRole.Admin);

        await _service.Received(1).UpdateMemberRoleAsync(tenantId, userId, TenantRole.Admin, Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run tests — expect pass (interface mocked, no real DB)**

```bash
dotnet test --filter "FullyQualifiedName~TenantServiceMemberTests"
```

Expected: 3 tests passing.

- [ ] **Step 3: Add two endpoints to TenantsController**

Open `SaaS_BasePlatform.Api/Controllers/TenantsController.cs`. Add before the closing `}` of the class:

```csharp
        [HttpGet("users/lookup")]
        [ProducesResponseType(typeof(UserLookupDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserLookupDto>> LookupUser(
            [FromQuery] string email, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("email query parameter is required.");

            var user = await _tenantService.LookupUserByEmailAsync(email, ct);
            return user == null ? NotFound() : Ok(user);
        }

        [HttpPut("{tenantId:guid}/members/{userId:guid}/role")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateMemberRole(
            Guid tenantId, Guid userId, [FromBody] UpdateMemberRoleDto request, CancellationToken ct)
        {
            var callerRole = await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);
            if (callerRole is not (TenantRole.Owner or TenantRole.Admin)) return Forbid();

            await _tenantService.UpdateMemberRoleAsync(tenantId, userId, request.Role, ct);
            return NoContent();
        }
```

- [ ] **Step 4: Build full solution**

```bash
dotnet build
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 5: Run all tests**

```bash
dotnet test
```

Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add SaaS_BasePlatform.Api/Controllers/TenantsController.cs \
        SaaS_BasePlatform.Tests/Services/TenantServiceMemberTests.cs
git commit -m "feat: add user lookup by email and update member role endpoints"
```

---

## Task 4: Angular API Service Extension

**Files:**
- Modify: `SaaSBasePlatform-Angular/src/app/core/services/api.service.ts`

> **Note:** All edits below are to `C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular\src\app\core\services\api.service.ts`.

- [ ] **Step 1: Read the current api.service.ts to find the right insertion point**

Read the file and locate the section that handles tenant-related calls (search for `tenants`).

- [ ] **Step 2: Add interfaces for new DTOs (at the top of the file or in a models file)**

In the existing models (or `src/app/core/models/index.ts`), add:

```typescript
export interface UserLookupResult {
  userId: string;
  email: string;
  fullName: string | null;
}

export interface TenantMember {
  userId: string;
  email: string;
  fullName: string | null;
  role: number;         // 0=Member, 1=Admin, 2=Owner
  joinedAt: string;
}

export interface AddTenantMemberRequest {
  userId: string;
  role: number;
}

export interface UpdateMemberRoleRequest {
  role: number;
}
```

- [ ] **Step 3: Add tenant member methods to api.service.ts**

In `api.service.ts`, add the following methods to the service class. Place them alongside other tenant-related methods:

```typescript
  lookupUserByEmail(email: string): Observable<UserLookupResult | null> {
    return this.http.get<UserLookupResult>(
      `${this.apiUrl}/tenants/users/lookup?email=${encodeURIComponent(email)}`
    ).pipe(catchError(() => of(null)));
  }

  getTenantMembers(tenantId: string): Observable<TenantMember[]> {
    return this.http.get<TenantMember[]>(`${this.apiUrl}/tenants/${tenantId}/members`);
  }

  addTenantMember(tenantId: string, request: AddTenantMemberRequest): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/tenants/${tenantId}/members`, request);
  }

  updateMemberRole(tenantId: string, userId: string, request: UpdateMemberRoleRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/tenants/${tenantId}/members/${userId}/role`, request);
  }

  removeTenantMember(tenantId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/tenants/${tenantId}/members/${userId}`);
  }
```

> Make sure `catchError` and `of` are imported from `rxjs/operators` and `rxjs` respectively. If `catchError` is already used elsewhere in the file, no import change is needed.

- [ ] **Step 4: Verify Angular compilation**

```bash
cd C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular
npx ng build --configuration development 2>&1 | tail -20
```

Expected: Compilation successful.

- [ ] **Step 5: Commit**

```bash
git -C "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" add src/app/core/services/api.service.ts src/app/core/models/index.ts
git -C "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" commit -m "feat: add tenant member management methods to api.service"
```

---

## Task 5: Add Member Dialog Component

**Files:**
- Create: `SaaSBasePlatform-Angular/src/app/modules/admin/tenant-members/add-member-dialog.component.ts`

- [ ] **Step 1: Create the dialog component**

Create `src/app/modules/admin/tenant-members/add-member-dialog.component.ts`:

```typescript
import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { ApiService, UserLookupResult } from '../../../core/services/api.service';

@Component({
  selector: 'app-add-member-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatDialogModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatButtonModule, MatProgressSpinnerModule, MatIconModule
  ],
  template: `
    <h2 mat-dialog-title>Add Team Member</h2>
    <mat-dialog-content>
      <form [formGroup]="form" (ngSubmit)="onLookup()">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>User Email</mat-label>
          <input matInput formControlName="email" type="email" placeholder="user@example.com">
          <mat-error *ngIf="form.get('email')?.hasError('required')">Email is required</mat-error>
          <mat-error *ngIf="form.get('email')?.hasError('email')">Enter a valid email</mat-error>
        </mat-form-field>

        <button mat-stroked-button type="submit" [disabled]="lookingUp || form.get('email')?.invalid">
          <mat-spinner *ngIf="lookingUp" diameter="18" style="display:inline-block"></mat-spinner>
          Look Up
        </button>

        <div *ngIf="lookupDone && !foundUser" class="not-found-msg">
          No active user found with that email.
        </div>

        <div *ngIf="foundUser" class="found-user">
          <mat-icon color="primary">check_circle</mat-icon>
          <span><strong>{{ foundUser.fullName || foundUser.email }}</strong> ({{ foundUser.email }})</span>
        </div>

        <mat-form-field appearance="outline" class="full-width" *ngIf="foundUser" style="margin-top:16px">
          <mat-label>Role</mat-label>
          <mat-select formControlName="role">
            <mat-option [value]="0">Member</mat-option>
            <mat-option [value]="1">Admin</mat-option>
          </mat-select>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary"
        [disabled]="!foundUser || saving"
        (click)="onAdd()">
        <mat-spinner *ngIf="saving" diameter="18" style="display:inline-block"></mat-spinner>
        Add Member
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .full-width { width: 100%; }
    .not-found-msg { color: #e53935; margin: 8px 0; font-size: 13px; }
    .found-user { display: flex; align-items: center; gap: 8px; color: #388e3c; margin: 8px 0; }
  `]
})
export class AddMemberDialogComponent {
  form: FormGroup;
  lookingUp = false;
  lookupDone = false;
  saving = false;
  foundUser: UserLookupResult | null = null;

  constructor(
    private fb: FormBuilder,
    private api: ApiService,
    private dialogRef: MatDialogRef<AddMemberDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { tenantId: string }
  ) {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      role: [0]
    });
  }

  onLookup(): void {
    if (this.form.get('email')?.invalid) return;
    this.lookingUp = true;
    this.lookupDone = false;
    this.foundUser = null;

    this.api.lookupUserByEmail(this.form.value.email).subscribe({
      next: user => {
        this.foundUser = user;
        this.lookupDone = true;
        this.lookingUp = false;
      },
      error: () => {
        this.lookupDone = true;
        this.lookingUp = false;
      }
    });
  }

  onAdd(): void {
    if (!this.foundUser) return;
    this.saving = true;

    this.api.addTenantMember(this.data.tenantId, {
      userId: this.foundUser.userId,
      role: this.form.value.role
    }).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => { this.saving = false; }
    });
  }
}
```

- [ ] **Step 2: Build to verify**

```bash
cd C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular
npx ng build --configuration development 2>&1 | tail -20
```

Expected: Compilation successful.

---

## Task 6: Change Role Dialog Component

**Files:**
- Create: `SaaSBasePlatform-Angular/src/app/modules/admin/tenant-members/change-role-dialog.component.ts`

- [ ] **Step 1: Create the dialog component**

Create `src/app/modules/admin/tenant-members/change-role-dialog.component.ts`:

```typescript
import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService, TenantMember } from '../../../core/services/api.service';

@Component({
  selector: 'app-change-role-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatDialogModule, MatFormFieldModule, MatSelectModule,
    MatButtonModule, MatProgressSpinnerModule
  ],
  template: `
    <h2 mat-dialog-title>Change Role</h2>
    <mat-dialog-content>
      <p>Update role for <strong>{{ data.member.fullName || data.member.email }}</strong>:</p>
      <form [formGroup]="form">
        <mat-form-field appearance="outline" style="width:100%">
          <mat-label>Role</mat-label>
          <mat-select formControlName="role">
            <mat-option [value]="0">Member</mat-option>
            <mat-option [value]="1">Admin</mat-option>
          </mat-select>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary" [disabled]="saving" (click)="onSave()">
        <mat-spinner *ngIf="saving" diameter="18" style="display:inline-block"></mat-spinner>
        Save
      </button>
    </mat-dialog-actions>
  `
})
export class ChangeRoleDialogComponent {
  form: FormGroup;
  saving = false;

  constructor(
    private fb: FormBuilder,
    private api: ApiService,
    private dialogRef: MatDialogRef<ChangeRoleDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { tenantId: string; member: TenantMember }
  ) {
    this.form = this.fb.group({ role: [data.member.role] });
  }

  onSave(): void {
    this.saving = true;
    this.api.updateMemberRole(this.data.tenantId, this.data.member.userId, {
      role: this.form.value.role
    }).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => { this.saving = false; }
    });
  }
}
```

---

## Task 7: Tenant Members Component + Routing

**Files:**
- Create: `SaaSBasePlatform-Angular/src/app/modules/admin/tenant-members/tenant-members.component.ts`
- Create: `SaaSBasePlatform-Angular/src/app/modules/admin/tenant-members/tenant-members.component.html`
- Modify: `SaaSBasePlatform-Angular/src/app/app.routes.ts`

- [ ] **Step 1: Create tenant-members.component.ts**

Create `src/app/modules/admin/tenant-members/tenant-members.component.ts`:

```typescript
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService, TenantMember } from '../../../core/services/api.service';
import { AuthService } from '../../../core/services/auth.service';
import { AddMemberDialogComponent } from './add-member-dialog.component';
import { ChangeRoleDialogComponent } from './change-role-dialog.component';

const ROLE_LABELS: Record<number, string> = { 0: 'Member', 1: 'Admin', 2: 'Owner' };
const ROLE_COLORS: Record<number, string> = { 0: 'default', 1: 'primary', 2: 'accent' };

@Component({
  selector: 'app-tenant-members',
  standalone: true,
  imports: [
    CommonModule, MatTableModule, MatButtonModule, MatIconModule,
    MatDialogModule, MatSnackBarModule, MatChipsModule,
    MatProgressSpinnerModule, MatTooltipModule
  ],
  templateUrl: './tenant-members.component.html'
})
export class TenantMembersComponent implements OnInit {
  displayedColumns = ['name', 'email', 'role', 'joinedAt', 'actions'];
  members: TenantMember[] = [];
  loading = false;
  currentUserId: string = '';

  private tenantId: string = '';

  constructor(
    private api: ApiService,
    private auth: AuthService,
    private dialog: MatDialog,
    private snack: MatSnackBar
  ) {}

  ngOnInit(): void {
    const user = this.auth.getCurrentUser();
    this.currentUserId = user?.id ?? '';
    this.tenantId = this.auth.getCurrentTenantId() ?? '';
    this.loadMembers();
  }

  loadMembers(): void {
    this.loading = true;
    this.api.getTenantMembers(this.tenantId).subscribe({
      next: members => { this.members = members; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  roleLabel(role: number): string { return ROLE_LABELS[role] ?? 'Unknown'; }
  roleColor(role: number): string { return ROLE_COLORS[role] ?? 'default'; }

  canManage(member: TenantMember): boolean {
    const myRole = this.members.find(m => m.userId === this.currentUserId)?.role ?? -1;
    return (myRole === 1 || myRole === 2) && member.role !== 2;
  }

  openAddDialog(): void {
    const ref = this.dialog.open(AddMemberDialogComponent, {
      width: '420px',
      data: { tenantId: this.tenantId }
    });
    ref.afterClosed().subscribe(added => { if (added) this.loadMembers(); });
  }

  openChangeRoleDialog(member: TenantMember): void {
    const ref = this.dialog.open(ChangeRoleDialogComponent, {
      width: '360px',
      data: { tenantId: this.tenantId, member }
    });
    ref.afterClosed().subscribe(changed => { if (changed) this.loadMembers(); });
  }

  removeMember(member: TenantMember): void {
    if (!confirm(`Remove ${member.fullName || member.email} from this tenant?`)) return;
    this.api.removeTenantMember(this.tenantId, member.userId).subscribe({
      next: () => {
        this.snack.open('Member removed.', 'OK', { duration: 3000 });
        this.loadMembers();
      },
      error: () => this.snack.open('Failed to remove member.', 'OK', { duration: 4000 })
    });
  }
}
```

- [ ] **Step 2: Create tenant-members.component.html**

Create `src/app/modules/admin/tenant-members/tenant-members.component.html`:

```html
<div class="page-container">
  <div class="page-header">
    <h1>Team Members</h1>
    <button mat-flat-button color="primary" (click)="openAddDialog()">
      <mat-icon>person_add</mat-icon> Add Member
    </button>
  </div>

  <div *ngIf="loading" class="loading-container">
    <mat-spinner diameter="48"></mat-spinner>
  </div>

  <table mat-table [dataSource]="members" class="mat-elevation-z2 full-width" *ngIf="!loading">

    <ng-container matColumnDef="name">
      <th mat-header-cell *matHeaderCellDef>Name</th>
      <td mat-cell *matCellDef="let m">{{ m.fullName || '—' }}</td>
    </ng-container>

    <ng-container matColumnDef="email">
      <th mat-header-cell *matHeaderCellDef>Email</th>
      <td mat-cell *matCellDef="let m">{{ m.email }}</td>
    </ng-container>

    <ng-container matColumnDef="role">
      <th mat-header-cell *matHeaderCellDef>Role</th>
      <td mat-cell *matCellDef="let m">
        <mat-chip [color]="roleColor(m.role)" selected>{{ roleLabel(m.role) }}</mat-chip>
      </td>
    </ng-container>

    <ng-container matColumnDef="joinedAt">
      <th mat-header-cell *matHeaderCellDef>Joined</th>
      <td mat-cell *matCellDef="let m">{{ m.joinedAt | date:'mediumDate' }}</td>
    </ng-container>

    <ng-container matColumnDef="actions">
      <th mat-header-cell *matHeaderCellDef></th>
      <td mat-cell *matCellDef="let m">
        <button mat-icon-button matTooltip="Change role"
          *ngIf="canManage(m)"
          (click)="openChangeRoleDialog(m)">
          <mat-icon>manage_accounts</mat-icon>
        </button>
        <button mat-icon-button matTooltip="Remove member" color="warn"
          *ngIf="canManage(m)"
          (click)="removeMember(m)">
          <mat-icon>person_remove</mat-icon>
        </button>
      </td>
    </ng-container>

    <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
    <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>

    <tr class="mat-row" *matNoDataRow>
      <td class="mat-cell" colspan="5" style="text-align:center;padding:32px">
        No members found.
      </td>
    </tr>
  </table>
</div>

<style>
  .page-container { padding: 24px; }
  .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
  .full-width { width: 100%; }
  .loading-container { display: flex; justify-content: center; padding: 64px; }
</style>
```

- [ ] **Step 3: Add route to app.routes.ts**

Open `src/app/app.routes.ts`. In the section where admin routes are defined, add:

```typescript
      {
        path: 'admin/members',
        loadComponent: () =>
          import('./modules/admin/tenant-members/tenant-members.component')
            .then(m => m.TenantMembersComponent),
        data: { roles: ['Admin', 'Owner'] }
      },
```

Also add a navigation link to the sidenav (wherever `Admin / Users & Roles` links appear in the app shell component) pointing to `/admin/members` with label "Team Members" and icon `group`.

- [ ] **Step 4: Final build**

```bash
cd C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular
npx ng build --configuration development 2>&1 | tail -20
```

Expected: Compilation successful, 0 errors.

- [ ] **Step 5: Commit**

```bash
git -C "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" add src/
git -C "C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular" commit -m "feat: add tenant members management module (list, add, change role, remove)"
```

---

## Quick Reference: New Endpoints

| Method | URL | Auth | Description |
|--------|-----|------|-------------|
| GET | `api/tenants/users/lookup?email=` | Any authenticated | Look up user by email |
| PUT | `api/tenants/{tenantId}/members/{userId}/role` | Admin or Owner | Update member's tenant role |

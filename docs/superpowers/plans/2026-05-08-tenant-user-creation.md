# Tenant User Creation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow a tenant Owner/Admin to create a new platform user and add them to the tenant in a single dialog action from the tenant members screen.

**Architecture:** New `POST /api/tenants/{tenantId}/users` endpoint calls a new `TenantService.CreateAndAddMemberAsync` method that creates the user via `UserManager`, assigns the "Usuario" system role, then adds them as a tenant member. The Angular `AddMemberDialogComponent` gains a `'create'` mode that activates when email lookup finds no existing user.

**Tech Stack:** .NET 10 / ASP.NET Core Identity / EF Core (backend), Angular 18 + Angular Material + Reactive Forms (frontend), xUnit + NSubstitute (tests)

---

## File Map

**Backend — create/modify:**
- Modify: `SaaS_BasePlatform.Application/DTOs/Tenants/TenantDtos.cs` — add `CreateTenantUserDto`
- Modify: `SaaS_BasePlatform.Application/Services/ITenantService.cs` — add interface method
- Modify: `SaaS_BasePlatform.Application/Services/TenantService.cs` — inject `UserManager`, implement method
- Modify: `SaaS_BasePlatform.Api/Controllers/TenantsController.cs` — add endpoint
- Create: `SaaS_BasePlatform.Tests/Services/TenantServiceCreateUserTests.cs` — unit tests
- Modify: `SaaS_BasePlatform.Tests/SaaS_BasePlatform.Tests.csproj` — add EF InMemory package

**Frontend — modify (separate repo `SaaSBasePlatform-Angular`):**
- Modify: `src/app/core/models/index.ts` — add `CreateTenantUserRequest`
- Modify: `src/app/core/services/api.service.ts` — add `createTenantUser`
- Modify: `src/app/modules/admin/tenant-members/add-member-dialog.component.ts` — add create mode

---

## Task 1: Add `CreateTenantUserDto`

**Files:**
- Modify: `SaaS_BasePlatform.Application/DTOs/Tenants/TenantDtos.cs`

- [ ] **Step 1: Add the record to TenantDtos.cs**

  Open `SaaS_BasePlatform.Application/DTOs/Tenants/TenantDtos.cs` and append after the last record:

  ```csharp
  public record CreateTenantUserDto(
      string Email,
      string Password,
      string FullName,
      string? PhoneNumber,
      TenantRole Role
  );
  ```

- [ ] **Step 2: Build to confirm no errors**

  ```bash
  dotnet build SaaS_BasePlatform.Application
  ```
  Expected: Build succeeded, 0 errors.

---

## Task 2: Add interface method to `ITenantService`

**Files:**
- Modify: `SaaS_BasePlatform.Application/Services/ITenantService.cs`

- [ ] **Step 1: Add the method signature**

  Open `SaaS_BasePlatform.Application/Services/ITenantService.cs` and add after `GetUserRoleAsync`:

  ```csharp
  Task<TenantMemberDto> CreateAndAddMemberAsync(
      Guid tenantId, CreateTenantUserDto dto, CancellationToken cancellationToken = default);
  ```

- [ ] **Step 2: Build Application — expect a compile error in TenantService**

  ```bash
  dotnet build SaaS_BasePlatform.Application
  ```
  Expected: Error — `TenantService` does not implement `CreateAndAddMemberAsync`. This is expected and confirms the interface is wired.

---

## Task 3: Add EF InMemory package to Tests project and write failing tests

**Files:**
- Modify: `SaaS_BasePlatform.Tests/SaaS_BasePlatform.Tests.csproj`
- Create: `SaaS_BasePlatform.Tests/Services/TenantServiceCreateUserTests.cs`

- [ ] **Step 1: Add EF InMemory package to test project**

  Check the EF Core version already used in Infrastructure:
  ```bash
  grep "EntityFrameworkCore\"" SaaS_BasePlatform.Infrastructure/SaaS_BasePlatform.Infrastructure.csproj
  ```

  Open `SaaS_BasePlatform.Tests/SaaS_BasePlatform.Tests.csproj` and add the InMemory package using the **same version number** you found above:

  ```xml
  <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="<same-as-infrastructure>" />
  ```

  Run restore:
  ```bash
  dotnet restore SaaS_BasePlatform.Tests
  ```

- [ ] **Step 2: Create the test file**

  Create `SaaS_BasePlatform.Tests/Services/TenantServiceCreateUserTests.cs`:

  ```csharp
  using Microsoft.AspNetCore.Identity;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Logging;
  using Microsoft.Extensions.Options;
  using NSubstitute;
  using SaaS_BasePlatform.Application.DTOs.Tenants;
  using SaaS_BasePlatform.Application.Services;
  using SaaS_BasePlatform.Domain.Entities;
  using SaaS_BasePlatform.Domain.Enums;
  using SaaS_BasePlatform.Infrastructure.Data;

  namespace SaaS_BasePlatform.Tests.Services;

  public class TenantServiceCreateUserTests
  {
      private static ApplicationDbContext MakeDb() =>
          new(new DbContextOptionsBuilder<ApplicationDbContext>()
              .UseInMemoryDatabase(Guid.NewGuid().ToString())
              .Options);

      private static UserManager<ApplicationUser> MakeUserManager()
      {
          var store = Substitute.For<IUserStore<ApplicationUser>>();
          return Substitute.For<UserManager<ApplicationUser>>(
              store, null, null, null, null, null, null, null, null);
      }

      [Fact]
      public async Task CreateAndAddMemberAsync_WhenEmailAlreadyExists_ThrowsInvalidOperation()
      {
          var db = MakeDb();
          var userManager = MakeUserManager();
          var existingUser = new ApplicationUser { Email = "taken@example.com", UserName = "taken@example.com" };
          userManager.FindByEmailAsync("taken@example.com").Returns(existingUser);

          var sut = new TenantService(db, userManager);
          var dto = new CreateTenantUserDto("taken@example.com", "Pass1!", "Test User", null, TenantRole.Member);

          await Assert.ThrowsAsync<InvalidOperationException>(() =>
              sut.CreateAndAddMemberAsync(Guid.NewGuid(), dto));
      }

      [Fact]
      public async Task CreateAndAddMemberAsync_WhenIdentityFails_ThrowsInvalidOperation()
      {
          var db = MakeDb();
          var userManager = MakeUserManager();
          userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
          userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
              .Returns(IdentityResult.Failed(new IdentityError { Description = "Too weak" }));

          var sut = new TenantService(db, userManager);
          var dto = new CreateTenantUserDto("new@example.com", "weak", "Test User", null, TenantRole.Member);

          await Assert.ThrowsAsync<InvalidOperationException>(() =>
              sut.CreateAndAddMemberAsync(Guid.NewGuid(), dto));
      }

      [Fact]
      public async Task CreateAndAddMemberAsync_OnSuccess_ReturnsMemberDtoAndAddsTenantUser()
      {
          var db = MakeDb();
          var userManager = MakeUserManager();
          var tenantId = Guid.NewGuid();

          // Seed tenant so FK constraint is satisfied
          db.Tenants.Add(new Tenant { Id = tenantId, Name = "Acme", Slug = "acme", OwnerUserId = Guid.NewGuid() });
          await db.SaveChangesAsync();

          userManager.FindByEmailAsync("new@example.com").Returns((ApplicationUser?)null);
          userManager.CreateAsync(Arg.Any<ApplicationUser>(), "Pass1!")
              .Returns(callInfo =>
              {
                  // Simulate Identity persisting the user by adding it to the DB
                  var user = callInfo.ArgAt<ApplicationUser>(0);
                  db.Users.Add(user);
                  db.SaveChanges();
                  return IdentityResult.Success;
              });
          userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), "Usuario")
              .Returns(IdentityResult.Success);

          var sut = new TenantService(db, userManager);
          var dto = new CreateTenantUserDto("new@example.com", "Pass1!", "João Silva", "+55 11 99999-0000", TenantRole.Member);

          var result = await sut.CreateAndAddMemberAsync(tenantId, dto);

          Assert.Equal("new@example.com", result.Email);
          Assert.Equal("João Silva", result.FullName);
          Assert.Equal(TenantRole.Member, result.Role);
          Assert.True(await db.TenantUsers.AnyAsync(tu => tu.TenantId == tenantId));
      }
  }
  ```

- [ ] **Step 3: Run tests — expect compile failure (TenantService doesn't have the new constructor yet)**

  ```bash
  dotnet test SaaS_BasePlatform.Tests --filter "FullyQualifiedName~TenantServiceCreateUserTests" 2>&1 | head -20
  ```
  Expected: Build error — `TenantService` constructor doesn't accept `UserManager`. This confirms the tests are wired correctly.

---

## Task 4: Implement `CreateAndAddMemberAsync` in `TenantService`

**Files:**
- Modify: `SaaS_BasePlatform.Application/Services/TenantService.cs`

- [ ] **Step 1: Add using directives and inject `UserManager`**

  Open `SaaS_BasePlatform.Application/Services/TenantService.cs`.

  Add to the top-level usings:
  ```csharp
  using Microsoft.AspNetCore.Identity;
  ```

  Replace the class fields and constructor:
  ```csharp
  public class TenantService : ITenantService
  {
      private readonly ApplicationDbContext _db;
      private readonly UserManager<ApplicationUser> _userManager;

      public TenantService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
      {
          _db = db;
          _userManager = userManager;
      }
  ```

- [ ] **Step 2: Add the `CreateAndAddMemberAsync` method**

  Append inside the class, after `GetUserRoleAsync`:

  ```csharp
  public async Task<TenantMemberDto> CreateAndAddMemberAsync(
      Guid tenantId, CreateTenantUserDto dto, CancellationToken cancellationToken = default)
  {
      var existing = await _userManager.FindByEmailAsync(dto.Email);
      if (existing != null)
          throw new InvalidOperationException($"A user with email '{dto.Email}' already exists.");

      var user = new ApplicationUser
      {
          UserName = dto.Email,
          Email = dto.Email,
          FullName = dto.FullName,
          PhoneNumber = dto.PhoneNumber,
          EmailConfirmed = true,
          IsActive = true,
          CreatedAt = DateTime.UtcNow
      };

      var createResult = await _userManager.CreateAsync(user, dto.Password);
      if (!createResult.Succeeded)
          throw new InvalidOperationException(
              string.Join("; ", createResult.Errors.Select(e => e.Description)));

      await _userManager.AddToRoleAsync(user, "Usuario");
      await AddMemberAsync(tenantId, user.Id, dto.Role, cancellationToken);

      return new TenantMemberDto(user.Id, user.Email!, user.FullName, dto.Role, DateTime.UtcNow);
  }
  ```

- [ ] **Step 3: Run the tests**

  ```bash
  dotnet test SaaS_BasePlatform.Tests --filter "FullyQualifiedName~TenantServiceCreateUserTests" -v normal
  ```
  Expected: All 3 tests PASS.

- [ ] **Step 4: Run full test suite to check for regressions**

  ```bash
  dotnet test
  ```
  Expected: All tests pass.

---

## Task 5: Add `POST /api/tenants/{tenantId}/users` endpoint

**Files:**
- Modify: `SaaS_BasePlatform.Api/Controllers/TenantsController.cs`

- [ ] **Step 1: Add the endpoint**

  Open `SaaS_BasePlatform.Api/Controllers/TenantsController.cs` and add after the `AddMember` action:

  ```csharp
  [HttpPost("{tenantId:guid}/users")]
  [ProducesResponseType(typeof(TenantMemberDto), StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<TenantMemberDto>> CreateUser(
      Guid tenantId, [FromBody] CreateTenantUserDto request, CancellationToken ct)
  {
      var role = await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);
      if (role is not (TenantRole.Owner or TenantRole.Admin)) return Forbid();

      var member = await _tenantService.CreateAndAddMemberAsync(tenantId, request, ct);
      return StatusCode(StatusCodes.Status201Created, member);
  }
  ```

- [ ] **Step 2: Build the solution**

  ```bash
  dotnet build
  ```
  Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit backend changes**

  ```bash
  git add SaaS_BasePlatform.Application/DTOs/Tenants/TenantDtos.cs \
          SaaS_BasePlatform.Application/Services/ITenantService.cs \
          SaaS_BasePlatform.Application/Services/TenantService.cs \
          SaaS_BasePlatform.Api/Controllers/TenantsController.cs \
          SaaS_BasePlatform.Tests/Services/TenantServiceCreateUserTests.cs \
          SaaS_BasePlatform.Tests/SaaS_BasePlatform.Tests.csproj
  git commit -m "feat: add tenant user creation endpoint and service method"
  ```

---

## Task 6: Add `CreateTenantUserRequest` model (Angular)

> All steps below are in the `SaaSBasePlatform-Angular` repository.

**Files:**
- Modify: `src/app/core/models/index.ts`

- [ ] **Step 1: Add the interface**

  Open `src/app/core/models/index.ts` and add after the `AddTenantMemberRequest` interface:

  ```typescript
  export interface CreateTenantUserRequest {
    email: string;
    password: string;
    fullName: string;
    phoneNumber?: string;
    role: TenantRole;
  }
  ```

---

## Task 7: Add `createTenantUser` to `ApiService`

**Files:**
- Modify: `src/app/core/services/api.service.ts`

- [ ] **Step 1: Import the new model**

  Confirm `CreateTenantUserRequest` and `TenantMember` are imported from `@core/models` at the top of `api.service.ts`. They should already be imported via a barrel import — if not, add `CreateTenantUserRequest` to the existing import line.

- [ ] **Step 2: Add the method**

  Find the `addTenantMember` method and add the new method directly after it:

  ```typescript
  createTenantUser(tenantId: string, data: CreateTenantUserRequest): Observable<TenantMember> {
    return this.http.post<TenantMember>(`${this.baseUrl}/tenants/${tenantId}/users`, data);
  }
  ```

- [ ] **Step 3: Build to verify no TypeScript errors**

  ```bash
  ng build --configuration development 2>&1 | tail -5
  ```
  Expected: Build succeeds with 0 errors.

---

## Task 8: Extend `AddMemberDialogComponent` with create mode

**Files:**
- Modify: `src/app/modules/admin/tenant-members/add-member-dialog.component.ts`

- [ ] **Step 1: Replace the component file with the updated version**

  Open `src/app/modules/admin/tenant-members/add-member-dialog.component.ts` and replace its entire contents with:

  ```typescript
  import { Component, Inject } from '@angular/core';
  import { CommonModule } from '@angular/common';
  import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
  import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
  import { MatFormFieldModule } from '@angular/material/form-field';
  import { MatInputModule } from '@angular/material/input';
  import { MatSelectModule } from '@angular/material/select';
  import { MatButtonModule } from '@angular/material/button';
  import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
  import { MatIconModule } from '@angular/material/icon';
  import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
  import { ApiService } from '@core/services';
  import { UserLookupResult } from '@core/models';

  @Component({
    selector: 'app-add-member-dialog',
    standalone: true,
    imports: [
      CommonModule, ReactiveFormsModule,
      MatDialogModule, MatFormFieldModule, MatInputModule,
      MatSelectModule, MatButtonModule, MatProgressSpinnerModule, MatIconModule, MatSnackBarModule
    ],
    template: `
      <h2 mat-dialog-title>Adicionar Membro</h2>
      <mat-dialog-content>

        <!-- LOOKUP MODE -->
        @if (mode === 'lookup') {
          <form [formGroup]="lookupForm" (ngSubmit)="onLookup()">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>E-mail do Usuário</mat-label>
              <input matInput formControlName="email" type="email" placeholder="usuario@exemplo.com">
              @if (lookupForm.get('email')?.hasError('required')) {
                <mat-error>E-mail é obrigatório</mat-error>
              }
              @if (lookupForm.get('email')?.hasError('email')) {
                <mat-error>Informe um e-mail válido</mat-error>
              }
            </mat-form-field>

            <button mat-stroked-button type="submit"
              [disabled]="lookingUp || lookupForm.get('email')?.invalid">
              @if (lookingUp) { <mat-spinner diameter="18" style="display:inline-block"></mat-spinner> }
              Buscar
            </button>

            @if (lookupDone && !foundUser) {
              <div class="not-found-msg">Nenhum usuário ativo encontrado com esse e-mail.</div>
              <button mat-stroked-button type="button" color="primary"
                style="margin-top:8px;width:100%" (click)="onSwitchToCreate()">
                + Criar novo usuário com este e-mail
              </button>
            }

            @if (foundUser) {
              <div class="found-user">
                <mat-icon color="primary">check_circle</mat-icon>
                <span><strong>{{ foundUser.fullName || foundUser.email }}</strong> ({{ foundUser.email }})</span>
              </div>
              <mat-form-field appearance="outline" class="full-width" style="margin-top:16px">
                <mat-label>Papel</mat-label>
                <mat-select formControlName="role">
                  <mat-option [value]="0">Membro</mat-option>
                  <mat-option [value]="1">Admin</mat-option>
                </mat-select>
              </mat-form-field>
            }
          </form>
        }

        <!-- CREATE MODE -->
        @if (mode === 'create') {
          <div class="creating-label">Criando novo usuário: <strong>{{ lookupForm.value.email }}</strong></div>
          <form [formGroup]="createForm">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Nome Completo *</mat-label>
              <input matInput formControlName="fullName" placeholder="João Silva">
              @if (createForm.get('fullName')?.hasError('required') && createForm.get('fullName')?.touched) {
                <mat-error>Nome é obrigatório</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Senha *</mat-label>
              <input matInput formControlName="password" type="password">
              @if (createForm.get('password')?.hasError('required') && createForm.get('password')?.touched) {
                <mat-error>Senha é obrigatória</mat-error>
              }
              @if (createForm.get('password')?.hasError('minlength') && createForm.get('password')?.touched) {
                <mat-error>Senha deve ter ao menos 6 caracteres</mat-error>
              }
              @if (passwordError) {
                <mat-error>{{ passwordError }}</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Telefone (opcional)</mat-label>
              <input matInput formControlName="phoneNumber" placeholder="+55 11 99999-0000">
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Papel *</mat-label>
              <mat-select formControlName="role">
                <mat-option [value]="0">Membro</mat-option>
                <mat-option [value]="1">Admin</mat-option>
              </mat-select>
            </mat-form-field>
          </form>
        }

      </mat-dialog-content>

      <mat-dialog-actions align="end">
        <button mat-button (click)="onCancel()">Cancelar</button>

        @if (mode === 'lookup') {
          <button mat-flat-button color="primary"
            [disabled]="!foundUser || saving"
            (click)="onAdd()">
            @if (saving) { <mat-spinner diameter="18" style="display:inline-block"></mat-spinner> }
            Adicionar
          </button>
        }

        @if (mode === 'create') {
          <button mat-flat-button color="primary"
            [disabled]="createForm.invalid || saving"
            (click)="onCreate()">
            @if (saving) { <mat-spinner diameter="18" style="display:inline-block"></mat-spinner> }
            Criar e Adicionar
          </button>
        }
      </mat-dialog-actions>
    `,
    styles: [`
      .full-width { width: 100%; }
      .not-found-msg { color: #e53935; margin: 8px 0; font-size: 13px; }
      .found-user { display: flex; align-items: center; gap: 8px; color: #388e3c; margin: 8px 0; }
      .creating-label { font-size: 13px; color: #1565c0; margin-bottom: 16px; }
    `]
  })
  export class AddMemberDialogComponent {
    mode: 'lookup' | 'create' = 'lookup';

    lookupForm: FormGroup;
    createForm: FormGroup;

    lookingUp = false;
    lookupDone = false;
    saving = false;
    foundUser: UserLookupResult | null = null;
    passwordError: string | null = null;

    constructor(
      private fb: FormBuilder,
      private api: ApiService,
      private snack: MatSnackBar,
      private dialogRef: MatDialogRef<AddMemberDialogComponent>,
      @Inject(MAT_DIALOG_DATA) public data: { tenantId: string }
    ) {
      this.lookupForm = this.fb.group({
        email: ['', [Validators.required, Validators.email]],
        role: [0]
      });
      this.createForm = this.fb.group({
        fullName: ['', Validators.required],
        password: ['', [Validators.required, Validators.minLength(6)]],
        phoneNumber: [''],
        role: [0]
      });
    }

    onLookup(): void {
      if (this.lookupForm.get('email')?.invalid) return;
      this.lookingUp = true;
      this.lookupDone = false;
      this.foundUser = null;

      this.api.lookupUserByEmail(this.lookupForm.value.email).subscribe({
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

    onSwitchToCreate(): void {
      this.mode = 'create';
    }

    onCancel(): void {
      if (this.mode === 'create') {
        this.mode = 'lookup';
        this.createForm.reset({ role: 0 });
        this.passwordError = null;
      } else {
        this.dialogRef.close(false);
      }
    }

    onAdd(): void {
      if (!this.foundUser) return;
      this.saving = true;

      this.api.addTenantMember(this.data.tenantId, {
        userId: this.foundUser.userId,
        role: this.lookupForm.value.role
      }).subscribe({
        next: () => this.dialogRef.close(true),
        error: () => {
          this.saving = false;
          this.snack.open('Falha ao adicionar membro.', 'OK', { duration: 4000 });
        }
      });
    }

    onCreate(): void {
      if (this.createForm.invalid) return;
      this.saving = true;
      this.passwordError = null;

      const { fullName, password, phoneNumber, role } = this.createForm.value;
      this.api.createTenantUser(this.data.tenantId, {
        email: this.lookupForm.value.email,
        password,
        fullName,
        phoneNumber: phoneNumber || undefined,
        role
      }).subscribe({
        next: () => this.dialogRef.close(true),
        error: (err) => {
          this.saving = false;
          const msg = err?.error?.message || err?.error?.title || 'Falha ao criar usuário.';
          if (msg.toLowerCase().includes('password') || msg.toLowerCase().includes('senha')) {
            this.passwordError = msg;
          } else {
            this.snack.open(msg, 'OK', { duration: 5000 });
          }
        }
      });
    }
  }
  ```

- [ ] **Step 2: Build and type-check**

  ```bash
  ng build --configuration development 2>&1 | tail -10
  ```
  Expected: Build succeeds, 0 errors.

- [ ] **Step 3: Serve the app and manually test the flow**

  ```bash
  ng serve
  ```

  1. Log in as a tenant Owner or Admin.
  2. Navigate to **Admin → Tenant Members**.
  3. Click **Add Member**.
  4. Enter an email that does **not** exist in the system → click **Buscar**.
  5. Verify "Nenhum usuário ativo encontrado" and **"+ Criar novo usuário"** button appear.
  6. Click the button → verify the form expands with Full Name, Password, Phone, Role fields.
  7. Fill in the form and click **Criar e Adicionar**.
  8. Verify dialog closes and the new member appears in the list.
  9. Log out and log in as the newly created user → verify the tenant appears on the tenant selection screen.
  10. Click **Cancelar** in create mode → verify it returns to lookup mode with the email preserved.
  11. Enter a **weak password** → verify the backend error is shown under the password field.

- [ ] **Step 4: Commit Angular changes**

  ```bash
  git add src/app/core/models/index.ts \
          src/app/core/services/api.service.ts \
          src/app/modules/admin/tenant-members/add-member-dialog.component.ts
  git commit -m "feat: extend add-member dialog with inline user creation"
  ```

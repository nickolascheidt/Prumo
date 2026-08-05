# Per-Tenant RBAC Implementation Plan (#1, #6, #7)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make role assignment tenant-scoped so a user's admin/feature access applies only to the tenant where it was granted, with one global master-admin that spans all tenants, and gate module dashboards by per-tenant access.

**Architecture:** Introduce a `TenantUserRole` join table (TenantId, UserId, RoleId) that replaces global `AspNetUserRoles` for all feature roles. The global `Administrador` Identity role is reserved as the **master admin** (full access in every tenant). A **tenant admin** is a `TenantUser` with `TenantRole.Owner`/`Admin`; it grants full resource access *within that tenant* and the right to manage that tenant's members and feature-role assignments. Permission/resource reads resolve a user's *effective roles for the active tenant* (global master role ∪ per-tenant `TenantUserRole`). The JWT, already tenant-bound, emits per-tenant role + `tenant_role` claims. The Angular app gates routes/menu/dashboards by the already-tenant-scoped `my-permissions` resource map.

**Tech Stack:** .NET 10 / ASP.NET Core / EF Core (PostgreSQL via Npgsql) / xUnit + NSubstitute (backend at `C:\Users\Nickolas\source\repos\SaaSBasePlatform`); Angular 18 standalone / Karma+Jasmine (frontend at `C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular`).

---

## ⏸️ EXECUTION STATUS — paused 2026-06-17 (resume here tomorrow)

Executed subagent-driven on branch `feature/per-tenant-rbac` (backend) and `feature/per-tenant-rbac` (Angular, quick-wins only so far).

**DONE & committed (backend, all green — 57 tests pass, build clean):**
- ✅ A0–A3 persistence: `Permissions.Roles` constants, `TenantUserRole` entity + EF config + DbSet + migration `20260617234848_AddTenantUserRoles` (spec ✅, quality ✅).
- ✅ A4 backfill: `DbInitializer.BackfillTenantUserRolesAsync` + wiring; hardened against null master-role delete; tests incl. master-skip (spec ✅, quality ✅).
- ✅ B1 `TenantRoleService` (effective-role resolution, assign/revoke feature roles) + DI + tests (spec ✅, quality ✅).
- ✅ B2 `PermissionService.GetUserPermissionsForTenantAsync` uses effective roles; + cross-tenant no-leak test (spec ✅, quality ✅).
- ✅ B3 `ResourcePermissionService` tenant-admin aware (master + tenant-admin → Full; per-tenant role union; no-ambient-tenant guard; `GetEffectiveRoleIdsAsync` helper) + tests incl. non-admin per-tenant resolution & cross-tenant no-leak (spec ✅, quality ✅).
- ⚠️ B4 JWT per-tenant role + `tenant_role` claims: production code DONE & spec-compliant (commit `25b5e72`), BUT code-quality review found a **CRITICAL test-fidelity issue** — `AuthServiceTestHarness` is a hand-copied claim builder that (a) emits the role claim as `"role"` while production emits the full `ClaimTypes.Role` URI, and (b) doesn't exercise the real union/`tenant_role`/permission logic. **FIRST TASK TOMORROW: rework the test to drive the real token path** (e.g. `[InternalsVisibleTo]` + internal claim-builder seam, using the already-created-but-unused `tenantRoleService` substitute), or at minimum fix the harness claim name + assert the real URI. Production behavior is correct (bearer middleware maps the URI back via `RoleClaimType`), so this is test-quality, not a functional bug.

**REMAINING (not started):**
- ⬜ B5 per-tenant feature-role endpoints (TenantsController + DTOs).
- ⬜ B6 lock global role assignment to master; drop phantom `Usuario` (register + `CreateAndAddMemberAsync`).
- ⬜ B7 full backend test pass / regression sweep.
- ⬜ C1–C2 frontend models + AuthService master/tenant-role helpers + ApiService methods.
- ⬜ C3 repurpose users-roles screen to per-tenant assignment (the direct UI fix for #1).
- ⬜ C4 gate routes/menu/dashboards by resource access (#7).
- ⬜ C5 frontend lint/test pass.
- ⬜ D1–D2 apply migration + manual cross-tenant verification (needs a running Postgres; dev env currently torn down — see memory `saasbase-azure-env-state`).

**Frontend quick-wins (#2–#5)** are already committed on the Angular `feature/per-tenant-rbac` branch (commit `96ec7c3`) and are independent/safe.

**Process note:** executing via superpowers:subagent-driven-development — fresh implementer per task, then spec-compliance review, then code-quality review, fixing findings before closing each task.

---

## Glossary / Model decisions (read first)

- **Master admin** = user holding the global Identity role `Administrador` (row in `AspNetUserRoles`, no tenant). Bypasses all tenant scoping. Only a master admin can grant/revoke it.
- **Tenant admin** = `TenantUser.Role ∈ { Owner, Admin }` for a tenant. Full resource access *within that tenant*; may manage that tenant's members and feature-role assignments.
- **Feature roles** = `Funcionario`, `Cliente`, `RH`, `Financeiro`, `ContasAPagar`. Assigned **per tenant** via `TenantUserRole`. They drive the existing per-tenant `RolePermission` / `ResourcePermission` rows. `Administrador` is **never** a feature role and never appears in `TenantUserRole`.
- **Effective roles for a tenant** = global roles (i.e. `Administrador` when present) ∪ feature roles from `TenantUserRole` for that tenant.
- **Reserved role constant**: `"Administrador"` — referenced in several places; define once (Task B0).

### Pre-flight

- [ ] **Backend branch**

Run from `C:\Users\Nickolas\source\repos\SaaSBasePlatform`:
```bash
git checkout -b feature/per-tenant-rbac
```

- [ ] **Frontend branch**

Run from `C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular`:
```bash
git checkout -b feature/per-tenant-rbac
```

- [ ] **Baseline build (backend)**

Run: `dotnet build`
Expected: build succeeds (establishes a green baseline before changes).

---

## Phase A — Backend domain & persistence

### Task A0: Reserved role constants

**Files:**
- Modify: `SaaS_BasePlatform.Domain/Authorization/Permissions.cs`

- [ ] **Step 1: Add role-name constants**

Add this nested class inside `public static class Permissions` (after the `AccountsPayable` class, before `GetAllPermissions`):

```csharp
        /// <summary>
        /// Canonical Identity role names. <see cref="MasterAdmin"/> is global (cross-tenant);
        /// the feature roles are assigned per-tenant via TenantUserRole.
        /// </summary>
        public static class Roles
        {
            public const string MasterAdmin = "Administrador";
            public const string Funcionario = "Funcionario";
            public const string Cliente = "Cliente";
            public const string RH = "RH";
            public const string Financeiro = "Financeiro";
            public const string ContasAPagar = "ContasAPagar";

            /// <summary>Roles a tenant admin may assign to members within their tenant.</summary>
            public static readonly IReadOnlyList<string> AssignableFeatureRoles = new[]
            {
                Funcionario, Cliente, RH, Financeiro, ContasAPagar
            };
        }
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add SaaS_BasePlatform.Domain/Authorization/Permissions.cs
git commit -m "feat(rbac): add canonical role-name constants"
```

---

### Task A1: `TenantUserRole` entity

**Files:**
- Create: `SaaS_BasePlatform.Domain/Entities/TenantUserRole.cs`

- [ ] **Step 1: Create the entity**

```csharp
using SaaS_BasePlatform.Domain.Common;

namespace SaaS_BasePlatform.Domain.Entities
{
    /// <summary>
    /// Per-tenant assignment of an Identity role to a user. Replaces global
    /// AspNetUserRoles for feature roles so access is scoped to a single tenant.
    /// </summary>
    public class TenantUserRole : ITenantScoped
    {
        public Guid TenantId { get; set; }

        public Guid UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public Guid RoleId { get; set; }
        public ApplicationRole Role { get; set; } = null!;

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
        public Guid? GrantedByUserId { get; set; }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add SaaS_BasePlatform.Domain/Entities/TenantUserRole.cs
git commit -m "feat(rbac): add TenantUserRole entity"
```

---

### Task A2: EF configuration + DbSet

**Files:**
- Create: `SaaS_BasePlatform.Infrastructure/Data/Configurations/TenantUserRoleConfiguration.cs`
- Modify: `SaaS_BasePlatform.Infrastructure/Data/ApplicationDbContext.cs`

- [ ] **Step 1: Create the configuration** (mirrors `TenantUserConfiguration`)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Infrastructure.Data.Configurations
{
    public class TenantUserRoleConfiguration : IEntityTypeConfiguration<TenantUserRole>
    {
        public void Configure(EntityTypeBuilder<TenantUserRole> builder)
        {
            builder.ToTable("TenantUserRoles");

            builder.HasKey(tur => new { tur.TenantId, tur.UserId, tur.RoleId });

            builder.HasOne(tur => tur.User)
                .WithMany()
                .HasForeignKey(tur => tur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(tur => tur.Role)
                .WithMany()
                .HasForeignKey(tur => tur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(tur => new { tur.TenantId, tur.UserId });
        }
    }
}
```

> The DbContext auto-applies all `IEntityTypeConfiguration` via `ApplyConfigurationsFromAssembly` (`ApplicationDbContext.cs:55`) and auto-applies the tenant query filter because `TenantUserRole : ITenantScoped` (`ApplicationDbContext.cs:60-80`). No filter wiring needed.

- [ ] **Step 2: Register the DbSet**

In `ApplicationDbContext.cs`, after the `public DbSet<TenantUser> TenantUsers => Set<TenantUser>();` line (`:33`), add:

```csharp
        public DbSet<TenantUserRole> TenantUserRoles => Set<TenantUserRole>();
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add SaaS_BasePlatform.Infrastructure/Data/Configurations/TenantUserRoleConfiguration.cs SaaS_BasePlatform.Infrastructure/Data/ApplicationDbContext.cs
git commit -m "feat(rbac): configure TenantUserRoles table"
```

---

### Task A3: EF migration

**Files:**
- Create: `SaaS_BasePlatform.Infrastructure/Migrations/<timestamp>_AddTenantUserRoles.cs` (generated)

- [ ] **Step 1: Generate the migration**

Run from repo root:
```bash
dotnet ef migrations add AddTenantUserRoles -p SaaS_BasePlatform.Infrastructure -s SaaS_BasePlatform.Api
```
Expected: a new migration file is created under `SaaS_BasePlatform.Infrastructure/Migrations/` creating table `TenantUserRoles` with composite PK `(TenantId, UserId, RoleId)` and FKs to `AspNetUsers` / `AspNetRoles`.

- [ ] **Step 2: Inspect the migration**

Open the generated `Up()` and confirm it creates `TenantUserRoles` (not altering unrelated tables). If it contains unrelated diffs, stop and reconcile the model snapshot before continuing.

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add SaaS_BasePlatform.Infrastructure/Migrations/
git commit -m "feat(rbac): migration for TenantUserRoles"
```

---

### Task A4: Backfill global feature-role assignments into per-tenant rows

Converts existing global feature-role memberships into `TenantUserRole` rows (one per tenant the user belongs to), then removes the global feature-role assignments. `Administrador` is left global (master admins).

**Files:**
- Modify: `SaaS_BasePlatform.Infrastructure/Data/DbInitializer.cs`
- Test: `SaaS_BasePlatform.Tests/Infrastructure/TenantUserRoleBackfillTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Microsoft.EntityFrameworkCore;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;
using SaaS_BasePlatform.Infrastructure.Data;
using Xunit;

namespace SaaS_BasePlatform.Tests.Infrastructure
{
    public class TenantUserRoleBackfillTests
    {
        private static ApplicationDbContext NewDb()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task Backfill_creates_per_tenant_role_for_each_membership()
        {
            using var db = NewDb();
            var userId = Guid.NewGuid();
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();
            var rhRoleId = Guid.NewGuid();

            db.Roles.Add(new ApplicationRole { Id = rhRoleId, Name = "RH", NormalizedName = "RH" });
            db.TenantUsers.Add(new TenantUser { TenantId = tenantA, UserId = userId, Role = TenantRole.Member });
            db.TenantUsers.Add(new TenantUser { TenantId = tenantB, UserId = userId, Role = TenantRole.Member });
            db.Set<IdentityUserRoleSeed>(); // placeholder; see Step 3 note
            await db.SaveChangesAsync();

            // Simulate a legacy global assignment row.
            db.Database.GetType(); // no-op anchor
            await DbInitializer.BackfillTenantUserRolesAsync(
                db,
                globalAssignments: new[] { (userId, rhRoleId, "RH") });

            var rows = await db.TenantUserRoles.ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Equal(rhRoleId, r.RoleId));
            Assert.Contains(rows, r => r.TenantId == tenantA);
            Assert.Contains(rows, r => r.TenantId == tenantB);
        }

        [Fact]
        public async Task Backfill_is_idempotent()
        {
            using var db = NewDb();
            var userId = Guid.NewGuid();
            var tenant = Guid.NewGuid();
            var roleId = Guid.NewGuid();
            db.Roles.Add(new ApplicationRole { Id = roleId, Name = "RH", NormalizedName = "RH" });
            db.TenantUsers.Add(new TenantUser { TenantId = tenant, UserId = userId, Role = TenantRole.Member });
            await db.SaveChangesAsync();

            var assignments = new[] { (userId, roleId, "RH") };
            await DbInitializer.BackfillTenantUserRolesAsync(db, assignments);
            await DbInitializer.BackfillTenantUserRolesAsync(db, assignments);

            Assert.Equal(1, await db.TenantUserRoles.CountAsync());
        }
    }
}
```

> Note: `IdentityUserRoleSeed` line above is illustrative only — delete it. The test calls a **pure, injectable** backfill helper `BackfillTenantUserRolesAsync(db, globalAssignments)` so it does not depend on Identity stores. The production `InitializeAsync` reads the real global assignments from `db.UserRoles` and calls this helper (Step 3).

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~TenantUserRoleBackfillTests"`
Expected: FAIL — `DbInitializer.BackfillTenantUserRolesAsync` does not exist.

- [ ] **Step 3: Implement the backfill helper + wire it in**

In `DbInitializer.cs`, add this public helper:

```csharp
        /// <summary>
        /// For each (userId, roleId) global feature-role assignment, create a
        /// per-tenant TenantUserRole for every tenant the user belongs to.
        /// Idempotent. Does not touch the master-admin role.
        /// </summary>
        public static async Task BackfillTenantUserRolesAsync(
            ApplicationDbContext context,
            IReadOnlyCollection<(Guid UserId, Guid RoleId, string RoleName)> globalAssignments,
            CancellationToken cancellationToken = default)
        {
            foreach (var (userId, roleId, roleName) in globalAssignments)
            {
                if (roleName == Domain.Authorization.Permissions.Roles.MasterAdmin)
                    continue;

                var tenantIds = await context.TenantUsers
                    .IgnoreQueryFilters()
                    .Where(tu => tu.UserId == userId)
                    .Select(tu => tu.TenantId)
                    .ToListAsync(cancellationToken);

                foreach (var tenantId in tenantIds)
                {
                    var exists = await context.TenantUserRoles
                        .IgnoreQueryFilters()
                        .AnyAsync(tur => tur.TenantId == tenantId
                                      && tur.UserId == userId
                                      && tur.RoleId == roleId, cancellationToken);
                    if (!exists)
                    {
                        context.TenantUserRoles.Add(new TenantUserRole
                        {
                            TenantId = tenantId,
                            UserId = userId,
                            RoleId = roleId,
                            GrantedAt = DateTime.UtcNow
                        });
                    }
                }
            }
            await context.SaveChangesAsync(cancellationToken);
        }
```

Then call it once during init. In `InitializeAsync`, immediately **before** the `// Verificar se já existe o admin` block (`DbInitializer.cs:116`), add:

```csharp
                // Backfill: convert legacy global feature-role assignments to per-tenant rows.
                var masterRoleId = (await roleManager.FindByNameAsync(Permissions.Roles.MasterAdmin))?.Id;
                var globalAssignments = await (
                    from ur in context.UserRoles
                    join r in context.Roles on ur.RoleId equals r.Id
                    where r.Id != masterRoleId
                    select new { ur.UserId, r.Id, r.Name }
                ).ToListAsync();

                if (globalAssignments.Count > 0)
                {
                    await BackfillTenantUserRolesAsync(
                        context,
                        globalAssignments.Select(a => (a.UserId, a.Id, a.Name!)).ToList());

                    // Remove the now-migrated global feature-role assignments.
                    var toRemove = await context.UserRoles
                        .Where(ur => ur.RoleId != masterRoleId)
                        .ToListAsync();
                    context.UserRoles.RemoveRange(toRemove);
                    await context.SaveChangesAsync();
                    logger.LogInformation("✓ Backfilled {Count} per-tenant role rows", globalAssignments.Count);
                }
```

Add `using SaaS_BasePlatform.Domain.Authorization;` to the file's usings if not present (it already imports it at `:1`).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~TenantUserRoleBackfillTests"`
Expected: PASS.

- [ ] **Step 5: Build + commit**

```bash
dotnet build
git add SaaS_BasePlatform.Infrastructure/Data/DbInitializer.cs SaaS_BasePlatform.Tests/Infrastructure/TenantUserRoleBackfillTests.cs
git commit -m "feat(rbac): backfill global feature roles into per-tenant rows"
```

---

## Phase B — Backend role resolution, authorization & endpoints

### Task B1: Effective-role resolution service

Single source of truth for "which role names does this user have in this tenant".

**Files:**
- Create: `SaaS_BasePlatform.Application/Services/ITenantRoleService.cs`
- Create: `SaaS_BasePlatform.Application/Services/TenantRoleService.cs`
- Modify: `SaaS_BasePlatform.Api/Configuration/DependencyInjectionConfiguration.cs`
- Test: `SaaS_BasePlatform.Tests/Services/TenantRoleServiceTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Microsoft.EntityFrameworkCore;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;
using SaaS_BasePlatform.Infrastructure.Data;
using Xunit;

namespace SaaS_BasePlatform.Tests.Services
{
    public class TenantRoleServiceTests
    {
        private static ApplicationDbContext NewDb() =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        [Fact]
        public async Task Effective_roles_include_per_tenant_feature_roles_only_for_that_tenant()
        {
            using var db = NewDb();
            var user = Guid.NewGuid();
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();
            var rh = new ApplicationRole { Id = Guid.NewGuid(), Name = "RH", NormalizedName = "RH" };
            db.Roles.Add(rh);
            db.TenantUserRoles.Add(new TenantUserRole { TenantId = tenantA, UserId = user, RoleId = rh.Id });
            await db.SaveChangesAsync();

            var sut = new TenantRoleService(db);

            var inA = await sut.GetEffectiveRoleNamesAsync(user, tenantA, globalRoleNames: Array.Empty<string>());
            var inB = await sut.GetEffectiveRoleNamesAsync(user, tenantB, globalRoleNames: Array.Empty<string>());

            Assert.Contains("RH", inA);
            Assert.DoesNotContain("RH", inB);
        }

        [Fact]
        public async Task Effective_roles_always_include_global_master_role()
        {
            using var db = NewDb();
            var user = Guid.NewGuid();
            var tenant = Guid.NewGuid();
            var sut = new TenantRoleService(db);

            var roles = await sut.GetEffectiveRoleNamesAsync(
                user, tenant, globalRoleNames: new[] { "Administrador" });

            Assert.Contains("Administrador", roles);
        }

        [Fact]
        public async Task AssignFeatureRole_rejects_master_admin_role()
        {
            using var db = NewDb();
            var sut = new TenantRoleService(db);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.AssignFeatureRoleAsync(Guid.NewGuid(), Guid.NewGuid(), "Administrador"));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~TenantRoleServiceTests"`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Create the interface**

`ITenantRoleService.cs`:

```csharp
namespace SaaS_BasePlatform.Application.Services
{
    public interface ITenantRoleService
    {
        /// <summary>Global roles (e.g. master Administrador) ∪ per-tenant feature roles.</summary>
        Task<IReadOnlyList<string>> GetEffectiveRoleNamesAsync(
            Guid userId, Guid tenantId, IReadOnlyCollection<string> globalRoleNames,
            CancellationToken ct = default);

        /// <summary>Per-tenant feature role names for a user (no global roles).</summary>
        Task<IReadOnlyList<string>> GetTenantRoleNamesAsync(
            Guid userId, Guid tenantId, CancellationToken ct = default);

        Task AssignFeatureRoleAsync(Guid tenantId, Guid userId, string roleName,
            Guid? grantedByUserId = null, CancellationToken ct = default);

        Task RevokeFeatureRoleAsync(Guid tenantId, Guid userId, string roleName,
            CancellationToken ct = default);
    }
}
```

- [ ] **Step 4: Create the implementation**

`TenantRoleService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using SaaS_BasePlatform.Domain.Authorization;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Infrastructure.Data;

namespace SaaS_BasePlatform.Application.Services
{
    public class TenantRoleService : ITenantRoleService
    {
        private readonly ApplicationDbContext _db;

        public TenantRoleService(ApplicationDbContext db) => _db = db;

        public async Task<IReadOnlyList<string>> GetTenantRoleNamesAsync(
            Guid userId, Guid tenantId, CancellationToken ct = default)
        {
            return await _db.TenantUserRoles
                .IgnoreQueryFilters()
                .Where(tur => tur.TenantId == tenantId && tur.UserId == userId)
                .Select(tur => tur.Role.Name!)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<string>> GetEffectiveRoleNamesAsync(
            Guid userId, Guid tenantId, IReadOnlyCollection<string> globalRoleNames,
            CancellationToken ct = default)
        {
            var tenantRoles = await GetTenantRoleNamesAsync(userId, tenantId, ct);
            return globalRoleNames.Concat(tenantRoles).Distinct().ToList();
        }

        public async Task AssignFeatureRoleAsync(
            Guid tenantId, Guid userId, string roleName,
            Guid? grantedByUserId = null, CancellationToken ct = default)
        {
            if (!Permissions.Roles.AssignableFeatureRoles.Contains(roleName))
                throw new InvalidOperationException(
                    $"'{roleName}' is not an assignable tenant feature role.");

            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct)
                ?? throw new InvalidOperationException($"Role '{roleName}' does not exist.");

            var exists = await _db.TenantUserRoles
                .IgnoreQueryFilters()
                .AnyAsync(t => t.TenantId == tenantId && t.UserId == userId && t.RoleId == role.Id, ct);
            if (exists) return;

            _db.TenantUserRoles.Add(new TenantUserRole
            {
                TenantId = tenantId,
                UserId = userId,
                RoleId = role.Id,
                GrantedByUserId = grantedByUserId,
                GrantedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }

        public async Task RevokeFeatureRoleAsync(
            Guid tenantId, Guid userId, string roleName, CancellationToken ct = default)
        {
            var row = await _db.TenantUserRoles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId
                                       && t.UserId == userId
                                       && t.Role.Name == roleName, ct);
            if (row == null) return;
            _db.TenantUserRoles.Remove(row);
            await _db.SaveChangesAsync(ct);
        }
    }
}
```

- [ ] **Step 5: Register the service**

In `DependencyInjectionConfiguration.cs`, after `services.AddScoped<ITenantService, TenantService>();` (`:28`), add:

```csharp
        services.AddScoped<ITenantRoleService, TenantRoleService>();
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~TenantRoleServiceTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add SaaS_BasePlatform.Application/Services/ITenantRoleService.cs SaaS_BasePlatform.Application/Services/TenantRoleService.cs SaaS_BasePlatform.Api/Configuration/DependencyInjectionConfiguration.cs SaaS_BasePlatform.Tests/Services/TenantRoleServiceTests.cs
git commit -m "feat(rbac): tenant-scoped role resolution service"
```

---

### Task B2: Tenant-scoped module permissions use effective roles

`PermissionService.GetUserPermissionsForTenantAsync` currently joins the user's **global** roles. Switch it to effective roles.

**Files:**
- Modify: `SaaS_BasePlatform.Infrastructure/Authorization/PermissionService.cs`
- Test: `SaaS_BasePlatform.Tests/Services/PermissionServiceTenantTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Microsoft.EntityFrameworkCore;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Infrastructure.Authorization;
using SaaS_BasePlatform.Infrastructure.Data;
using Xunit;

namespace SaaS_BasePlatform.Tests.Services
{
    public class PermissionServiceTenantTests
    {
        private static ApplicationDbContext NewDb() =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        [Fact]
        public async Task Permissions_resolve_from_tenant_role_assignment()
        {
            using var db = NewDb();
            var user = Guid.NewGuid();
            var tenant = Guid.NewGuid();
            var rh = new ApplicationRole { Id = Guid.NewGuid(), Name = "RH", NormalizedName = "RH" };
            var perm = new Permission { Id = Guid.NewGuid(), Name = "employees.view" };
            db.Roles.Add(rh);
            db.Permissions.Add(perm);
            db.RolePermissions.Add(new RolePermission { TenantId = tenant, RoleId = rh.Id, PermissionId = perm.Id, GrantedAt = DateTime.UtcNow });
            db.TenantUserRoles.Add(new TenantUserRole { TenantId = tenant, UserId = user, RoleId = rh.Id });
            await db.SaveChangesAsync();

            var sut = new PermissionService(db, /* userManager */ null!, /* roleManager */ null!);
            var perms = await sut.GetUserPermissionsForTenantAsync(user, tenant);

            Assert.Contains("employees.view", perms);
        }
    }
}
```

> The method must no longer call `UserManager`. Verify the implementation does not dereference `_userManager`/`_roleManager` on this path (the test passes `null!` for both).

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~PermissionServiceTenantTests"`
Expected: FAIL — current implementation calls `_userManager.FindByIdAsync` → `NullReferenceException`.

- [ ] **Step 3: Rewrite `GetUserPermissionsForTenantAsync`**

Replace the body of `GetUserPermissionsForTenantAsync` (`PermissionService.cs:59-75`) with a query that resolves roles from `TenantUserRoles` plus the user's global roles, without `UserManager`:

```csharp
        public async Task<IReadOnlyCollection<string>> GetUserPermissionsForTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
        {
            // Effective role IDs for this tenant: global Identity roles (master admin) ∪ per-tenant feature roles.
            var globalRoleIds = await _context.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId)
                .ToListAsync(cancellationToken);

            var tenantRoleIds = await _context.TenantUserRoles
                .IgnoreQueryFilters()
                .Where(tur => tur.TenantId == tenantId && tur.UserId == userId)
                .Select(tur => tur.RoleId)
                .ToListAsync(cancellationToken);

            var roleIds = globalRoleIds.Concat(tenantRoleIds).Distinct().ToList();
            if (roleIds.Count == 0) return Array.Empty<string>();

            return await _context.RolePermissions
                .IgnoreQueryFilters()
                .Where(rp => rp.TenantId == tenantId && roleIds.Contains(rp.RoleId))
                .Select(rp => rp.Permission.Name)
                .Distinct()
                .ToListAsync(cancellationToken);
        }
```

> `_context.UserRoles` is the Identity `AspNetUserRoles` set exposed by `IdentityDbContext`. No `UserManager` needed.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~PermissionServiceTenantTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add SaaS_BasePlatform.Infrastructure/Authorization/PermissionService.cs SaaS_BasePlatform.Tests/Services/PermissionServiceTenantTests.cs
git commit -m "feat(rbac): module permissions resolve from per-tenant roles"
```

---

### Task B3: Resource permissions are tenant-role aware

`ResourcePermissionService` must: (a) treat global master `Administrador` as Full everywhere; (b) treat tenant `Owner`/`Admin` as Full within the active tenant; (c) otherwise resolve effective per-tenant roles. It must use the ambient `ITenantContext`.

**Files:**
- Modify: `SaaS_BasePlatform.Application/Services/ResourcePermissionService.cs`
- Test: `SaaS_BasePlatform.Tests/Services/ResourcePermissionTenantTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Common;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;
using SaaS_BasePlatform.Infrastructure.Data;
using Xunit;

namespace SaaS_BasePlatform.Tests.Services
{
    public class ResourcePermissionTenantTests
    {
        private static ApplicationDbContext NewDb(ITenantContext ctx) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, ctx);

        [Fact]
        public async Task Tenant_owner_has_full_access_within_tenant()
        {
            var tenant = Guid.NewGuid();
            var ctx = Substitute.For<ITenantContext>();
            ctx.TenantId.Returns(tenant);
            ctx.HasTenant.Returns(true);
            using var db = NewDb(ctx);

            var user = Guid.NewGuid();
            db.Resources.Add(new Resource { Id = Guid.NewGuid(), TenantId = tenant, Code = "HR.Employees", Name = "x", Module = "RH", IsActive = true });
            db.TenantUsers.Add(new TenantUser { TenantId = tenant, UserId = user, Role = TenantRole.Owner });
            await db.SaveChangesAsync();

            var sut = new ResourcePermissionService(db, MockUserManager(user), ctx);
            var level = await sut.GetUserPermissionForResourceAsync(user, "HR.Employees");

            Assert.Equal(PermissionLevel.Full, level);
        }
    }
}
```

> `MockUserManager(user)` returns a `UserManager<ApplicationUser>` substitute whose `FindByIdAsync` returns a user with **no** global roles (`GetRolesAsync` → empty). Reuse the existing UserManager-mock helper pattern from `TenantServiceCreateUserTests.cs` (NSubstitute). If none is shared, inline it in this test file.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ResourcePermissionTenantTests"`
Expected: FAIL — `ResourcePermissionService` has no `ITenantContext` constructor parameter.

- [ ] **Step 3: Add `ITenantContext` dependency**

In `ResourcePermissionService.cs`, add the field and constructor parameter:

```csharp
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITenantContext _tenantContext;

        public ResourcePermissionService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITenantContext tenantContext)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
        }
```

Add `using SaaS_BasePlatform.Domain.Common;` at the top.

- [ ] **Step 4: Add the tenant-admin short-circuit helper**

Add this private method:

```csharp
        private async Task<bool> IsTenantAdminAsync(Guid userId)
        {
            if (!_tenantContext.HasTenant) return false;
            var tenantId = _tenantContext.TenantId!.Value;
            var role = await _context.TenantUsers
                .IgnoreQueryFilters()
                .Where(tu => tu.TenantId == tenantId && tu.UserId == userId)
                .Select(tu => (TenantRole?)tu.Role)
                .FirstOrDefaultAsync();
            return role is TenantRole.Owner or TenantRole.Admin;
        }
```

- [ ] **Step 5: Use it in `GetUserPermissionForResourceAsync`**

In `GetUserPermissionForResourceAsync`, after the master-admin check (`ResourcePermissionService.cs:267`, `if (userRoles.Contains("Administrador")) return PermissionLevel.Full;`) add:

```csharp
            if (await IsTenantAdminAsync(userId))
                return PermissionLevel.Full;
```

Then replace the role-id resolution (`:270-273`) so it includes per-tenant roles:

```csharp
            var globalRoleIds = await _context.Roles
                .Where(r => userRoles.Contains(r.Name!))
                .Select(r => r.Id)
                .ToListAsync();

            var tenantRoleIds = _tenantContext.HasTenant
                ? await _context.TenantUserRoles
                    .IgnoreQueryFilters()
                    .Where(tur => tur.TenantId == _tenantContext.TenantId!.Value && tur.UserId == userId)
                    .Select(tur => tur.RoleId)
                    .ToListAsync()
                : new List<Guid>();

            var roleIds = globalRoleIds.Concat(tenantRoleIds).Distinct().ToList();
```

- [ ] **Step 6: Mirror the same logic in `BuildUserPermissionsDto`**

In `BuildUserPermissionsDto`, keep the master-admin branch (`:297`). Immediately after it add a tenant-admin branch that returns Full over all active resources in the current tenant:

```csharp
            if (await IsTenantAdminAsync(user.Id))
            {
                var tenantResources = await _context.Resources
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.Module).ThenBy(r => r.DisplayOrder)
                    .ToListAsync();

                var allowed = tenantResources.Select(r => new ResourceDto
                {
                    Id = r.Id, Code = r.Code, Name = r.Name, Description = r.Description,
                    Module = r.Module, FrontendRoute = r.FrontendRoute, Icon = r.Icon,
                    DisplayOrder = r.DisplayOrder, UserPermissionLevel = PermissionLevel.Full
                }).ToList();

                return new UserPermissionsDto
                {
                    UserId = user.Id, Email = user.Email ?? "", FullName = user.FullName,
                    Roles = userRoles.ToList(), AllowedResources = allowed,
                    ResourcePermissions = allowed.ToDictionary(r => r.Code, _ => PermissionLevel.Full)
                };
            }
```

> `_context.Resources` is auto-filtered to the current tenant, so "all active resources" already means "in this tenant".

Then replace the `roleIds` resolution (`:329-332`) with the same global∪tenant union used in Step 5 (use `user.Id` instead of `userId`).

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~ResourcePermissionTenantTests"`
Expected: PASS.

- [ ] **Step 8: Build + commit**

```bash
dotnet build
git add SaaS_BasePlatform.Application/Services/ResourcePermissionService.cs SaaS_BasePlatform.Tests/Services/ResourcePermissionTenantTests.cs
git commit -m "feat(rbac): resource access honors tenant admin + per-tenant roles"
```

---

### Task B4: JWT emits per-tenant role + tenant_role claims

**Files:**
- Modify: `SaaS_BasePlatform.Application/Services/AuthService.cs`
- Test: `SaaS_BasePlatform.Tests/Services/AuthServiceTokenTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Entities;
using Xunit;

namespace SaaS_BasePlatform.Tests.Services
{
    public class AuthServiceTokenTests
    {
        [Fact]
        public async Task Token_for_tenant_includes_tenant_feature_roles_not_global()
        {
            // Arrange a user with NO global roles but RH in the selected tenant.
            var tenantId = Guid.NewGuid();
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "u@x.com", Email = "u@x.com" };

            var tenantRoleService = Substitute.For<ITenantRoleService>();
            tenantRoleService.GetTenantRoleNamesAsync(user.Id, tenantId, Arg.Any<CancellationToken>())
                .Returns(new List<string> { "RH" });

            var token = await AuthServiceTestHarness.GenerateTokenAsync(
                user, tenantId, globalRoles: Array.Empty<string>(),
                tenantPermissions: new[] { "employees.view" },
                tenantRole: "Admin");

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "RH");
            Assert.Contains(jwt.Claims, c => c.Type == "tenant_role" && c.Value == "Admin");
            Assert.Contains(jwt.Claims, c => c.Type == "permission" && c.Value == "employees.view");
        }
    }
}
```

> `AuthServiceTestHarness.GenerateTokenAsync` is a thin static test helper you create in the test project that builds the claim list exactly as `AuthService.GenerateJwtToken` does and signs it with a fixed test key. Its purpose is to lock the **claim shape**. (Claim type `"role"` is the short form `ClaimTypes.Role` serializes to in the JWT.) Place it in `SaaS_BasePlatform.Tests/Services/AuthServiceTestHarness.cs`; copy the final claim-building block from Step 3 into it.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~AuthServiceTokenTests"`
Expected: FAIL — helper/claims not present.

- [ ] **Step 3: Update `GenerateJwtToken`**

Inject `ITenantRoleService` into `AuthService` (add field + constructor param, register is already done in DI since the service is registered). Then change `GenerateJwtToken` (`AuthService.cs:239-280`) so that when a tenant is selected it emits **effective** role claims and a `tenant_role` claim:

```csharp
        private async Task<string> GenerateJwtToken(ApplicationUser user, Guid? tenantId)
        {
            var globalRoles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.UserName!),
                new(ClaimTypes.Email, user.Email!),
                new("FullName", user.FullName ?? string.Empty)
            };

            IEnumerable<string> roleClaims = globalRoles;

            if (tenantId.HasValue)
            {
                claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));

                // Effective roles = global (master) ∪ per-tenant feature roles.
                roleClaims = await _tenantRoleService.GetEffectiveRoleNamesAsync(
                    user.Id, tenantId.Value, globalRoles);

                var tenantRole = await _tenantService.GetUserRoleAsync(tenantId.Value, user.Id);
                if (tenantRole.HasValue)
                    claims.Add(new Claim("tenant_role", tenantRole.Value.ToString()));

                var permissions = await _permissionService.GetUserPermissionsForTenantAsync(user.Id, tenantId.Value);
                claims.AddRange(permissions.Select(p => new Claim("permission", p)));
            }

            claims.AddRange(roleClaims.Distinct().Select(r => new Claim(ClaimTypes.Role, r)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key não configurada")));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(TokenExpirationHours),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
```

Add the constructor field:
```csharp
        private readonly ITenantRoleService _tenantRoleService;
```
and the parameter (append to the existing constructor signature and assignment).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~AuthServiceTokenTests"`
Expected: PASS.

- [ ] **Step 5: Build + commit**

```bash
dotnet build
git add SaaS_BasePlatform.Application/Services/AuthService.cs SaaS_BasePlatform.Tests/Services/AuthServiceTokenTests.cs SaaS_BasePlatform.Tests/Services/AuthServiceTestHarness.cs
git commit -m "feat(rbac): JWT carries per-tenant roles and tenant_role"
```

---

### Task B5: Per-tenant feature-role endpoints

**Files:**
- Modify: `SaaS_BasePlatform.Application/DTOs/Tenants/TenantDtos.cs`
- Modify: `SaaS_BasePlatform.Api/Controllers/TenantsController.cs`
- Test: `SaaS_BasePlatform.Tests/Controllers/TenantRoleEndpointsTests.cs` (optional integration-style; minimum: service covered by B1)

- [ ] **Step 1: Add DTOs**

Append to `TenantDtos.cs`:

```csharp
    public record TenantMemberRolesDto(Guid UserId, IReadOnlyList<string> Roles);
    public record AssignFeatureRoleDto(string RoleName);
```

- [ ] **Step 2: Add endpoints to `TenantsController`**

Inject `ITenantRoleService` (add field + constructor param). Add these actions:

```csharp
        [HttpGet("{tenantId:guid}/assignable-roles")]
        [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
        public ActionResult<IReadOnlyList<string>> GetAssignableRoles(Guid tenantId)
            => Ok(SaaS_BasePlatform.Domain.Authorization.Permissions.Roles.AssignableFeatureRoles);

        [HttpGet("{tenantId:guid}/members/{userId:guid}/roles")]
        [ProducesResponseType(typeof(TenantMemberRolesDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<TenantMemberRolesDto>> GetMemberRoles(
            Guid tenantId, Guid userId, CancellationToken ct)
        {
            if (!await _tenantService.IsMemberAsync(tenantId, CurrentUserId, ct)) return Forbid();
            var roles = await _tenantRoleService.GetTenantRoleNamesAsync(userId, tenantId, ct);
            return Ok(new TenantMemberRolesDto(userId, roles));
        }

        [HttpPost("{tenantId:guid}/members/{userId:guid}/roles")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AssignMemberRole(
            Guid tenantId, Guid userId, [FromBody] AssignFeatureRoleDto request, CancellationToken ct)
        {
            var callerRole = await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);
            if (callerRole is not (TenantRole.Owner or TenantRole.Admin)) return Forbid();
            if (!await _tenantService.IsMemberAsync(tenantId, userId, ct))
                return BadRequest(new { message = "User is not a member of this tenant." });

            await _tenantRoleService.AssignFeatureRoleAsync(tenantId, userId, request.RoleName, CurrentUserId, ct);
            return NoContent();
        }

        [HttpDelete("{tenantId:guid}/members/{userId:guid}/roles/{roleName}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RevokeMemberRole(
            Guid tenantId, Guid userId, string roleName, CancellationToken ct)
        {
            var callerRole = await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);
            if (callerRole is not (TenantRole.Owner or TenantRole.Admin)) return Forbid();

            await _tenantRoleService.RevokeFeatureRoleAsync(tenantId, userId, roleName, ct);
            return NoContent();
        }
```

> Authorization is enforced in-controller via `GetUserRoleAsync` (matching the existing pattern at `TenantsController.cs:80-84`). `AssignFeatureRoleAsync` rejects the master role (Task B1), so a tenant admin can never grant `Administrador`.

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add SaaS_BasePlatform.Application/DTOs/Tenants/TenantDtos.cs SaaS_BasePlatform.Api/Controllers/TenantsController.cs
git commit -m "feat(rbac): per-tenant feature-role assignment endpoints"
```

---

### Task B6: Lock global role assignment to master + drop phantom "Usuario"

`AuthController.AssignRoleToUser` / `register` currently assign global Identity roles. Restrict assignment to the master role only, and stop assigning the non-existent `Usuario`.

**Files:**
- Modify: `SaaS_BasePlatform.Application/Services/AuthService.cs`
- Modify: `SaaS_BasePlatform.Api/Controllers/AuthController.cs`
- Modify: `SaaS_BasePlatform.Application/Services/TenantService.cs`
- Test: `SaaS_BasePlatform.Tests/Services/AuthServiceRoleGuardTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using NSubstitute;
using SaaS_BasePlatform.Application.Services;
using Xunit;

namespace SaaS_BasePlatform.Tests.Services
{
    public class AuthServiceRoleGuardTests
    {
        [Fact]
        public async Task AssignRole_rejects_non_master_global_role()
        {
            var sut = AuthServiceTestFactory.Create(out _); // existing/added factory wiring UserManager mocks
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.AssignRoleToUserAsync(Guid.NewGuid(), "RH"));
        }
    }
}
```

> If no `AuthServiceTestFactory` exists, inline the NSubstitute UserManager/SignInManager setup in the test (follow `TenantServiceCreateUserTests.cs`). The assertion only needs the guard to throw before any store call.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~AuthServiceRoleGuardTests"`
Expected: FAIL — currently any role name is accepted.

- [ ] **Step 3: Guard `AssignRoleToUserAsync`**

At the top of `AuthService.AssignRoleToUserAsync` (`AuthService.cs:169`), before any work:

```csharp
            if (roleName != Domain.Authorization.Permissions.Roles.MasterAdmin)
                throw new InvalidOperationException(
                    "Only the master admin role can be assigned globally. Use per-tenant role assignment for feature roles.");
```

Add `using SaaS_BasePlatform.Domain.Authorization;` (or use the fully-qualified name as shown).

- [ ] **Step 4: Register users without the phantom global role**

In `AuthService.RegisterAsync` (`:101`), the `roleName` parameter comes from the controller. Change `AuthController.Register` (`AuthController.cs:49`) to register **without** a global role:

```csharp
            var response = await _authService.RegisterAsync(request, roleName: null, cancellationToken);
```

In `RegisterAsync`, make the role optional and skip assignment when null (replace `AuthService.cs:124-125`):

```csharp
            // Self-registration grants no global role; tenant feature roles are assigned per-tenant.
            if (!string.IsNullOrWhiteSpace(roleName))
                await _userManager.AddToRoleAsync(user, roleName);
```

Change the signature to `RegisterAsync(RegisterRequestDto request, string? roleName, ...)` in both `IAuthService` and `AuthService`.

- [ ] **Step 5: Fix `TenantService.CreateAndAddMemberAsync`**

It currently does `AddToRoleAsync(user, "Usuario")` (`TenantService.cs:168`) — a non-existent global role. Remove that block and instead assign the tenant feature role via the new service. Inject `ITenantRoleService` into `TenantService` and replace lines `168-173` with:

```csharp
            await _db.SaveChangesAsync(ct); // persist the user first (TenantUser added below)
            _db.TenantUsers.Add(new TenantUser { TenantId = tenantId, UserId = user.Id, Role = dto.Role });
            await _db.SaveChangesAsync(ct);
```

Remove the now-duplicate `TenantUsers.Add` + final `SaveChangesAsync` that followed (`:175-176`). (Net effect: the created user becomes a tenant member with the given `TenantRole`; feature roles, if any, are granted afterward through the Task B5 endpoint.)

- [ ] **Step 6: Update the `CreateAndAddMemberAsync` test**

`TenantServiceCreateUserTests.cs:72,113,126` assert `AddToRoleAsync(..., "Usuario")`. Remove those assertions and instead assert a `TenantUser` row was added with `dto.Role`. Concretely, delete the three `AddToRoleAsync` lines and add to the success test:

```csharp
        Assert.Equal(1, db.TenantUsers.IgnoreQueryFilters().Count(tu => tu.TenantId == tenantId));
```

- [ ] **Step 7: Run tests**

Run: `dotnet test --filter "FullyQualifiedName~AuthServiceRoleGuardTests|FullyQualifiedName~TenantServiceCreateUserTests"`
Expected: PASS.

- [ ] **Step 8: Build + commit**

```bash
dotnet build
git add -A
git commit -m "feat(rbac): restrict global role assignment to master; drop phantom Usuario role"
```

---

### Task B7: Full backend test pass

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test`
Expected: all green. Fix regressions (likely in any test that constructed `ResourcePermissionService` with the old 2-arg constructor — update them to pass an `ITenantContext` substitute).

- [ ] **Step 2: Commit any fixes**

```bash
git add -A
git commit -m "test(rbac): update suites for tenant-aware services"
```

---

## Phase C — Frontend (Angular)

### Task C1: Models + AuthService master/tenant-role state

**Files:**
- Modify: `src/app/core/models/index.ts`
- Modify: `src/app/core/services/auth.service.ts`

- [ ] **Step 1: Add models**

In `models/index.ts`, extend `AuthResponse` and add helpers:

```typescript
export interface AuthResponse {
  token: string;
  expiresAt: string;
  user: User;
  tenantId?: string | null;
}

export interface TenantMemberRoles {
  userId: string;
  roles: string[];
}

export interface AssignFeatureRoleRequest {
  roleName: string;
}
```

- [ ] **Step 2: Decode tenant_role + master flag from the JWT**

Add to `AuthService` a helper that reads claims from the stored token (the token already carries `tenant_role` and role claims). Add:

```typescript
  private decodeToken(): any | null {
    const token = this.getToken();
    if (!token) return null;
    try {
      const payload = token.split('.')[1];
      return JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
    } catch {
      return null;
    }
  }

  isMasterAdmin(): boolean {
    return this.hasRole('Administrador');
  }

  getTenantRole(): 'Owner' | 'Admin' | 'Member' | null {
    const claims = this.decodeToken();
    return claims?.['tenant_role'] ?? null;
  }

  isTenantAdmin(): boolean {
    const r = this.getTenantRole();
    return r === 'Owner' || r === 'Admin';
  }
```

> `hasRole` already reads `currentUser.roles`, which `refreshCurrentUser()` populates from `/api/auth/me`. Since role claims are now per-tenant, `roles` reflects the active tenant.

- [ ] **Step 3: Build**

Run: `npm run build`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/app/core/models/index.ts src/app/core/services/auth.service.ts
git commit -m "feat(rbac): expose master-admin and tenant-role helpers"
```

---

### Task C2: ApiService — per-tenant role methods

**Files:**
- Modify: `src/app/core/services/api.service.ts`

- [ ] **Step 1: Add methods** (after `updateMemberRole`, `api.service.ts:266`)

```typescript
  getAssignableTenantRoles(tenantId: string): Observable<string[]> {
    return this.http.get<string[]>(
      `${this.apiUrl}/tenants/${encodeURIComponent(tenantId)}/assignable-roles`);
  }

  getMemberFeatureRoles(tenantId: string, userId: string): Observable<TenantMemberRoles> {
    return this.http.get<TenantMemberRoles>(
      `${this.apiUrl}/tenants/${encodeURIComponent(tenantId)}/members/${encodeURIComponent(userId)}/roles`);
  }

  assignMemberFeatureRole(tenantId: string, userId: string, body: AssignFeatureRoleRequest): Observable<void> {
    return this.http.post<void>(
      `${this.apiUrl}/tenants/${encodeURIComponent(tenantId)}/members/${encodeURIComponent(userId)}/roles`, body);
  }

  revokeMemberFeatureRole(tenantId: string, userId: string, roleName: string): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/tenants/${encodeURIComponent(tenantId)}/members/${encodeURIComponent(userId)}/roles/${encodeURIComponent(roleName)}`);
  }
```

Add `TenantMemberRoles, AssignFeatureRoleRequest` to the existing `@core/models` import at the top of the file.

- [ ] **Step 2: Build + commit**

```bash
npm run build
git add src/app/core/services/api.service.ts
git commit -m "feat(rbac): API methods for per-tenant feature roles"
```

---

### Task C3: Repurpose users-roles screen to per-tenant feature roles

The `/admin/users-roles` screen currently assigns global Identity roles (root cause of #1). Switch it to use the per-tenant endpoints.

**Files:**
- Modify: `src/app/modules/admin/users-roles-management.component.ts`

- [ ] **Step 1: Replace the assignable list + load/assign/remove calls**

- Replace `availableRoles` with a list loaded from the backend in `ngOnInit`:
```typescript
  availableRoles: string[] = [];
```
- In `loadUsers()`, after obtaining `tenantId`, also call:
```typescript
    this.apiService.getAssignableTenantRoles(tenantId).subscribe({
      next: roles => this.availableRoles = roles,
      error: () => this.availableRoles = []
    });
```
- Change `loadSelectedUserRoles()` to call `getMemberFeatureRoles(tenantId, userId)` and set `selectedUserRoles = response.roles`.
- Change `assignRole()` to call `assignMemberFeatureRole(tenantId, selectedUser.id, { roleName: this.roleToAssign })`.
- Change `removeRole(roleName)` to call `revokeMemberFeatureRole(tenantId, selectedUser.id, roleName)`.

Each call needs the current `tenantId` (`this.authService.getCurrentTenantId()`). Read it once into a field in `ngOnInit`.

> Concretely, replace the three subscribe bodies that currently use `assignRoleToUser` / `getUserRoles` / `removeRoleFromUser` with the new tenant-scoped calls above, keeping the existing snackbar/`loadSelectedUserRoles()` success handling.

- [ ] **Step 2: Build**

Run: `npm run build`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add src/app/modules/admin/users-roles-management.component.ts
git commit -m "fix(rbac): user-role screen assigns per-tenant feature roles (#1)"
```

---

### Task C4: Gate routes, menu, and dashboards by resource access (#7)

**Files:**
- Modify: `src/app/app.routes.ts`
- Modify: `src/app/shared/components/layout/layout.component.ts`
- Modify: `src/app/modules/dashboard/dashboard.component.ts`

- [ ] **Step 1: Add `canActivateChild` so child-route guards actually run**

In `app.routes.ts`, the `LayoutComponent` route uses `canActivate: [authGuard]` only. Add resource gating per child route and ensure it runs. Change the feature routes to use `resourceAccessGuard` with `data.resource`. For example, replace the finance/hr/admin routes (`app.routes.ts:62-95`) so each carries a guard + resource code matching the seeded `Resource.Code` values from `TenantBootstrapSeeder`:

```typescript
import { resourceAccessGuard } from './core/guards';
import { PermissionLevel } from './core/models';
// ...
      { path: 'finance/chart-of-accounts', component: ChartOfAccountsComponent,
        canActivate: [resourceAccessGuard], data: { resource: 'ChartOfAccounts.Management', requiredLevel: PermissionLevel.Read } },
      { path: 'finance/general-ledger', component: GeneralLedgerComponent,
        canActivate: [resourceAccessGuard], data: { resource: 'GeneralLedger.Management', requiredLevel: PermissionLevel.Read } },
      { path: 'hr/employees', component: EmployeesComponent,
        canActivate: [resourceAccessGuard], data: { resource: 'HR.Employees', requiredLevel: PermissionLevel.Read } },
      { path: 'hr/worklogs', component: WorklogsComponent,
        canActivate: [resourceAccessGuard], data: { resource: 'HR.WorkLogs', requiredLevel: PermissionLevel.Read } },
      { path: 'hr/payments', component: HrPaymentsComponent,
        canActivate: [resourceAccessGuard], data: { resource: 'HR.Payments', requiredLevel: PermissionLevel.Read } },
      { path: 'hr/periodos', component: PaymentPeriodsComponent,
        canActivate: [resourceAccessGuard], data: { resource: 'HR.PaymentPeriods', requiredLevel: PermissionLevel.Read } },
      { path: 'accounts-payable', component: AccountsPayableListComponent,
        canActivate: [resourceAccessGuard], data: { resource: 'AccountsPayable.Entries', requiredLevel: PermissionLevel.Read } },
      { path: 'accounts-payable/new', component: AccountsPayableFormComponent,
        canActivate: [resourceAccessGuard], data: { resource: 'AccountsPayable.Entries', requiredLevel: PermissionLevel.Write } },
      { path: 'accounts-payable/:id/edit', component: AccountsPayableFormComponent,
        canActivate: [resourceAccessGuard], data: { resource: 'AccountsPayable.Entries', requiredLevel: PermissionLevel.Write } },
      { path: 'admin/permissions', component: PermissionsManagementComponent,
        canActivate: [resourceAccessGuard], data: { resource: 'Permission.Management', requiredLevel: PermissionLevel.Read } },
      { path: 'admin/users-roles', component: UsersRolesManagementComponent,
        canActivate: [resourceAccessGuard], data: { resource: 'User.Management', requiredLevel: PermissionLevel.Read } },
      { path: 'admin/members', component: TenantMembersComponent,
        canActivate: [resourceAccessGuard], data: { resource: 'User.Management', requiredLevel: PermissionLevel.Read } },
```

> `resourceAccessGuard` declared directly on each child route runs on activation of that child (guards declared on a route always run for that route). `authGuard` on the parent still enforces auth + tenant selection. Tenant admins/master admins pass these because `my-permissions` returns Full for them (Task B3).

- [ ] **Step 2: Drive the menu by `resourceCode`**

In `layout.component.ts`, set `resourceCode` on each module item (and drop `roles`), matching the seeded codes. For example:

```typescript
        { label: 'Plano de Contas', icon: 'account_tree', route: '/finance/chart-of-accounts', resourceCode: 'ChartOfAccounts.Management' },
        { label: 'Razão Geral', icon: 'menu_book', route: '/finance/general-ledger', resourceCode: 'GeneralLedger.Management' },
        // RH section:
        { label: 'Funcionários', icon: 'badge', route: '/hr/employees', resourceCode: 'HR.Employees' },
        { label: 'Horas', icon: 'schedule', route: '/hr/worklogs', resourceCode: 'HR.WorkLogs' },
        { label: 'Pagamentos', icon: 'payments', route: '/hr/payments', resourceCode: 'HR.Payments' },
        { label: 'Períodos', icon: 'event_note', route: '/hr/periodos', resourceCode: 'HR.PaymentPeriods' },
        // Contas a Pagar:
        { label: 'Lançamentos', icon: 'request_quote', route: '/accounts-payable', resourceCode: 'AccountsPayable.Entries' },
        // Administração:
        { label: 'Permissões por Role', icon: 'security', route: '/admin/permissions', resourceCode: 'Permission.Management' },
        { label: 'Roles por Usuário', icon: 'manage_accounts', route: '/admin/users-roles', resourceCode: 'User.Management' },
        { label: 'Membros do Tenant', icon: 'group', route: '/admin/members', resourceCode: 'User.Management' },
```

`hasAccess()` already prefers `resourceCode` via `canAccessResource` (`layout.component.ts:172-178`), so the menu now reflects per-tenant resource access. Leave the `/dashboard` and `/admin/tenant` items role-free (always visible to members).

- [ ] **Step 3: Gate dashboard tabs by resource access**

In `dashboard.component.ts`, replace role-based tabs with resource-based gating:

```typescript
  readonly tabs: DashTab[] = [
    { label: 'Visão Geral', route: 'overview' },
    { label: 'Contabilidade', route: 'accounting', resourceCode: 'GeneralLedger.Management' },
    { label: 'Financeiro', route: 'finance', resourceCode: 'AccountsPayable.Entries' },
    { label: 'RH', route: 'hr', resourceCode: 'HR.Employees' },
    { label: 'Administração', route: 'admin', resourceCode: 'User.Management' }
  ];

  hasAccess(tab: DashTab): boolean {
    if (!tab.resourceCode) return true;
    return this.authService.canAccessResource(tab.resourceCode);
  }
```

Update the `DashTab` interface to `{ label: string; route: string; resourceCode?: string }` and update the template binding from `hasAccess(tab, user)` to `hasAccess(tab)` (and the dashboard child routes in `app.routes.ts:57-68` can drop their `data.roles`).

- [ ] **Step 4: Build**

Run: `npm run build`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/app/app.routes.ts src/app/shared/components/layout/layout.component.ts src/app/modules/dashboard/dashboard.component.ts src/app/modules/dashboard/dashboard.component.html
git commit -m "feat(rbac): gate routes/menu/dashboards by per-tenant resource access (#7)"
```

---

### Task C5: Frontend test/lint pass

- [ ] **Step 1: Lint**

Run: `npm run lint`
Expected: PASS (fix any unused imports left from C3/C4, e.g. removed `assignRoleToUser` usages).

- [ ] **Step 2: Unit tests**

Run: `npm test -- --watch=false`
Expected: PASS. (No specs cover the changed components today; if the run scaffolds any, keep them green.)

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "chore(rbac): lint/test cleanup"
```

---

## Phase D — End-to-end verification

### Task D1: Backend migration applies on a real database

- [ ] **Step 1: Apply migration**

With PostgreSQL reachable (see `appsettings.ConnectionStrings.json`), run from repo root:
```bash
dotnet ef database update -p SaaS_BasePlatform.Infrastructure -s SaaS_BasePlatform.Api
```
Expected: `TenantUserRoles` table created; `DbInitializer` backfill log line `✓ Backfilled N per-tenant role rows` appears on next API start.

### Task D2: Manual cross-tenant verification (the original bug)

- [ ] **Step 1: Run API + SPA**

Backend: `dotnet run --project SaaS_BasePlatform.Api`. Frontend: `npm start`.

- [ ] **Step 2: Verify #1**

As a master admin, create two tenants (or use seeded `default`). In Tenant A's `/admin/users-roles`, grant a member the `RH` role. Log in as that member, select **Tenant B** → confirm they do **not** have RH access (menu/dashboard hidden, `/hr/employees` redirects to `/dashboard`). Select **Tenant A** → confirm RH access is present.

- [ ] **Step 3: Verify #6**

Confirm a `TenantRole.Admin`/`Owner` member sees the admin areas for their tenant only, and that the master admin (`admin@SBP.com`) sees everything across tenants. Confirm a tenant admin cannot grant `Administrador` (the option is absent — `assignable-roles` excludes it).

- [ ] **Step 4: Verify #7**

With a member who has only `Financeiro` in the active tenant, confirm only the Financeiro dashboard/menu entries are visible and HR/admin routes redirect away.

---

## Self-Review notes (author checklist — already applied)

- **Spec coverage:** #1 → A1–A4, B1–B6, C3; #6 → A0, B1, B3 (tenant-admin Full), B6 (master-only global), C5/D3; #7 → B2/B3 (tenant-scoped reads), C4. ✅
- **Type consistency:** `ITenantRoleService` methods (`GetEffectiveRoleNamesAsync`, `GetTenantRoleNamesAsync`, `AssignFeatureRoleAsync`, `RevokeFeatureRoleAsync`) are used identically in B3/B4/B5. Resource codes used in C4 (`HR.Employees`, `ChartOfAccounts.Management`, `GeneralLedger.Management`, `HR.WorkLogs`, `HR.Payments`, `HR.PaymentPeriods`, `AccountsPayable.Entries`, `Permission.Management`, `User.Management`) all exist in `TenantBootstrapSeeder.DefaultResources`. ✅
- **Known limitation / out of scope:** master-admin promotion UI is not added (use the seeded `admin@SBP.com`); creating brand-new tenants remains master-only (the self-service create flow was removed in the earlier quick-win).

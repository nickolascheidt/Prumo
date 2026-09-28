using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Prumo.Application.DTOs.Roles;
using Prumo.Application.Services;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Services
{
    /// <summary>
    /// A role created by one tenant must not show up for another. It is the same class of
    /// leak that started the per-tenant RBAC rework — Identity roles were global and access
    /// leaked between tenants.
    /// </summary>
    public class TenantRoleAdminServiceTests
    {
        private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static ApplicationDbContext NewDb(Guid tenantId, string dbName)
        {
            var ctx = new TenantContext();
            ctx.SetTenant(tenantId);
            return new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(dbName).Options, ctx);
        }

        /// <summary>
        /// A thin RoleManager over the DbContext itself: we only need NormalizeKey,
        /// CreateAsync and DeleteAsync, and we want them to write to the same in-memory
        /// database the service reads.
        /// </summary>
        private static RoleManager<ApplicationRole> RoleManagerOver(ApplicationDbContext db)
        {
            var manager = Substitute.For<RoleManager<ApplicationRole>>(
                Substitute.For<IRoleStore<ApplicationRole>>(),
                null, null, null, null);

            manager.NormalizeKey(Arg.Any<string>())
                   .Returns(call => call.Arg<string>()?.ToUpperInvariant());

            manager.CreateAsync(Arg.Any<ApplicationRole>())
                   .Returns(call =>
                   {
                       var role = call.Arg<ApplicationRole>();
                       role.NormalizedName = role.Name?.ToUpperInvariant();
                       db.Roles.Add(role);
                       db.SaveChanges();
                       return Task.FromResult(IdentityResult.Success);
                   });

            manager.DeleteAsync(Arg.Any<ApplicationRole>())
                   .Returns(call =>
                   {
                       db.Roles.Remove(call.Arg<ApplicationRole>());
                       db.SaveChanges();
                       return Task.FromResult(IdentityResult.Success);
                   });

            return manager;
        }

        private static void SeedCanonicalRole(ApplicationDbContext db, string name)
        {
            db.Roles.Add(new ApplicationRole
            {
                Id = Guid.NewGuid(),
                Name = name,
                NormalizedName = name.ToUpperInvariant(),
                TenantId = null
            });
            db.SaveChanges();
        }

        private static TenantRoleAdminService ServiceOver(ApplicationDbContext db) =>
            new(db, RoleManagerOver(db));

        [Fact]
        public async Task A_role_created_by_a_tenant_shows_in_its_list()
        {
            using var db = NewDb(TenantA, nameof(A_role_created_by_a_tenant_shows_in_its_list));
            var service = ServiceOver(db);

            await service.CreateAsync(TenantA, new CreateTenantRoleDto("Viewer", null));

            var visible = await service.GetVisibleRolesAsync(TenantA);

            Assert.Contains(visible, r => r.Name == "Viewer" && !r.IsCanonical);
        }

        [Fact]
        public async Task A_role_created_by_a_tenant_does_NOT_show_for_another()
        {
            using var db = NewDb(TenantA, nameof(A_role_created_by_a_tenant_does_NOT_show_for_another));
            var service = ServiceOver(db);

            await service.CreateAsync(TenantA, new CreateTenantRoleDto("Viewer", null));

            var visibleInB = await service.GetVisibleRolesAsync(TenantB);

            Assert.DoesNotContain(visibleInB, r => r.Name == "Viewer");
        }

        [Fact]
        public async Task Canonical_roles_stay_visible_in_every_tenant()
        {
            using var db = NewDb(TenantA, nameof(Canonical_roles_stay_visible_in_every_tenant));
            SeedCanonicalRole(db, "HR");
            var service = ServiceOver(db);

            var visibleInB = await service.GetVisibleRolesAsync(TenantB);

            Assert.Contains(visibleInB, r => r.Name == "HR" && r.IsCanonical);
        }

        [Fact]
        public async Task A_tenant_role_is_assignable_only_in_its_owner_tenant()
        {
            using var db = NewDb(TenantA, nameof(A_tenant_role_is_assignable_only_in_its_owner_tenant));
            var service = ServiceOver(db);

            await service.CreateAsync(TenantA, new CreateTenantRoleDto("Viewer", null));

            var assignableInA = await service.GetAssignableRoleNamesAsync(TenantA);
            var assignableInB = await service.GetAssignableRoleNamesAsync(TenantB);

            Assert.Contains("Viewer", assignableInA);
            Assert.DoesNotContain("Viewer", assignableInB);

            // Canonical roles stay assignable in both, and the master stays out.
            Assert.Contains("HR", assignableInA);
            Assert.Contains("HR", assignableInB);
            Assert.DoesNotContain("Administrator", assignableInA);
        }

        /// <summary>
        /// WARNING: this test covers the service logic, <b>not</b> Identity's
        /// <c>IRoleValidator</c> — the RoleManager here is a stand-in that writes straight to
        /// the context and runs no validator. The real defect ("Role name is already taken",
        /// from the default validator) passed this test unnoticed and only showed up against
        /// the live API. When touching name uniqueness, also check with the API running.
        /// </summary>
        [Fact]
        public async Task Two_tenants_can_each_have_a_role_with_the_same_name()
        {
            using var db = NewDb(TenantA, nameof(Two_tenants_can_each_have_a_role_with_the_same_name));
            var service = ServiceOver(db);

            await service.CreateAsync(TenantA, new CreateTenantRoleDto("Viewer", null));
            await service.CreateAsync(TenantB, new CreateTenantRoleDto("Viewer", null));

            var inA = await service.GetVisibleRolesAsync(TenantA);
            var inB = await service.GetVisibleRolesAsync(TenantB);

            Assert.Single(inA, r => r.Name == "Viewer");
            Assert.Single(inB, r => r.Name == "Viewer");

            // And they are different rows, not the same role showing up twice.
            var idA = inA.Single(r => r.Name == "Viewer").Id;
            var idB = inB.Single(r => r.Name == "Viewer").Id;
            Assert.NotEqual(idA, idB);
        }

        [Fact]
        public async Task A_role_created_by_the_tenant_can_be_granted_to_a_member()
        {
            using var db = NewDb(TenantA, nameof(A_role_created_by_the_tenant_can_be_granted_to_a_member));
            var admin = ServiceOver(db);
            var roles = new TenantRoleService(db);

            await admin.CreateAsync(TenantA, new CreateTenantRoleDto("Viewer", null));

            var userId = Guid.NewGuid();
            await roles.AssignFeatureRoleAsync(TenantA, userId, "Viewer");

            var granted = await roles.GetTenantRoleNamesAsync(userId, TenantA);
            Assert.Contains("Viewer", granted);
        }

        [Fact]
        public async Task Cannot_grant_another_tenants_role()
        {
            using var db = NewDb(TenantA, nameof(Cannot_grant_another_tenants_role));
            var admin = ServiceOver(db);
            var roles = new TenantRoleService(db);

            // The role exists, but it belongs to tenant A.
            await admin.CreateAsync(TenantA, new CreateTenantRoleDto("Viewer", null));

            // Tenant B cannot grant it just by knowing the name.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => roles.AssignFeatureRoleAsync(TenantB, Guid.NewGuid(), "Viewer"));
        }

        [Fact]
        public async Task Cannot_recreate_a_system_role()
        {
            using var db = NewDb(TenantA, nameof(Cannot_recreate_a_system_role));
            SeedCanonicalRole(db, "HR");
            var service = ServiceOver(db);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateAsync(TenantA, new CreateTenantRoleDto("hr", null)));

            Assert.Contains("system role", error.Message);
        }

        [Fact]
        public async Task A_tenant_cannot_delete_another_tenants_role()
        {
            using var db = NewDb(TenantA, nameof(A_tenant_cannot_delete_another_tenants_role));
            var service = ServiceOver(db);

            var created = await service.CreateAsync(TenantA, new CreateTenantRoleDto("Viewer", null));

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => service.DeleteAsync(TenantB, created.Id));
        }

        [Fact]
        public async Task Cannot_delete_a_role_still_assigned_to_a_member()
        {
            using var db = NewDb(TenantA, nameof(Cannot_delete_a_role_still_assigned_to_a_member));
            var service = ServiceOver(db);

            var created = await service.CreateAsync(TenantA, new CreateTenantRoleDto("Viewer", null));

            db.TenantUserRoles.Add(new TenantUserRole
            {
                TenantId = TenantA,
                UserId = Guid.NewGuid(),
                RoleId = created.Id
            });
            await db.SaveChangesAsync();

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.DeleteAsync(TenantA, created.Id));

            Assert.Contains("assigned to members", error.Message);
        }
    }
}

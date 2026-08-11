using Microsoft.EntityFrameworkCore;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Authorization;
using Prumo.Infrastructure.Data;
using Xunit;

namespace Prumo.Tests.Services
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

        [Fact]
        public async Task Permissions_do_not_leak_across_tenants()
        {
            using var db = NewDb();
            var user = Guid.NewGuid();
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();
            var rh = new ApplicationRole { Id = Guid.NewGuid(), Name = "RH", NormalizedName = "RH" };
            var perm = new Permission { Id = Guid.NewGuid(), Name = "employees.view" };
            db.Roles.Add(rh);
            db.Permissions.Add(perm);
            // RolePermission + role assignment exist ONLY in tenant B.
            db.RolePermissions.Add(new RolePermission { TenantId = tenantB, RoleId = rh.Id, PermissionId = perm.Id, GrantedAt = DateTime.UtcNow });
            db.TenantUserRoles.Add(new TenantUserRole { TenantId = tenantB, UserId = user, RoleId = rh.Id });
            await db.SaveChangesAsync();

            var sut = new PermissionService(db, null!, null!);

            var inTenantA = await sut.GetUserPermissionsForTenantAsync(user, tenantA);
            var inTenantB = await sut.GetUserPermissionsForTenantAsync(user, tenantB);

            Assert.Empty(inTenantA);                         // no leakage into tenant A
            Assert.Contains("employees.view", inTenantB);    // present in tenant B
        }
    }
}

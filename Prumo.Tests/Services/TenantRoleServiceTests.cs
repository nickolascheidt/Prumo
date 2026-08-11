using Microsoft.EntityFrameworkCore;
using Prumo.Application.Services;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data;
using Xunit;

namespace Prumo.Tests.Services
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

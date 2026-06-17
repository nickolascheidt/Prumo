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
            await db.SaveChangesAsync();

            // Simulate a legacy global assignment row.
            await DbInitializer.BackfillTenantUserRolesAsync(
                db,
                globalAssignments: new[] { (userId, rhRoleId, "RH") });

            var rows = await db.TenantUserRoles.IgnoreQueryFilters().ToListAsync();
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

            Assert.Equal(1, await db.TenantUserRoles.IgnoreQueryFilters().CountAsync());
        }
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Prumo.Application.Services;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data;

namespace Prumo.Tests.Services
{
    public class TenantSupportAccessTests
    {
        private static ApplicationDbContext NewDb()
        {
            var ctx = Substitute.For<ITenantContext>();
            ctx.HasTenant.Returns(false);
            return new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, ctx);
        }

        private static UserManager<ApplicationUser> MockUserManager() =>
            Substitute.For<UserManager<ApplicationUser>>(
                Substitute.For<IUserStore<ApplicationUser>>(),
                null, null, null, null, null, null, null, null);

        [Fact]
        public async Task Support_access_is_idempotent_and_makes_the_master_admin_a_member()
        {
            var tenantId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            await using var db = NewDb();
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t", OwnerUserId = Guid.NewGuid() });
            await db.SaveChangesAsync();

            var service = new TenantService(db, MockUserManager());

            Assert.True(await service.GrantSupportAccessAsync(tenantId, adminId));
            Assert.True(await service.GrantSupportAccessAsync(tenantId, adminId));

            var memberships = await db.TenantUsers.IgnoreQueryFilters()
                .Where(tu => tu.TenantId == tenantId && tu.UserId == adminId).ToListAsync();

            Assert.Single(memberships);
            Assert.Equal(TenantRole.Admin, memberships[0].Role);
        }

        [Fact]
        public async Task Support_access_writes_exactly_one_audit_row()
        {
            var tenantId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            await using var db = NewDb();
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t", OwnerUserId = Guid.NewGuid() });
            await db.SaveChangesAsync();

            var service = new TenantService(db, MockUserManager());

            await service.GrantSupportAccessAsync(tenantId, adminId);
            await service.GrantSupportAccessAsync(tenantId, adminId);

            // Entidade própria desde o item 3B: antes isto ia no PermissionAuditLog com
            // sentinelas, porque aquela tabela era modelada para grants de permissão.
            var audit = await db.SupportAccessLogs.IgnoreQueryFilters()
                .Where(a => a.TenantId == tenantId).ToListAsync();

            Assert.Single(audit);
            Assert.Equal(adminId, audit[0].MasterAdminUserId);
        }

        [Fact]
        public async Task Support_access_to_an_unknown_tenant_returns_false()
        {
            await using var db = NewDb();
            var service = new TenantService(db, MockUserManager());

            Assert.False(await service.GrantSupportAccessAsync(Guid.NewGuid(), Guid.NewGuid()));
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Prumo.Application.Services;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Services
{
    /// <summary>
    /// GetTenantRoleNamesAsync has a requirement the entity type hides: it runs while the
    /// token is being issued, when there is NO resolved tenant yet.
    ///
    /// TenantResolutionMiddleware fills the TenantContext from the `tenant_id` claim, and in
    /// POST /api/tenants/select the INCOMING token does not have that claim yet — it is the
    /// token being issued that will have it. At login the user is not even authenticated
    /// when the token is generated.
    ///
    /// TenantUserRole is ITenantScoped, so the fail-closed global filter applies to it.
    /// Without IgnoreQueryFilters this method would return an empty list at that moment,
    /// and the user would get a token with no feature role — an empty menu.
    ///
    /// That is why that IgnoreQueryFilters is legitimately cross-tenant. This test exists
    /// so removing the call breaks the build instead of breaking login.
    /// </summary>
    public class TenantRoleServiceTenantlessTests
    {
        [Fact]
        public async Task Tenant_roles_are_readable_while_no_tenant_is_resolved()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var roleId = Guid.NewGuid();

            var seedCtx = new TenantContext();
            seedCtx.SetTenant(tenantId);
            await using (var seed = NewDb(seedCtx, dbName))
            {
                seed.Roles.Add(new ApplicationRole { Id = roleId, Name = "HR", NormalizedName = "HR" });
                seed.TenantUserRoles.Add(new TenantUserRole
                {
                    TenantId = tenantId,
                    UserId = userId,
                    RoleId = roleId
                });
                await seed.SaveChangesAsync();
            }

            // The real state of POST /tenants/select: authenticated, but no resolved tenant.
            await using var db = NewDb(new TenantContext(), dbName);
            var service = new TenantRoleService(db);

            var roles = await service.GetTenantRoleNamesAsync(userId, tenantId);

            Assert.Equal(new[] { "HR" }, roles);
        }

        private static ApplicationDbContext NewDb(ITenantContext ctx, string dbName) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName).Options, ctx);
    }
}

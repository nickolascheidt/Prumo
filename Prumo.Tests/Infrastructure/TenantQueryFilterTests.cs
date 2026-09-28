using Microsoft.EntityFrameworkCore;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Infrastructure
{
    public class TenantQueryFilterTests
    {
        private static ApplicationDbContext NewDb(ITenantContext ctx, string dbName) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName).Options, ctx);

        [Fact]
        public async Task Without_a_resolved_tenant_scoped_entities_return_nothing()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();

            var seeding = new TenantContext();
            seeding.SetTenant(tenantA);
            await using (var db = NewDb(seeding, dbName))
            {
                db.Resources.Add(new Resource { TenantId = tenantA, Code = "a", Name = "A", IsActive = true });
                db.Resources.Add(new Resource { TenantId = tenantB, Code = "b", Name = "B", IsActive = true });
                await db.SaveChangesAsync();
            }

            // Context without a tenant: fail-closed returns nothing, not everything.
            await using var noTenant = NewDb(new TenantContext(), dbName);
            var visible = await noTenant.Resources.ToListAsync();

            Assert.Empty(visible);
        }

        [Fact]
        public async Task With_a_resolved_tenant_only_that_tenants_rows_are_visible()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();

            var seeding = new TenantContext();
            seeding.SetTenant(tenantA);
            await using (var db = NewDb(seeding, dbName))
            {
                db.Resources.Add(new Resource { TenantId = tenantA, Code = "a", Name = "A", IsActive = true });
                db.Resources.Add(new Resource { TenantId = tenantB, Code = "b", Name = "B", IsActive = true });
                await db.SaveChangesAsync();
            }

            var scoped = new TenantContext();
            scoped.SetTenant(tenantB);
            await using var db2 = NewDb(scoped, dbName);
            var visible = await db2.Resources.ToListAsync();

            Assert.Single(visible);
            Assert.Equal("b", visible[0].Code);
        }
    }
}

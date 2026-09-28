using Microsoft.EntityFrameworkCore;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Data.Seeders;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Infrastructure
{
    public class SeederIdempotenceTests
    {
        private static ApplicationDbContext NewDb(ITenantContext ctx, string dbName) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName).Options, ctx);

        [Fact]
        public async Task Re_running_the_bootstrap_seeder_does_not_resurrect_a_revoked_grant()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantId = Guid.NewGuid();

            var ctx = new TenantContext();
            ctx.SetTenant(tenantId);

            await using var db = NewDb(ctx, dbName);

            // The seeder only grants ResourcePermissions to Identity roles that exist.
            // Without this line nothing is seeded and the test would pass vacuously.
            db.Roles.Add(new ApplicationRole { Name = "Administrator", NormalizedName = "ADMINISTRADOR" });
            await db.SaveChangesAsync();

            await TenantBootstrapSeeder.SeedAsync(db, tenantId);

            var granted = await db.ResourcePermissions.IgnoreQueryFilters()
                .Where(rp => rp.TenantId == tenantId).ToListAsync();
            Assert.NotEmpty(granted);

            // An admin revokes a grant.
            var revoked = granted[0];
            db.ResourcePermissions.Remove(revoked);
            await db.SaveChangesAsync();

            // Re-applying the seeder, as a startup routine would.
            await TenantBootstrapSeeder.SeedAsync(db, tenantId);

            var stillRevoked = !await db.ResourcePermissions.IgnoreQueryFilters()
                .AnyAsync(rp => rp.TenantId == tenantId
                             && rp.RoleId == revoked.RoleId
                             && rp.ResourceId == revoked.ResourceId);

            // Contract: the seeder is called ONCE, when the tenant is created. Re-applying it
            // would bring revoked grants back, which is why it does not run at startup. This
            // test exists so nobody brings it back.
            Assert.False(stillRevoked,
                "The seeder still brings grants back when re-applied — so it must NEVER run at "
                + "startup again. If this test fails because the seeder became safe to re-apply, "
                + "great: adjust the assertion.");
        }
    }
}

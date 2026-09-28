using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Infrastructure
{
    /// <summary>
    /// The gap this file closes: no test exercised the DbInitializer, and so a bug where
    /// startup re-seeded revoked grants passed a green suite. SeederIdempotenceTests proves
    /// the seeder brings grants back when re-applied; what was missing was proof that the
    /// initializer does NOT re-apply it.
    ///
    /// The whole InitializeAsync is not testable here — it calls MigrateAsync, which needs
    /// a relational provider, and its catch would swallow the exception, letting the test
    /// pass vacuously. So the target is EnsureDefaultTenantAsync, which is where the bug
    /// lived.
    /// </summary>
    public class DbInitializerReseedTests
    {
        private static ApplicationDbContext NewDb(ITenantContext ctx, string dbName) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName).Options, ctx);

        [Fact]
        public async Task Second_boot_does_not_resurrect_a_revoked_grant()
        {
            var dbName = Guid.NewGuid().ToString();
            var ctx = new TenantContext();
            await using var db = NewDb(ctx, dbName);

            // TenantBootstrapSeeder only grants ResourcePermissions to Identity roles that
            // exist. Without this line nothing is seeded and the test would pass vacuously.
            db.Roles.Add(new ApplicationRole { Name = "Administrator", NormalizedName = "ADMINISTRADOR" });
            var admin = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "admin@SBP.com",
                Email = "admin@SBP.com",
                FullName = "Administrador do Sistema"
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();

            // Primeiro boot: cria o tenant 'default' e semeia recursos + grants.
            await DbInitializer.EnsureDefaultTenantAsync(db, admin, NullLogger.Instance);

            var granted = await db.ResourcePermissions.IgnoreQueryFilters().ToListAsync();
            Assert.NotEmpty(granted);

            // An admin revokes a grant between the two boots.
            var revoked = granted[0];
            db.ResourcePermissions.Remove(revoked);
            await db.SaveChangesAsync();

            // Second boot: the tenant already exists, so nothing may be re-seeded.
            await DbInitializer.EnsureDefaultTenantAsync(db, admin, NullLogger.Instance);

            var resurrected = await db.ResourcePermissions.IgnoreQueryFilters()
                .AnyAsync(rp => rp.RoleId == revoked.RoleId
                             && rp.ResourceId == revoked.ResourceId);

            Assert.False(resurrected,
                "The revoked grant came back after a restart. The seeders may only run "
                + "inside the tenant-creation branch of EnsureDefaultTenantAsync — if "
                + "someone moved them out, this is the re-seeding bug coming back.");
        }

        [Fact]
        public async Task Second_boot_does_not_duplicate_the_default_tenant_or_its_membership()
        {
            var dbName = Guid.NewGuid().ToString();
            var ctx = new TenantContext();
            await using var db = NewDb(ctx, dbName);

            db.Roles.Add(new ApplicationRole { Name = "Administrator", NormalizedName = "ADMINISTRADOR" });
            var admin = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "admin@SBP.com",
                Email = "admin@SBP.com",
                FullName = "Administrador do Sistema"
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();

            await DbInitializer.EnsureDefaultTenantAsync(db, admin, NullLogger.Instance);
            await DbInitializer.EnsureDefaultTenantAsync(db, admin, NullLogger.Instance);

            var tenants = await db.Tenants.IgnoreQueryFilters()
                .Where(t => t.Slug == "default").ToListAsync();
            Assert.Single(tenants);

            var memberships = await db.TenantUsers.IgnoreQueryFilters()
                .Where(tu => tu.TenantId == tenants[0].Id && tu.UserId == admin.Id).ToListAsync();
            Assert.Single(memberships);
        }
    }
}

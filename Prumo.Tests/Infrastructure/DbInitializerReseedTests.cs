using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Infrastructure
{
    /// <summary>
    /// A lacuna que este arquivo fecha: nenhum teste exercitava o DbInitializer, e por
    /// isso o bug 4203a15 passou por 83 testes verdes. O SeederIdempotenceTests prova
    /// que o seeder ressuscita grants quando reaplicado; o que faltava era provar que o
    /// initializer NÃO o reaplica.
    ///
    /// O InitializeAsync inteiro não é testável aqui — ele chama MigrateAsync, que exige
    /// provider relacional, e o catch dele engoliria a exceção fazendo o teste passar por
    /// vacuidade. Por isso o alvo é EnsureDefaultTenantAsync, que é onde o bug morava.
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

            // O TenantBootstrapSeeder só concede ResourcePermissions para roles do
            // Identity que existam. Sem esta linha nada é semeado e o teste passaria
            // por vacuidade — foi exatamente o erro que o plano da fase 1 cometeu.
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

            // Um admin revoga um grant entre os dois boots.
            var revoked = granted[0];
            db.ResourcePermissions.Remove(revoked);
            await db.SaveChangesAsync();

            // Segundo boot: o tenant já existe, então nada pode ser re-semeado.
            await DbInitializer.EnsureDefaultTenantAsync(db, admin, NullLogger.Instance);

            var resurrected = await db.ResourcePermissions.IgnoreQueryFilters()
                .AnyAsync(rp => rp.RoleId == revoked.RoleId
                             && rp.ResourceId == revoked.ResourceId);

            Assert.False(resurrected,
                "O grant revogado voltou depois de um restart. Os seeders só podem rodar "
                + "dentro do ramo de criação do tenant em EnsureDefaultTenantAsync — se "
                + "alguém os moveu para fora, este é o bug 4203a15 de volta.");
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

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

            // O seeder só concede ResourcePermissions para roles do Identity que existam.
            // Sem esta linha nada é semeado e o teste passaria por vacuidade.
            db.Roles.Add(new ApplicationRole { Name = "Administrador", NormalizedName = "ADMINISTRADOR" });
            await db.SaveChangesAsync();

            await TenantBootstrapSeeder.SeedAsync(db, tenantId);

            var granted = await db.ResourcePermissions.IgnoreQueryFilters()
                .Where(rp => rp.TenantId == tenantId).ToListAsync();
            Assert.NotEmpty(granted);

            // Um admin revoga um grant.
            var revoked = granted[0];
            db.ResourcePermissions.Remove(revoked);
            await db.SaveChangesAsync();

            // Reaplicar o seeder é o que o startup faz hoje.
            await TenantBootstrapSeeder.SeedAsync(db, tenantId);

            var stillRevoked = !await db.ResourcePermissions.IgnoreQueryFilters()
                .AnyAsync(rp => rp.TenantId == tenantId
                             && rp.RoleId == revoked.RoleId
                             && rp.ResourceId == revoked.ResourceId);

            // Contrato: o seeder é chamado UMA vez, na criação do tenant. Reaplicá-lo
            // ressuscitaria grants revogados, e é por isso que EnsureTenantBootstrapAsync
            // foi removido do startup. Este teste existe para que ninguém o traga de volta.
            Assert.False(stillRevoked,
                "O seeder continua ressuscitando grants ao ser reaplicado — por isso ele NUNCA "
                + "pode voltar a rodar no startup. Se este teste falhar porque o seeder passou a "
                + "ser seguro para reaplicação, ótimo: ajuste a asserção e registre a mudança.");
        }
    }
}

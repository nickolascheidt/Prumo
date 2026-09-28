using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prumo.Domain.Entities;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
    {
        public void Configure(EntityTypeBuilder<ApplicationRole> builder)
        {
            builder.Property(r => r.Description).HasMaxLength(256);

            // O índice do Identity precisa SAIR do modelo, não apenas ser sobreposto:
            // enquanto ele existir, NormalizedName segue único globalmente e dois tenants
            // nunca conseguiriam ter cada um a sua "Leitura". Sem esta remoção o EF ainda
            // recusa o modelo, porque dois índices diferentes disputam o mesmo nome.
            var normalizedName = builder.Metadata.FindProperty(nameof(ApplicationRole.NormalizedName));
            if (normalizedName is not null)
            {
                var identityIndex = builder.Metadata.FindIndex(normalizedName);
                if (identityIndex is not null)
                {
                    builder.Metadata.RemoveIndex(identityIndex);
                }
            }

            // O Identity cria RoleNameIndex único sobre NormalizedName. Com role por
            // tenant, dois tenants podem ter a sua própria "Leitura", então a unicidade
            // passa a valer para o par.
            builder.HasIndex(r => new { r.NormalizedName, r.TenantId })
                   .HasDatabaseName("RoleNameIndex")
                   .IsUnique()
                   // OBRIGATÓRIO. No Postgres NULL não é igual a NULL, então sem isto
                   // duas roles canônicas "HR" (ambas com TenantId null) passariam pelo
                   // índice. Verificado em PG 17.9: sem a flag insere as duas; com ela,
                   // a segunda viola a constraint. Requer PG 15+.
                   .AreNullsDistinct(false);
        }
    }
}

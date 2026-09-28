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

            // Identity's index has to LEAVE the model, not just be overridden: while it
            // exists, NormalizedName stays globally unique and two tenants could never each
            // have their own "Viewer". Without removing it EF also rejects the model,
            // because two different indexes compete for the same name.
            var normalizedName = builder.Metadata.FindProperty(nameof(ApplicationRole.NormalizedName));
            if (normalizedName is not null)
            {
                var identityIndex = builder.Metadata.FindIndex(normalizedName);
                if (identityIndex is not null)
                {
                    builder.Metadata.RemoveIndex(identityIndex);
                }
            }

            // Identity creates a unique RoleNameIndex over NormalizedName. With per-tenant
            // roles, two tenants can each have their own "Viewer", so uniqueness applies to
            // the pair.
            builder.HasIndex(r => new { r.NormalizedName, r.TenantId })
                   .HasDatabaseName("RoleNameIndex")
                   .IsUnique()
                   // REQUIRED. In Postgres NULL is not equal to NULL, so without this two
                   // canonical "HR" roles (both with a null TenantId) would pass the index.
                   // Checked on PG 17.9: without the flag both rows go in; with it, the
                   // second violates the constraint. Requires PG 15+.
                   .AreNullsDistinct(false);
        }
    }
}

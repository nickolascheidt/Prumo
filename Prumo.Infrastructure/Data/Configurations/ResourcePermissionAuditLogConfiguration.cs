using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prumo.Domain.Entities;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class ResourcePermissionAuditLogConfiguration
        : IEntityTypeConfiguration<ResourcePermissionAuditLog>
    {
        public void Configure(EntityTypeBuilder<ResourcePermissionAuditLog> builder)
        {
            builder.ToTable("ResourcePermissionAuditLogs");

            builder.HasKey(a => a.Id);

            builder.Property(a => a.TenantId).IsRequired();

            builder.Property(a => a.RoleName)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(a => a.ResourceCode)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(a => a.PerformedByUserEmail)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(a => a.PerformedAt).IsRequired();

            builder.HasIndex(a => a.TenantId);
            builder.HasIndex(a => a.RoleId);
            builder.HasIndex(a => a.ResourceId);
            // O índice que a consulta natural usa: "o que mudou neste tenant, do mais
            // recente para o mais antigo".
            builder.HasIndex(a => new { a.TenantId, a.PerformedAt });
        }
    }
}

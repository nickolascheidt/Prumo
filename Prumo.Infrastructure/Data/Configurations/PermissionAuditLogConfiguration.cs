using Prumo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class PermissionAuditLogConfiguration : IEntityTypeConfiguration<PermissionAuditLog>
    {
        public void Configure(EntityTypeBuilder<PermissionAuditLog> builder)
        {
            builder.ToTable("PermissionAuditLogs");

            builder.HasKey(p => p.Id);

            builder.Property(p => p.RoleName)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(p => p.PermissionName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(p => p.Action)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(p => p.PerformedByUserEmail)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(p => p.PerformedAt)
                .IsRequired();

            builder.Property(p => p.Reason)
                .HasMaxLength(500);

            builder.Property(p => p.TenantId).IsRequired();

            builder.HasIndex(p => p.TenantId);
            builder.HasIndex(p => p.RoleId);
            builder.HasIndex(p => p.PermissionId);
            builder.HasIndex(p => p.PerformedByUserId);
            builder.HasIndex(p => p.PerformedAt);
        }
    }
}

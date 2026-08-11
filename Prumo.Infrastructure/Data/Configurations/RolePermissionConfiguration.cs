using Prumo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
    {
        public void Configure(EntityTypeBuilder<RolePermission> builder)
        {
            builder.ToTable("RolePermissions");

            builder.Property(rp => rp.TenantId).IsRequired();

            builder.HasKey(rp => new { rp.TenantId, rp.RoleId, rp.PermissionId });

            builder.HasIndex(rp => rp.TenantId);

            builder.HasOne(rp => rp.Role)
                .WithMany()
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(rp => rp.GrantedAt)
                .IsRequired();

            builder.Property(rp => rp.GrantedByUserEmail)
                .HasMaxLength(256);
        }
    }
}

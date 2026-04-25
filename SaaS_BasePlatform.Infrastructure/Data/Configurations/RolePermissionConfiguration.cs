using SaaS_BasePlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SaaS_BasePlatform.Infrastructure.Data.Configurations
{
    public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
    {
        public void Configure(EntityTypeBuilder<RolePermission> builder)
        {
            builder.ToTable("RolePermissions");

            builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

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

using SaaS_BasePlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SaaS_BasePlatform.Infrastructure.Data.Configurations
{
    public class ResourcePermissionConfiguration : IEntityTypeConfiguration<ResourcePermission>
    {
        public void Configure(EntityTypeBuilder<ResourcePermission> builder)
        {
            builder.ToTable("ResourcePermissions");

            builder.HasKey(rp => new { rp.RoleId, rp.ResourceId });

            builder.Property(rp => rp.Level)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(rp => rp.CreatedAt)
                .IsRequired();

            builder.Property(rp => rp.CreatedByUserEmail)
                .HasMaxLength(256);

            builder.HasOne(rp => rp.Role)
                .WithMany()
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(rp => rp.Resource)
                .WithMany(r => r.ResourcePermissions)
                .HasForeignKey(rp => rp.ResourceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(rp => rp.RoleId);
            builder.HasIndex(rp => rp.ResourceId);
        }
    }
}

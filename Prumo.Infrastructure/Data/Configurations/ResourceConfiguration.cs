using Prumo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class ResourceConfiguration : IEntityTypeConfiguration<Resource>
    {
        public void Configure(EntityTypeBuilder<Resource> builder)
        {
            builder.ToTable("Resources");

            builder.HasKey(r => r.Id);

            builder.Property(r => r.Code)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(r => r.TenantId).IsRequired();

            builder.HasIndex(r => new { r.TenantId, r.Code })
                .IsUnique();

            builder.Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(r => r.Description)
                .HasMaxLength(500);

            builder.Property(r => r.Module)
                .HasMaxLength(100);

            builder.Property(r => r.FrontendRoute)
                .HasMaxLength(500);

            builder.Property(r => r.Icon)
                .HasMaxLength(100);

            builder.Property(r => r.DisplayOrder)
                .IsRequired();

            builder.HasMany(r => r.ResourcePermissions)
                .WithOne(rp => rp.Resource)
                .HasForeignKey(rp => rp.ResourceId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

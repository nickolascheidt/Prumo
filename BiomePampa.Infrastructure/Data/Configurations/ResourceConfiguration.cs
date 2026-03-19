using BiomePampa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BiomePampa.Infrastructure.Data.Configurations
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

            builder.HasIndex(r => r.Code)
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

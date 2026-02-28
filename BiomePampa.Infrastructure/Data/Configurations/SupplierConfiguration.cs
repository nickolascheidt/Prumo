using BiomePampa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BiomePampa.Infrastructure.Data.Configurations
{
    public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
    {
        public void Configure(EntityTypeBuilder<Supplier> builder)
        {
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(s => s.CompanyName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(s => s.TaxId)
                .IsRequired()
                .HasMaxLength(20);

            builder.HasIndex(s => s.TaxId)
                .IsUnique();

            builder.Property(s => s.Phone)
                .HasMaxLength(20);

            builder.Property(s => s.Email)
                .HasMaxLength(100);

            builder.Property(s => s.Address)
                .HasMaxLength(300);

            builder.Property(s => s.City)
                .HasMaxLength(100);

            builder.Property(s => s.State)
                .HasMaxLength(50);

            builder.Property(s => s.ZipCode)
                .HasMaxLength(10);

            builder.Property(s => s.Notes)
                .HasMaxLength(500);

            builder.HasMany(s => s.Batches)
                .WithOne(b => b.Supplier)
                .HasForeignKey(b => b.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasMany(s => s.Movements)
                .WithOne(m => m.Supplier)
                .HasForeignKey(m => m.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}

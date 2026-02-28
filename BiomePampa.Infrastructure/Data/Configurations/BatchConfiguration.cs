using BiomePampa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BiomePampa.Infrastructure.Data.Configurations
{
    public class BatchConfiguration : IEntityTypeConfiguration<Batch>
    {
        public void Configure(EntityTypeBuilder<Batch> builder)
        {
            builder.HasKey(b => b.Id);

            builder.Property(b => b.BatchNumber)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(b => b.BatchNumber)
                .IsUnique();

            builder.Property(b => b.InitialQuantity)
                .HasPrecision(18, 2);

            builder.Property(b => b.CurrentQuantity)
                .HasPrecision(18, 2);

            builder.Property(b => b.Notes)
                .HasMaxLength(500);

            builder.HasOne(b => b.Product)
                .WithMany(p => p.Batches)
                .HasForeignKey(b => b.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(b => b.Supplier)
                .WithMany(s => s.Batches)
                .HasForeignKey(b => b.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}

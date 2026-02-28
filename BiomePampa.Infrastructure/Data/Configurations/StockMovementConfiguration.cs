using BiomePampa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BiomePampa.Infrastructure.Data.Configurations
{
    public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
    {
        public void Configure(EntityTypeBuilder<StockMovement> builder)
        {
            builder.HasKey(m => m.Id);

            builder.Property(m => m.Quantity)
                .HasPrecision(18, 2);

            builder.Property(m => m.ResponsiblePerson)
                .HasMaxLength(200);

            builder.Property(m => m.Notes)
                .HasMaxLength(500);

            builder.HasOne(m => m.Product)
                .WithMany(p => p.Movements)
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(m => m.Batch)
                .WithMany()
                .HasForeignKey(m => m.BatchId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(m => m.Supplier)
                .WithMany(s => s.Movements)
                .HasForeignKey(m => m.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(m => m.Customer)
                .WithMany(c => c.Movements)
                .HasForeignKey(m => m.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(m => m.MovementDate);
        }
    }
}

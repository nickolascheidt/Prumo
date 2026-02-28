using BiomePampa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BiomePampa.Infrastructure.Data.Configurations
{
    public class StockConfiguration : IEntityTypeConfiguration<Stock>
    {
        public void Configure(EntityTypeBuilder<Stock> builder)
        {
            builder.HasKey(s => s.Id);

            builder.Property(s => s.AvailableQuantity)
                .HasPrecision(18, 2);

            builder.Property(s => s.ReservedQuantity)
                .HasPrecision(18, 2);

            builder.Property(s => s.Location)
                .HasMaxLength(100);

            builder.HasOne(s => s.Product)
                .WithMany()
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => s.ProductId)
                .IsUnique();
        }
    }
}

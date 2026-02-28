using BiomePampa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BiomePampa.Infrastructure.Data.Configurations
{
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(c => c.CompanyName)
                .HasMaxLength(200);

            builder.Property(c => c.TaxId)
                .IsRequired()
                .HasMaxLength(20);

            builder.HasIndex(c => c.TaxId)
                .IsUnique();

            builder.Property(c => c.Phone)
                .HasMaxLength(20);

            builder.Property(c => c.Email)
                .HasMaxLength(100);

            builder.Property(c => c.Address)
                .HasMaxLength(300);

            builder.Property(c => c.City)
                .HasMaxLength(100);

            builder.Property(c => c.State)
                .HasMaxLength(50);

            builder.Property(c => c.ZipCode)
                .HasMaxLength(10);

            builder.Property(c => c.Notes)
                .HasMaxLength(500);

            builder.HasMany(c => c.Movements)
                .WithOne(m => m.Customer)
                .HasForeignKey(m => m.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}

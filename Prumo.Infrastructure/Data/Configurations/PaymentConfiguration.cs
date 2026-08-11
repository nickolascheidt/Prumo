using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prumo.Domain.Entities;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payments");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            builder.Property(p => p.PaymentMethod).HasConversion<int>();
            builder.Property(p => p.PaymentProof).HasMaxLength(500);
            builder.Property(p => p.Notes).HasMaxLength(500);

            builder.HasIndex(p => p.PaymentDate);
            builder.HasIndex(p => p.EmployeeId);

            builder.HasOne(p => p.Employee)
                .WithMany(e => e.Payments)
                .HasForeignKey(p => p.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.PaidByUser)
                .WithMany()
                .HasForeignKey(p => p.PaidByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

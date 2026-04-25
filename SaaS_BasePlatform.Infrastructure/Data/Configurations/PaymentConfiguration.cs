using SaaS_BasePlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SaaS_BasePlatform.Infrastructure.Data.Configurations
{
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payments");

            builder.Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");

            builder.Property(p => p.PaymentProof)
                .HasMaxLength(500);

            builder.Property(p => p.Notes)
                .HasMaxLength(500);

            builder.HasIndex(p => p.PaymentDate);

            // Relacionamentos
            builder.HasOne(p => p.PaidByUser)
                .WithMany()
                .HasForeignKey(p => p.PaidByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

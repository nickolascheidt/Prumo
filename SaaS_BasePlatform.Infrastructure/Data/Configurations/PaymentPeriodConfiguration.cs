using SaaS_BasePlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SaaS_BasePlatform.Infrastructure.Data.Configurations
{
    public class PaymentPeriodConfiguration : IEntityTypeConfiguration<PaymentPeriod>
    {
        public void Configure(EntityTypeBuilder<PaymentPeriod> builder)
        {
            builder.ToTable("PaymentPeriods");

            builder.Property(p => p.TotalHours)
                .HasColumnType("decimal(18,2)");

            builder.Property(p => p.TotalAmount)
                .HasColumnType("decimal(18,2)");

            builder.HasIndex(p => new { p.EmployeeId, p.StartDate, p.EndDate });

            // Relacionamentos
            builder.HasMany(p => p.WorkLogs)
                .WithOne(w => w.PaymentPeriod)
                .HasForeignKey(w => w.PaymentPeriodId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(p => p.Payment)
                .WithOne(pay => pay.PaymentPeriod)
                .HasForeignKey<Payment>(pay => pay.PaymentPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

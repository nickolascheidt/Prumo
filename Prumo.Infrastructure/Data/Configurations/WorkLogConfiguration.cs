using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prumo.Domain.Entities;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class WorkLogConfiguration : IEntityTypeConfiguration<WorkLog>
    {
        public void Configure(EntityTypeBuilder<WorkLog> builder)
        {
            builder.ToTable("WorkLogs");
            builder.HasKey(w => w.Id);

            builder.Property(w => w.HoursWorked).HasColumnType("decimal(18,2)");
            builder.Property(w => w.HourlyRateAtTime).HasColumnType("decimal(18,2)");
            builder.Property(w => w.TotalAmount).HasColumnType("decimal(18,2)");
            builder.Property(w => w.Notes).HasMaxLength(500);

            builder.HasIndex(w => new { w.EmployeeId, w.WorkDate }).IsUnique();
            builder.HasIndex(w => w.PaymentPeriodId);

            builder.HasOne(w => w.Employee)
                .WithMany(e => e.WorkLogs)
                .HasForeignKey(w => w.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

using SaaS_BasePlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SaaS_BasePlatform.Infrastructure.Data.Configurations
{
    public class WorkLogConfiguration : IEntityTypeConfiguration<WorkLog>
    {
        public void Configure(EntityTypeBuilder<WorkLog> builder)
        {
            builder.ToTable("WorkLogs");

            builder.Property(w => w.HoursWorked)
                .HasColumnType("decimal(18,2)");

            builder.Property(w => w.HourlyRateAtTime)
                .HasColumnType("decimal(18,2)");

            builder.Property(w => w.TotalAmount)
                .HasColumnType("decimal(18,2)");

            builder.Property(w => w.Notes)
                .HasMaxLength(500);

            builder.HasIndex(w => new { w.EmployeeId, w.WorkDate });
        }
    }
}

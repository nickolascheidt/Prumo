using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Infrastructure.Data.Configurations
{
    public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> builder)
        {
            builder.ToTable("Employees");
            builder.HasKey(e => e.Id);

            builder.Property(e => e.FullName).IsRequired().HasMaxLength(200);
            builder.Property(e => e.CPF).IsRequired().HasMaxLength(14);
            builder.Property(e => e.Phone).HasMaxLength(20);
            builder.Property(e => e.Email).HasMaxLength(200);
            builder.Property(e => e.HourlyRate).HasColumnType("decimal(18,2)");
            builder.Property(e => e.PixKey).HasMaxLength(100);
            builder.Property(e => e.BankName).HasMaxLength(100);
            builder.Property(e => e.BankAccountNumber).HasMaxLength(20);
            builder.Property(e => e.BankAgency).HasMaxLength(10);
            builder.Property(e => e.ContractType).HasConversion<int>();
            builder.Property(e => e.PreferredPaymentMethod).HasConversion<int>();

            builder.HasIndex(e => new { e.TenantId, e.CPF }).IsUnique();
            builder.HasIndex(e => new { e.TenantId, e.IsActive });

            builder.HasOne(e => e.ApplicationUser)
                .WithMany()
                .HasForeignKey(e => e.ApplicationUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}

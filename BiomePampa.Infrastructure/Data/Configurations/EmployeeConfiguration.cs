using BiomePampa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BiomePampa.Infrastructure.Data.Configurations
{
    public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> builder)
        {
            builder.ToTable("Employees");

            builder.Property(e => e.FullName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.CPF)
                .IsRequired()
                .HasMaxLength(14);

            builder.HasIndex(e => e.CPF)
                .IsUnique();

            builder.Property(e => e.Phone)
                .HasMaxLength(20);

            builder.Property(e => e.Email)
                .HasMaxLength(256);

            builder.Property(e => e.HourlyRate)
                .HasColumnType("decimal(18,2)");

            builder.Property(e => e.PixKey)
                .HasMaxLength(100);

            builder.Property(e => e.BankName)
                .HasMaxLength(100);

            builder.Property(e => e.BankAccountNumber)
                .HasMaxLength(50);

            builder.Property(e => e.BankAgency)
                .HasMaxLength(20);

            // Relacionamentos
            builder.HasMany(e => e.WorkLogs)
                .WithOne(w => w.Employee)
                .HasForeignKey(w => w.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(e => e.Payments)
                .WithOne(p => p.Employee)
                .HasForeignKey(p => p.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.ApplicationUser)
                .WithMany()
                .HasForeignKey(e => e.ApplicationUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}

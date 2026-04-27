using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Infrastructure.Data.Configurations
{
    public class AccountsPayableEntryConfiguration : IEntityTypeConfiguration<AccountsPayableEntry>
    {
        public void Configure(EntityTypeBuilder<AccountsPayableEntry> builder)
        {
            builder.ToTable("AccountsPayableEntries");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
            builder.Property(e => e.SupplierName).HasMaxLength(200);
            builder.Property(e => e.Notes).HasMaxLength(2000);
            builder.Property(e => e.CancellationReason).HasMaxLength(500);

            builder.Property(e => e.Amount).HasColumnType("decimal(18,2)");

            builder.Property(e => e.Status).HasConversion<int>();
            builder.Property(e => e.PaymentMethod).HasConversion<int?>();

            builder.HasIndex(e => e.TenantId);
            builder.HasIndex(e => new { e.TenantId, e.Status });
            builder.HasIndex(e => new { e.TenantId, e.DueDate });
            builder.HasIndex(e => new { e.TenantId, e.CategoryId });
        }
    }
}

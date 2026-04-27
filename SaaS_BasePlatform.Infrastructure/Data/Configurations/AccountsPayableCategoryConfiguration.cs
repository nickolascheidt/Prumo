using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Infrastructure.Data.Configurations
{
    public class AccountsPayableCategoryConfiguration : IEntityTypeConfiguration<AccountsPayableCategory>
    {
        public void Configure(EntityTypeBuilder<AccountsPayableCategory> builder)
        {
            builder.ToTable("AccountsPayableCategories");

            builder.HasKey(c => c.Id);

            builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
            builder.Property(c => c.Description).HasMaxLength(500);
            builder.Property(c => c.Color).HasMaxLength(20);

            builder.HasIndex(c => new { c.TenantId, c.Name }).IsUnique();
            builder.HasIndex(c => c.TenantId);

            builder.HasMany(c => c.Entries)
                .WithOne(e => e.Category)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

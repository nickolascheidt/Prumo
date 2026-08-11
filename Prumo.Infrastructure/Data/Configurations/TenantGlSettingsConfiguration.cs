using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prumo.Domain.Entities;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class TenantGlSettingsConfiguration : IEntityTypeConfiguration<TenantGlSettings>
    {
        public void Configure(EntityTypeBuilder<TenantGlSettings> builder)
        {
            builder.ToTable("TenantGlSettings");

            builder.HasKey(s => s.TenantId);
            builder.Property(s => s.TenantId).ValueGeneratedNever();

            builder.HasOne(s => s.Tenant)
                .WithOne()
                .HasForeignKey<TenantGlSettings>(s => s.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(s => s.DefaultCashAccount)
                .WithMany()
                .HasForeignKey(s => s.DefaultCashAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(s => s.DefaultAccountsPayableAccount)
                .WithMany()
                .HasForeignKey(s => s.DefaultAccountsPayableAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(s => s.DefaultExpenseAccount)
                .WithMany()
                .HasForeignKey(s => s.DefaultExpenseAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

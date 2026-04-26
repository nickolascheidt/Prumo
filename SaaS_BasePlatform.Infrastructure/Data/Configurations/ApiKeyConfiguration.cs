using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Infrastructure.Data.Configurations
{
    public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
    {
        public void Configure(EntityTypeBuilder<ApiKey> builder)
        {
            builder.ToTable("ApiKeys");

            builder.HasKey(k => k.Id);

            builder.Property(k => k.Name).IsRequired().HasMaxLength(200);
            builder.Property(k => k.Prefix).IsRequired().HasMaxLength(32);
            builder.Property(k => k.KeyHash).IsRequired().HasMaxLength(128);
            builder.Property(k => k.Type).HasConversion<int>();

            builder.HasIndex(k => k.KeyHash).IsUnique();
            builder.HasIndex(k => k.TenantId);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prumo.Domain.Entities;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class SupportAccessLogConfiguration : IEntityTypeConfiguration<SupportAccessLog>
    {
        public void Configure(EntityTypeBuilder<SupportAccessLog> builder)
        {
            builder.ToTable("SupportAccessLogs");

            builder.HasKey(s => s.Id);

            builder.Property(s => s.TenantId).IsRequired();

            builder.Property(s => s.MasterAdminEmail)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(s => s.GrantedAt).IsRequired();

            builder.Property(s => s.Reason).HasMaxLength(500);

            builder.HasIndex(s => s.TenantId);
            builder.HasIndex(s => s.MasterAdminUserId);
            builder.HasIndex(s => s.GrantedAt);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prumo.Domain.Entities;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class TenantInvitationConfiguration : IEntityTypeConfiguration<TenantInvitation>
    {
        public void Configure(EntityTypeBuilder<TenantInvitation> builder)
        {
            builder.ToTable("TenantInvitations");

            builder.HasKey(i => i.Id);

            builder.Property(i => i.TenantId).IsRequired();

            builder.Property(i => i.Email)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(i => i.NormalizedEmail)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(i => i.Role).IsRequired();
            builder.Property(i => i.InvitedByUserId).IsRequired();

            builder.HasOne(i => i.Tenant)
                .WithMany()
                .HasForeignKey(i => i.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // One pending invitation per address in each tenant. The filter is what allows
            // inviting again someone who left: an accepted invitation does not block the next.
            builder.HasIndex(i => new { i.TenantId, i.NormalizedEmail })
                .IsUnique()
                .HasFilter("\"AcceptedAt\" IS NULL");

            // The sign-up query: "is there a pending invitation for this address?" — without
            // a tenant, because someone who just signed up does not have one yet.
            builder.HasIndex(i => i.NormalizedEmail);
        }
    }
}

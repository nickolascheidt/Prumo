using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Infrastructure.Data.Configurations
{
    public class TenantUserRoleConfiguration : IEntityTypeConfiguration<TenantUserRole>
    {
        public void Configure(EntityTypeBuilder<TenantUserRole> builder)
        {
            builder.ToTable("TenantUserRoles");

            builder.HasKey(tur => new { tur.TenantId, tur.UserId, tur.RoleId });

            builder.Property(tur => tur.TenantId).IsRequired();

            builder.HasOne(tur => tur.User)
                .WithMany()
                .HasForeignKey(tur => tur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(tur => tur.Role)
                .WithMany()
                .HasForeignKey(tur => tur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(tur => new { tur.TenantId, tur.UserId });
        }
    }
}

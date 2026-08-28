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

            // Um convite pendente por endereço em cada tenant. O filtro é o que permite
            // convidar de novo alguém que saiu: convite aceito não bloqueia o próximo.
            builder.HasIndex(i => new { i.TenantId, i.NormalizedEmail })
                .IsUnique()
                .HasFilter("\"AcceptedAt\" IS NULL");

            // A consulta do cadastro: "há convite pendente para este endereço?" — sem
            // tenant, porque quem acabou de se cadastrar ainda não tem nenhum.
            builder.HasIndex(i => i.NormalizedEmail);
        }
    }
}

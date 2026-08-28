using Prumo.Domain.Common;
using Prumo.Domain.Enums;

namespace Prumo.Domain.Entities
{
    /// <summary>
    /// Um e-mail que o admin adicionou ao tenant e que ainda não tem conta.
    ///
    /// **Não é um link com token.** O convite não autoriza nada: ele é uma anotação de que
    /// aquele endereço, quando existir, entra neste tenant com este cargo. Quem se cadastra
    /// com o endereço convidado é admitido porque provou ser dono da caixa — a mesma prova
    /// que a confirmação de e-mail exige. Isso é o que permite o aviso por e-mail ser
    /// best-effort: se ele não chegar, ninguém entra em tenant nenhum por engano.
    /// </summary>
    public class TenantInvitation : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        /// <summary>
        /// Guardado **normalizado** (maiúsculas), como o Identity faz com
        /// `NormalizedEmail` — senão "Ana@x.com" e "ana@x.com" viram convites diferentes.
        /// </summary>
        public string NormalizedEmail { get; set; } = string.Empty;

        /// <summary>O endereço como o admin digitou, para exibir na tela.</summary>
        public string Email { get; set; } = string.Empty;

        public TenantRole Role { get; set; } = TenantRole.Member;

        public Guid InvitedByUserId { get; set; }

        /// <summary>Nulo enquanto pendente. Preenchido quando alguém se cadastra com o endereço.</summary>
        public DateTime? AcceptedAt { get; set; }
    }
}

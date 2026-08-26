using Prumo.Domain.Common;

namespace Prumo.Domain.Entities
{
    /// <summary>
    /// Registro de quando o master admin se inseriu num tenant para dar suporte.
    /// </summary>
    /// <remarks>
    /// Decisão de 2026-08-18, executada no item 3B: o suporte do master admin é por
    /// <b>associação auditada, sem bypass</b> — dar a ele um atalho reintroduziria o
    /// vazamento cross-tenant que o RBAC removeu. O rastro é o que torna isso aceitável,
    /// então ele merece entidade própria.
    ///
    /// Antes isto era gravado no <c>PermissionAuditLog</c> com sentinelas
    /// (<c>RoleId = Guid.Empty</c>, <c>PermissionName = "SupportAccess"</c>), porque
    /// aquela tabela era modelada para grants de permissão e não para isto.
    /// </remarks>
    public class SupportAccessLog : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }

        public Guid MasterAdminUserId { get; set; }
        public string MasterAdminEmail { get; set; } = null!;

        public DateTime GrantedAt { get; set; }

        /// <summary>Por que o acesso foi concedido. Texto livre, para leitura humana.</summary>
        public string? Reason { get; set; }
    }
}

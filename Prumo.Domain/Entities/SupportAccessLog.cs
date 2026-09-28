using Prumo.Domain.Common;

namespace Prumo.Domain.Entities
{
    /// <summary>
    /// Record of when the master admin added themselves to a tenant to give support.
    /// </summary>
    /// <remarks>
    /// Master admin support works by <b>audited membership, with no bypass</b> — giving them
    /// a shortcut would reintroduce the cross-tenant leak that RBAC removed. The trail is
    /// what makes that acceptable, so it gets its own entity.
    /// </remarks>
    public class SupportAccessLog : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }

        public Guid MasterAdminUserId { get; set; }
        public string MasterAdminEmail { get; set; } = null!;

        public DateTime GrantedAt { get; set; }

        /// <summary>Why access was granted. Free text, for humans.</summary>
        public string? Reason { get; set; }
    }
}

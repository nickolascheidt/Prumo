using Prumo.Domain.Common;
using Prumo.Domain.Enums;

namespace Prumo.Domain.Entities
{
    /// <summary>
    /// History of access level changes: who granted or removed what, from whom, when.
    /// </summary>
    /// <remarks>
    /// Audits <c>ResourcePermission</c>, which is what actually controls the menu, the
    /// route guards and the API.
    ///
    /// Stores <b>names</b> besides the ids on purpose: deleting a role deletes its
    /// permissions, and without the name copied here the history would turn into a list of
    /// orphaned GUIDs — exactly when you want to know what happened.
    /// </remarks>
    public class ResourcePermissionAuditLog : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }

        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = null!;

        public Guid ResourceId { get; set; }
        public string ResourceCode { get; set; } = null!;

        /// <summary>Level before the change. <c>None</c> when there was no access.</summary>
        public PermissionLevel PreviousLevel { get; set; }

        /// <summary>Level after the change. <c>None</c> when it was revoked.</summary>
        public PermissionLevel NewLevel { get; set; }

        public Guid PerformedByUserId { get; set; }
        public string PerformedByUserEmail { get; set; } = null!;

        public DateTime PerformedAt { get; set; }
    }
}

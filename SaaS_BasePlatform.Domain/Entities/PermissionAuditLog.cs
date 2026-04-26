using SaaS_BasePlatform.Domain.Common;

namespace SaaS_BasePlatform.Domain.Entities
{
    public class PermissionAuditLog : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }

        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = null!;

        public Guid PermissionId { get; set; }
        public string PermissionName { get; set; } = null!;

        public string Action { get; set; } = null!; // "GRANTED" ou "REVOKED"

        public Guid PerformedByUserId { get; set; }
        public string PerformedByUserEmail { get; set; } = null!;

        public DateTime PerformedAt { get; set; }

        public string? Reason { get; set; }
    }
}

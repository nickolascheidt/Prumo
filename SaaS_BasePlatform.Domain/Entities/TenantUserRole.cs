using SaaS_BasePlatform.Domain.Common;

namespace SaaS_BasePlatform.Domain.Entities
{
    /// <summary>
    /// Per-tenant assignment of an Identity role to a user. Replaces global
    /// AspNetUserRoles for feature roles so access is scoped to a single tenant.
    /// </summary>
    public class TenantUserRole : ITenantScoped
    {
        public Guid TenantId { get; set; }

        public Guid UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public Guid RoleId { get; set; }
        public ApplicationRole Role { get; set; } = null!;

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
        public Guid? GrantedByUserId { get; set; }
    }
}

using Prumo.Domain.Common;
using Prumo.Domain.Enums;

namespace Prumo.Domain.Entities
{
    /// <summary>
    /// The access level a Role has on a Resource
    /// </summary>
    public class ResourcePermission : ITenantScoped
    {
        public Guid TenantId { get; set; }

        public Guid RoleId { get; set; }
        public ApplicationRole Role { get; set; } = null!;

        public Guid ResourceId { get; set; }
        public Resource Resource { get; set; } = null!;

        /// <summary>
        /// Granted access level (None, Read, Write, Full)
        /// </summary>
        public PermissionLevel Level { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Guid? CreatedByUserId { get; set; }
        public string? CreatedByUserEmail { get; set; }
    }
}

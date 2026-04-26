using SaaS_BasePlatform.Domain.Common;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Domain.Entities
{
    /// <summary>
    /// Define o nível de acesso que uma Role tem sobre um Resource
    /// </summary>
    public class ResourcePermission : ITenantScoped
    {
        public Guid TenantId { get; set; }

        public Guid RoleId { get; set; }
        public ApplicationRole Role { get; set; } = null!;

        public Guid ResourceId { get; set; }
        public Resource Resource { get; set; } = null!;

        /// <summary>
        /// Nível de acesso concedido (None, Read, Write, Full)
        /// </summary>
        public PermissionLevel Level { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Guid? CreatedByUserId { get; set; }
        public string? CreatedByUserEmail { get; set; }
    }
}

using SaaS_BasePlatform.Domain.Common;

namespace SaaS_BasePlatform.Domain.Entities
{
    public class Permission : EntityBase
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        // Relacionamentos
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}

using Prumo.Domain.Common;

namespace Prumo.Domain.Entities
{
    public class Permission : EntityBase
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        // Relacionamentos
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}

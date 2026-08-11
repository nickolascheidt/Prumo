using Prumo.Domain.Common;

namespace Prumo.Domain.Entities
{
    public class RolePermission : ITenantScoped
    {
        public Guid TenantId { get; set; }

        public Guid RoleId { get; set; }
        public ApplicationRole Role { get; set; } = null!;

        public Guid PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;

        public DateTime GrantedAt { get; set; }
        public Guid? GrantedByUserId { get; set; }
        public string? GrantedByUserEmail { get; set; }
    }
}

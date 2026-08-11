using Prumo.Domain.Enums;

namespace Prumo.Domain.Entities
{
    public class TenantUser
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public Guid UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public TenantRole Role { get; set; } = TenantRole.Member;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}

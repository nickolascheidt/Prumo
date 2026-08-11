using Prumo.Domain.Common;

namespace Prumo.Domain.Entities
{
    public class Tenant : EntityBase
    {
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public Guid OwnerUserId { get; set; }

        public ICollection<TenantUser> Members { get; set; } = new List<TenantUser>();
    }
}

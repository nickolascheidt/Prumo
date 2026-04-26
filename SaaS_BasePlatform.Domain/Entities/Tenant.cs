using SaaS_BasePlatform.Domain.Common;

namespace SaaS_BasePlatform.Domain.Entities
{
    public class Tenant : EntityBase
    {
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public Guid OwnerUserId { get; set; }

        public ICollection<TenantUser> Members { get; set; } = new List<TenantUser>();
        public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
    }
}

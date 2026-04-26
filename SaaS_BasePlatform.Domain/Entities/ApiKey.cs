using SaaS_BasePlatform.Domain.Common;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Domain.Entities
{
    public class ApiKey : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public string Name { get; set; } = null!;

        public string Prefix { get; set; } = null!;

        public string KeyHash { get; set; } = null!;

        public ApiKeyType Type { get; set; }

        public DateTime? ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }

        public Guid CreatedByUserId { get; set; }
    }
}

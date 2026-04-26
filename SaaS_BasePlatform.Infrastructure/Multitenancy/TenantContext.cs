using SaaS_BasePlatform.Domain.Common;

namespace SaaS_BasePlatform.Infrastructure.Multitenancy
{
    public class TenantContext : ITenantContext
    {
        public Guid? TenantId { get; private set; }
        public bool HasTenant => TenantId.HasValue;

        public void SetTenant(Guid tenantId)
        {
            TenantId = tenantId;
        }
    }
}

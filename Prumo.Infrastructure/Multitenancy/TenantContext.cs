using Prumo.Domain.Common;

namespace Prumo.Infrastructure.Multitenancy
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

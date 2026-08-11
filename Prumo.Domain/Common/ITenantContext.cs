namespace Prumo.Domain.Common
{
    public interface ITenantContext
    {
        Guid? TenantId { get; }
        bool HasTenant { get; }
        void SetTenant(Guid tenantId);
    }
}

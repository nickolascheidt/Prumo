namespace SaaS_BasePlatform.Domain.Common
{
    public interface ITenantScoped
    {
        Guid TenantId { get; set; }
    }
}

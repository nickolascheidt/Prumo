namespace SaaS_BasePlatform.Domain.Entities
{
    // Intentionally not EntityBase: PK is TenantId (1:1 with Tenant), not an independent Guid.
    public class TenantGlSettings
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        public Guid? DefaultCashAccountId { get; set; }
        public Account? DefaultCashAccount { get; set; }
        public Guid? DefaultAccountsPayableAccountId { get; set; }
        public Account? DefaultAccountsPayableAccount { get; set; }
    }
}

using Prumo.Domain.Common;

namespace Prumo.Domain.Entities
{
    public class AccountsPayableCategory : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? Color { get; set; }

        public ICollection<AccountsPayableEntry> Entries { get; set; } = new List<AccountsPayableEntry>();
    }
}

using Prumo.Domain.Common;
using Prumo.Domain.Enums;

namespace Prumo.Domain.Entities
{
    public class Account : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AccountType Type { get; set; }
        public bool IsAnalytic { get; set; }
        public Guid? ParentId { get; set; }
        public Account? Parent { get; set; }
        public ICollection<Account> Children { get; set; } = new List<Account>();
        public ICollection<JournalLine> JournalLines { get; set; } = new List<JournalLine>();
    }
}

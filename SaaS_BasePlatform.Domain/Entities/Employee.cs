using SaaS_BasePlatform.Domain.Common;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Domain.Entities
{
    public class Employee : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public string FullName { get; set; } = string.Empty;
        public string CPF { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public DateTime HireDate { get; set; }
        public DateTime? TerminationDate { get; set; }

        public ContractType ContractType { get; set; }
        public decimal HourlyRate { get; set; }

        public HrPaymentMethod PreferredPaymentMethod { get; set; }
        public string? PixKey { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAgency { get; set; }

        public bool HasSignedContract { get; set; }
        public DateTime? ContractSignedDate { get; set; }

        public Guid? ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        public ICollection<WorkLog> WorkLogs { get; set; } = new List<WorkLog>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}

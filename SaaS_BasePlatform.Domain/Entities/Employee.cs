using SaaS_BasePlatform.Domain.Common;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Domain.Entities
{
    public class Employee : EntityBase
    {
        // Dados Pessoais
        public string FullName { get; set; }
        public string CPF { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public DateTime HireDate { get; set; }
        public DateTime? TerminationDate { get; set; }

        // Dados de Trabalho
        public ContractType ContractType { get; set; } // CLT, Frio ou Temporario
        public decimal HourlyRate { get; set; }

        // Dados de Pagamento
        public PaymentMethod PreferredPaymentMethod { get; set; }
        public string? PixKey { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAgency { get; set; }

        // Assinatura do contrato
        public bool HasSignedContract { get; set; }
        public DateTime? ContractSignedDate { get; set; }

        // Relacionamentos
        public ICollection<WorkLog> WorkLogs { get; set; }
        public ICollection<Payment> Payments { get; set; }

        // Opcional: vincular ao usuário do sistema (se tiver acesso)
        public Guid? ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }
    }
}

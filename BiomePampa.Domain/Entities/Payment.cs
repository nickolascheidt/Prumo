using BiomePampa.Domain.Common;
using BiomePampa.Domain.Enums;

namespace BiomePampa.Domain.Entities
{
    public class Payment : EntityBase
    {
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public Guid PaymentPeriodId { get; set; }
        public PaymentPeriod PaymentPeriod { get; set; } = null!;

        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }

        public string? PaymentProof { get; set; }
        public string? Notes { get; set; }

        public Guid PaidByUserId { get; set; }
        public ApplicationUser PaidByUser { get; set; } = null!;
    }
}

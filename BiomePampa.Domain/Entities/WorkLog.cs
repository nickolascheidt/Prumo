using BiomePampa.Domain.Common;

namespace BiomePampa.Domain.Entities
{
    public class WorkLog : EntityBase
    {
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public DateTime WorkDate { get; set; }
        public decimal HoursWorked { get; set; }
        public decimal HourlyRateAtTime { get; set; }
        public decimal TotalAmount { get; set; }

        public string? Notes { get; set; }

        // Relacionamento com período de pagamento
        public Guid? PaymentPeriodId { get; set; }
        public PaymentPeriod? PaymentPeriod { get; set; }
    }
}

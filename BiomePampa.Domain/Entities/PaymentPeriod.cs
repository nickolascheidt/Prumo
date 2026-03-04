using BiomePampa.Domain.Common;
using BiomePampa.Domain.Enums;

namespace BiomePampa.Domain.Entities
{
    public class PaymentPeriod : EntityBase
    {
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public decimal TotalHours { get; set; }
        public decimal TotalAmount { get; set; }

        public PaymentStatus Status { get; set; }

        // Relacionamentos
        public ICollection<WorkLog> WorkLogs { get; set; } = new List<WorkLog>();
        public Payment? Payment { get; set; }
    }
}

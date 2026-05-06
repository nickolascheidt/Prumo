using SaaS_BasePlatform.Domain.Common;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Domain.Entities
{
    public class PaymentPeriod : EntityBase
    {
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public decimal TotalHours { get; set; }
        public decimal TotalAmount { get; set; }

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        public ICollection<WorkLog> WorkLogs { get; set; } = new List<WorkLog>();
        public Payment? Payment { get; set; }

        public static (decimal TotalHours, decimal TotalAmount) CalculateSummary(
            IReadOnlyList<WorkLog> logs)
        {
            if (logs.Count == 0)
                throw new InvalidOperationException(
                    "Cannot generate a payment period without work logs.");
            return (logs.Sum(l => l.HoursWorked), logs.Sum(l => l.TotalAmount));
        }
    }
}

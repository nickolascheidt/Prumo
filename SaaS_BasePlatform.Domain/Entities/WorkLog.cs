using SaaS_BasePlatform.Domain.Common;

namespace SaaS_BasePlatform.Domain.Entities
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

        public Guid? PaymentPeriodId { get; set; }
        public PaymentPeriod? PaymentPeriod { get; set; }

        public static decimal CalculateTotalAmount(decimal hoursWorked, decimal hourlyRate)
        {
            if (hoursWorked <= 0) throw new ArgumentException("Hours worked must be greater than zero.");
            if (hourlyRate <= 0) throw new ArgumentException("Hourly rate must be greater than zero.");
            return Math.Round(hoursWorked * hourlyRate, 2);
        }
    }
}

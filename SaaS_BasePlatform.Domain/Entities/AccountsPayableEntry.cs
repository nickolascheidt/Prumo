using SaaS_BasePlatform.Domain.Common;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Domain.Entities
{
    public class AccountsPayableEntry : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public string Description { get; set; } = null!;
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }

        public Guid CategoryId { get; set; }
        public AccountsPayableCategory Category { get; set; } = null!;

        public AccountsPayableStatus Status { get; set; } = AccountsPayableStatus.Pending;

        public DateTime? PaidAt { get; set; }
        public PaymentMethod? PaymentMethod { get; set; }

        public string? SupplierName { get; set; }
        public string? Notes { get; set; }

        public DateTime? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }

        public Guid CreatedByUserId { get; set; }

        public void MarkPaid(DateTime paidAt, PaymentMethod paymentMethod)
        {
            if (Status == AccountsPayableStatus.Cancelled)
                throw new InvalidOperationException("Cannot mark a cancelled entry as paid.");
            if (Status == AccountsPayableStatus.Paid)
                throw new InvalidOperationException("Entry is already marked as paid.");

            Status = AccountsPayableStatus.Paid;
            PaidAt = paidAt;
            PaymentMethod = paymentMethod;
        }

        public void Cancel(string reason, DateTime cancelledAt)
        {
            if (Status == AccountsPayableStatus.Paid)
                throw new InvalidOperationException("Cannot cancel an entry that is already paid.");
            if (Status == AccountsPayableStatus.Cancelled)
                throw new InvalidOperationException("Entry is already cancelled.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Cancellation reason is required.", nameof(reason));

            Status = AccountsPayableStatus.Cancelled;
            CancelledAt = cancelledAt;
            CancellationReason = reason;
        }
    }
}

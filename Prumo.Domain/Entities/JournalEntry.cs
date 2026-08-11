using Prumo.Domain.Common;
using Prumo.Domain.Enums;

namespace Prumo.Domain.Entities
{
    public class JournalEntry : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? SourceModule { get; set; }
        public Guid? SourceDocumentId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();

        public static void ValidateBalance(IReadOnlyList<JournalLine> lines)
        {
            if (lines.Count < 2)
                throw new InvalidOperationException("A journal entry must have at least 2 lines.");

            var totalDebits  = lines.Where(l => l.EntryType == JournalEntryType.Debit).Sum(l => l.Amount);
            var totalCredits = lines.Where(l => l.EntryType == JournalEntryType.Credit).Sum(l => l.Amount);

            if (totalDebits != totalCredits)
                throw new InvalidOperationException(
                    $"Journal entry is unbalanced: debits {totalDebits:F2} ≠ credits {totalCredits:F2}.");
        }
    }
}

using Prumo.Domain.Enums;

namespace Prumo.Domain.Entities
{
    // Intentionally not EntityBase: journal lines are immutable accounting records.
    public class JournalLine
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid JournalEntryId { get; set; }
        public JournalEntry JournalEntry { get; set; } = null!;
        public Guid AccountId { get; set; }
        public Account Account { get; set; } = null!;
        public JournalEntryType EntryType { get; set; }
        public decimal Amount { get; set; }
    }
}

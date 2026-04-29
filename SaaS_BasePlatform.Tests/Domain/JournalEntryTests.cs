using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Tests.Domain;

public class JournalEntryTests
{
    [Fact]
    public void ValidateBalance_WhenDebitsEqualCredits_DoesNotThrow()
    {
        var lines = new List<JournalLine>
        {
            new() { EntryType = JournalEntryType.Debit,  Amount = 100m },
            new() { EntryType = JournalEntryType.Credit, Amount = 100m }
        };

        var ex = Record.Exception(() => JournalEntry.ValidateBalance(lines));

        Assert.Null(ex);
    }

    [Fact]
    public void ValidateBalance_WhenUnbalanced_ThrowsInvalidOperationException()
    {
        var lines = new List<JournalLine>
        {
            new() { EntryType = JournalEntryType.Debit,  Amount = 100m },
            new() { EntryType = JournalEntryType.Credit, Amount = 90m }
        };

        Assert.Throws<InvalidOperationException>(() => JournalEntry.ValidateBalance(lines));
    }

    [Fact]
    public void ValidateBalance_WithOnlyOneLine_ThrowsInvalidOperationException()
    {
        var lines = new List<JournalLine>
        {
            new() { EntryType = JournalEntryType.Debit, Amount = 100m }
        };

        Assert.Throws<InvalidOperationException>(() => JournalEntry.ValidateBalance(lines));
    }

    [Fact]
    public void ValidateBalance_WithMultipleBalancedLines_DoesNotThrow()
    {
        var lines = new List<JournalLine>
        {
            new() { EntryType = JournalEntryType.Debit,  Amount = 60m },
            new() { EntryType = JournalEntryType.Debit,  Amount = 40m },
            new() { EntryType = JournalEntryType.Credit, Amount = 100m }
        };

        var ex = Record.Exception(() => JournalEntry.ValidateBalance(lines));

        Assert.Null(ex);
    }
}

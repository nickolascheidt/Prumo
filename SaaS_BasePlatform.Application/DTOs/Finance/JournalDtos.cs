namespace SaaS_BasePlatform.Application.DTOs.Finance
{
    public record JournalLineDto(
        Guid Id,
        Guid AccountId,
        string AccountCode,
        string AccountName,
        int EntryType,
        decimal Amount
    );

    public record JournalEntryDto(
        Guid Id,
        Guid TenantId,
        DateTime Date,
        string Description,
        string? SourceModule,
        Guid? SourceDocumentId,
        Guid CreatedByUserId,
        DateTime CreatedAt,
        IReadOnlyList<JournalLineDto> Lines
    );

    public record JournalEntryListItemDto(
        Guid Id,
        DateTime Date,
        string Description,
        string? SourceModule,
        decimal TotalAmount,
        int LineCount,
        DateTime CreatedAt
    );

    public record CreateJournalLineDto(
        Guid AccountId,
        int EntryType,
        decimal Amount
    );

    public record CreateJournalEntryRequestDto(
        DateTime Date,
        string Description,
        IReadOnlyList<CreateJournalLineDto> Lines
    );

    public record JournalEntryQueryDto(
        DateTime? From = null,
        DateTime? To = null,
        string? SourceModule = null,
        int Page = 1,
        int PageSize = 20
    );

    public record AccountStatementLineDto(
        Guid JournalEntryId,
        DateTime Date,
        string Description,
        int EntryType,
        decimal Amount,
        decimal RunningBalance
    );

    public record AccountStatementDto(
        Guid AccountId,
        string AccountCode,
        string AccountName,
        DateTime? From,
        DateTime? To,
        decimal OpeningBalance,
        IReadOnlyList<AccountStatementLineDto> Lines,
        decimal ClosingBalance
    );
}

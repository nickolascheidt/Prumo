using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Application.DTOs.AccountsPayable
{
    public record BulkEntryLineDto(
        string Description,
        decimal Amount,
        DateTime DueDate,
        Guid? CategoryId,
        string? CategoryName,
        PaymentMethod? PaymentMethod,
        string? SupplierName,
        string? Notes);

    public record BulkEntriesRequestDto(
        IReadOnlyList<BulkEntryLineDto> Lines);

    public record BulkEntryResultDto(
        int Index,
        bool Success,
        Guid? EntryId,
        IReadOnlyList<string> Errors);

    public record BulkEntriesResponseDto(
        int TotalLines,
        int SuccessCount,
        int FailedCount,
        IReadOnlyList<BulkEntryResultDto> Results);
}

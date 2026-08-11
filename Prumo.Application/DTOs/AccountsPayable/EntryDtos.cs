using Prumo.Domain.Enums;

namespace Prumo.Application.DTOs.AccountsPayable
{
    public record CreateEntryRequestDto(
        string Description,
        decimal Amount,
        DateTime DueDate,
        Guid CategoryId,
        PaymentMethod? PaymentMethod,
        string? SupplierName,
        string? Notes);

    public record UpdateEntryRequestDto(
        string Description,
        decimal Amount,
        DateTime DueDate,
        Guid CategoryId,
        PaymentMethod? PaymentMethod,
        string? SupplierName,
        string? Notes);

    public record MarkPaidRequestDto(
        DateTime PaidAt,
        PaymentMethod PaymentMethod);

    public record CancelEntryRequestDto(
        string Reason);

    public record EntryDto(
        Guid Id,
        string Description,
        decimal Amount,
        DateTime DueDate,
        Guid CategoryId,
        string CategoryName,
        AccountsPayableStatus Status,
        DateTime? PaidAt,
        PaymentMethod? PaymentMethod,
        string? SupplierName,
        string? Notes,
        DateTime? CancelledAt,
        string? CancellationReason,
        bool IsOverdue,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    public record EntryListQueryDto
    {
        public DateTime? From { get; init; }
        public DateTime? To { get; init; }
        public AccountsPayableStatus? Status { get; init; }
        public Guid? CategoryId { get; init; }
        public string? SupplierName { get; init; }
        public PaymentMethod? PaymentMethod { get; init; }
        public string? Search { get; init; }
        public decimal? MinAmount { get; init; }
        public decimal? MaxAmount { get; init; }

        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 20;

        public string? SortBy { get; init; }
        public string? SortDir { get; init; }
    }
}

namespace SaaS_BasePlatform.Application.DTOs.AccountsPayable
{
    public record SummaryQueryDto
    {
        public DateTime? From { get; init; }
        public DateTime? To { get; init; }
    }

    public record CategoryTotalsDto(
        Guid CategoryId,
        string CategoryName,
        decimal TotalPending,
        decimal TotalPaid,
        decimal TotalCancelled);

    public record SummaryResponseDto(
        DateTime? From,
        DateTime? To,
        decimal TotalPending,
        decimal TotalPaid,
        decimal TotalCancelled,
        int CountPending,
        int CountPaid,
        int CountCancelled,
        IReadOnlyList<CategoryTotalsDto> TotalsByCategory);
}

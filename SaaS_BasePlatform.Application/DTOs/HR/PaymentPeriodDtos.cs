namespace SaaS_BasePlatform.Application.DTOs.HR
{
    public record PaymentPeriodDto(
        Guid Id,
        Guid EmployeeId,
        string EmployeeName,
        DateTime StartDate,
        DateTime EndDate,
        decimal TotalHours,
        decimal TotalAmount,
        int Status,
        string StatusName,
        IReadOnlyList<WorkLogDto> WorkLogs,
        DateTime CreatedAt
    );

    public record PaymentPeriodSummaryDto(
        Guid Id,
        Guid EmployeeId,
        string EmployeeName,
        DateTime StartDate,
        DateTime EndDate,
        decimal TotalHours,
        decimal TotalAmount,
        int Status,
        string StatusName,
        DateTime CreatedAt
    );

    public record GeneratePaymentPeriodRequestDto(
        Guid EmployeeId,
        DateTime StartDate,
        DateTime EndDate
    );

    public record UpdatePaymentPeriodStatusDto(int Status);
}

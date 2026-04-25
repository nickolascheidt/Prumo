using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Application.DTOs.Employee
{
    public record PaymentPeriodDto(
        Guid Id,
        Guid EmployeeId,
        string EmployeeName,
        DateTime StartDate,
        DateTime EndDate,
        decimal TotalHours,
        decimal TotalAmount,
        PaymentStatus Status,
        PaymentDto? Payment,
        List<WorkLogDto> WorkLogs,
        DateTime CreatedAt
    );

    public record CreatePaymentPeriodDto(
        Guid EmployeeId,
        DateTime StartDate,
        DateTime EndDate
    );

    public record PaymentPeriodSummaryDto(
        Guid Id,
        Guid EmployeeId,
        string EmployeeName,
        DateTime StartDate,
        DateTime EndDate,
        decimal TotalAmount,
        PaymentStatus Status
    );
}

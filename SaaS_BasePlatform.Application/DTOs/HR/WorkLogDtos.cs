namespace SaaS_BasePlatform.Application.DTOs.HR
{
    public record WorkLogDto(
        Guid Id,
        Guid EmployeeId,
        string EmployeeName,
        DateTime WorkDate,
        decimal HoursWorked,
        decimal HourlyRateAtTime,
        decimal TotalAmount,
        string? Notes,
        Guid? PaymentPeriodId,
        DateTime CreatedAt
    );

    public record CreateWorkLogRequestDto(
        Guid EmployeeId,
        DateTime WorkDate,
        decimal HoursWorked,
        string? Notes
    );

    public record UpdateWorkLogRequestDto(
        DateTime WorkDate,
        decimal HoursWorked,
        string? Notes
    );

    public record WorkLogQueryDto(
        DateTime? From = null,
        DateTime? To = null,
        bool OnlyUnassigned = false
    );
}

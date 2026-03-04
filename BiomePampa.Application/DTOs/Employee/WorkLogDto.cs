namespace BiomePampa.Application.DTOs.Employee
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

    public record CreateWorkLogDto(
        Guid EmployeeId,
        DateTime WorkDate,
        decimal HoursWorked,
        string? Notes
    );

    public record UpdateWorkLogDto(
        DateTime WorkDate,
        decimal HoursWorked,
        string? Notes
    );
}

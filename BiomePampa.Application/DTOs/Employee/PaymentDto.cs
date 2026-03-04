using BiomePampa.Domain.Enums;

namespace BiomePampa.Application.DTOs.Employee
{
    public record PaymentDto(
        Guid Id,
        Guid EmployeeId,
        string EmployeeName,
        Guid PaymentPeriodId,
        DateTime PaymentDate,
        decimal Amount,
        PaymentMethod PaymentMethod,
        string? PaymentProof,
        string? Notes,
        Guid PaidByUserId,
        string PaidByUserName,
        DateTime CreatedAt
    );

    public record CreatePaymentDto(
        Guid PaymentPeriodId,
        DateTime PaymentDate,
        PaymentMethod PaymentMethod,
        string? PaymentProof,
        string? Notes
    );

    public record PaymentSummaryDto(
        Guid Id,
        string EmployeeName,
        DateTime PaymentDate,
        decimal Amount,
        PaymentMethod PaymentMethod,
        string PaidByUserName
    );
}

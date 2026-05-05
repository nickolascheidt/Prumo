namespace SaaS_BasePlatform.Application.DTOs.HR
{
    public record PaymentDto(
        Guid Id,
        Guid EmployeeId,
        string EmployeeName,
        Guid PaymentPeriodId,
        DateTime PaymentDate,
        decimal Amount,
        int PaymentMethod,
        string PaymentMethodName,
        string? PaymentProof,
        string? Notes,
        Guid PaidByUserId,
        string PaidByUserName,
        DateTime CreatedAt
    );

    public record CreatePaymentRequestDto(
        Guid PaymentPeriodId,
        DateTime PaymentDate,
        int PaymentMethod,
        string? PaymentProof,
        string? Notes
    );
}

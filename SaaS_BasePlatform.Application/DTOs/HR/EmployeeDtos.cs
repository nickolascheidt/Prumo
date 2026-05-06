namespace SaaS_BasePlatform.Application.DTOs.HR
{
    public record EmployeeDto(
        Guid Id,
        Guid TenantId,
        string FullName,
        string CPF,
        string? Phone,
        string? Email,
        DateTime HireDate,
        DateTime? TerminationDate,
        bool IsActive,
        int ContractType,
        string ContractTypeName,
        decimal HourlyRate,
        int PreferredPaymentMethod,
        string PreferredPaymentMethodName,
        string? PixKey,
        string? BankName,
        string? BankAccountNumber,
        string? BankAgency,
        bool HasSignedContract,
        DateTime? ContractSignedDate,
        Guid? ApplicationUserId,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    public record CreateEmployeeRequestDto(
        string FullName,
        string CPF,
        string? Phone,
        string? Email,
        DateTime HireDate,
        int ContractType,
        decimal HourlyRate,
        int PreferredPaymentMethod,
        string? PixKey,
        string? BankName,
        string? BankAccountNumber,
        string? BankAgency,
        bool HasSignedContract,
        Guid? ApplicationUserId
    );

    public record UpdateEmployeeRequestDto(
        string FullName,
        string? Phone,
        string? Email,
        bool IsActive,
        int ContractType,
        decimal HourlyRate,
        int PreferredPaymentMethod,
        string? PixKey,
        string? BankName,
        string? BankAccountNumber,
        string? BankAgency,
        bool HasSignedContract,
        DateTime? TerminationDate
    );
}

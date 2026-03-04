using BiomePampa.Domain.Enums;

namespace BiomePampa.Application.DTOs.Employee
{
    public record EmployeeDto(
        Guid Id,
        string FullName,
        string CPF,
        string? Phone,
        string? Email,
        DateTime HireDate,
        DateTime? TerminationDate,
        bool IsActive,
        ContractType ContractType,
        decimal HourlyRate,
        PaymentMethod PreferredPaymentMethod,
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

    public record CreateEmployeeDto(
        string FullName,
        string CPF,
        string? Phone,
        string? Email,
        DateTime HireDate,
        ContractType ContractType,
        decimal HourlyRate,
        PaymentMethod PreferredPaymentMethod,
        string? PixKey,
        string? BankName,
        string? BankAccountNumber,
        string? BankAgency,
        bool HasSignedContract,
        Guid? ApplicationUserId
    );

    public record UpdateEmployeeDto(
        string FullName,
        string? Phone,
        string? Email,
        bool IsActive,
        ContractType ContractType,
        decimal HourlyRate,
        PaymentMethod PreferredPaymentMethod,
        string? PixKey,
        string? BankName,
        string? BankAccountNumber,
        string? BankAgency,
        bool HasSignedContract,
        DateTime? TerminationDate
    );

    public record EmployeeSummaryDto(
        Guid Id,
        string FullName,
        string CPF,
        bool IsActive,
        ContractType ContractType,
        decimal HourlyRate
    );
}

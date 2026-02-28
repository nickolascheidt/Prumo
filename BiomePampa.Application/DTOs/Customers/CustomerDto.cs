namespace BiomePampa.Application.DTOs.Customers
{
    public record CustomerDto(
        Guid Id,
        string Name,
        string? CompanyName,
        string TaxId,
        string? Phone,
        string? Email,
        string? Address,
        string? City,
        string? State,
        string? ZipCode,
        string? Notes,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    public record CreateCustomerDto(
        string Name,
        string? CompanyName,
        string TaxId,
        string? Phone,
        string? Email,
        string? Address,
        string? City,
        string? State,
        string? ZipCode,
        string? Notes
    );

    public record UpdateCustomerDto(
        string Name,
        string? CompanyName,
        string TaxId,
        string? Phone,
        string? Email,
        string? Address,
        string? City,
        string? State,
        string? ZipCode,
        string? Notes
    );
}

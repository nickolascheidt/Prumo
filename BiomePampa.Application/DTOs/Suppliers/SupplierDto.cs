namespace BiomePampa.Application.DTOs.Suppliers
{
    public record SupplierDto(
        Guid Id,
        string Name,
        string CompanyName,
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

    public record CreateSupplierDto(
        string Name,
        string CompanyName,
        string TaxId,
        string? Phone,
        string? Email,
        string? Address,
        string? City,
        string? State,
        string? ZipCode,
        string? Notes
    );

    public record UpdateSupplierDto(
        string Name,
        string CompanyName,
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

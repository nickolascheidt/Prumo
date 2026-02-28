namespace BiomePampa.Application.DTOs.Batches
{
    public record BatchDto(
        Guid Id,
        string BatchNumber,
        Guid ProductId,
        string ProductName,
        DateTime ProductionDate,
        DateTime ExpirationDate,
        decimal InitialQuantity,
        decimal CurrentQuantity,
        Guid? SupplierId,
        string? SupplierName,
        string? Notes,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    public record CreateBatchDto(
        string BatchNumber,
        Guid ProductId,
        DateTime ProductionDate,
        DateTime ExpirationDate,
        decimal InitialQuantity,
        Guid? SupplierId,
        string? Notes
    );

    public record UpdateBatchDto(
        string BatchNumber,
        DateTime ProductionDate,
        DateTime ExpirationDate,
        decimal CurrentQuantity,
        Guid? SupplierId,
        string? Notes
    );
}

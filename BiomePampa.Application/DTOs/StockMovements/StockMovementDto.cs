using BiomePampa.Domain.Enums;

namespace BiomePampa.Application.DTOs.StockMovements
{
    public record StockMovementDto(
        Guid Id,
        Guid ProductId,
        string ProductName,
        Guid? BatchId,
        string? BatchNumber,
        MovementType MovementType,
        decimal Quantity,
        DateTime MovementDate,
        string? ResponsiblePerson,
        string? Notes,
        Guid? SupplierId,
        string? SupplierName,
        Guid? CustomerId,
        string? CustomerName,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    public record CreateStockMovementDto(
        Guid ProductId,
        Guid? BatchId,
        MovementType MovementType,
        decimal Quantity,
        DateTime MovementDate,
        string? ResponsiblePerson,
        string? Notes,
        Guid? SupplierId,
        Guid? CustomerId
    );
}

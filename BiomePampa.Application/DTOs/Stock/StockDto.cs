namespace BiomePampa.Application.DTOs.Stock
{
    public record StockDto(
        Guid Id,
        Guid ProductId,
        string ProductName,
        decimal AvailableQuantity,
        decimal ReservedQuantity,
        string? Location,
        DateTime LastUpdate,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    public record UpdateStockDto(
        decimal AvailableQuantity,
        decimal ReservedQuantity,
        string? Location
    );
}

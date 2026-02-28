using BiomePampa.Domain.Enums;

namespace BiomePampa.Application.DTOs.Products
{
    public record ProductDto(
        Guid Id,
        string Name,
        string Description,
        string SKU,
        OliveOilType OliveOilType,
        decimal Volume,
        string? Barcode,
        decimal MinimumStock,
        decimal MaximumStock,
        decimal UnitPrice,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    public record CreateProductDto(
        string Name,
        string Description,
        string SKU,
        OliveOilType OliveOilType,
        decimal Volume,
        string? Barcode,
        decimal MinimumStock,
        decimal MaximumStock,
        decimal UnitPrice
    );

    public record UpdateProductDto(
        string Name,
        string Description,
        string SKU,
        OliveOilType OliveOilType,
        decimal Volume,
        string? Barcode,
        decimal MinimumStock,
        decimal MaximumStock,
        decimal UnitPrice
    );
}

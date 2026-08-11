namespace Prumo.Application.DTOs.AccountsPayable
{
    public record CreateCategoryRequestDto(
        string Name,
        string? Description,
        string? Color);

    public record UpdateCategoryRequestDto(
        string Name,
        string? Description,
        string? Color,
        bool IsActive);

    public record CategoryDto(
        Guid Id,
        string Name,
        string? Description,
        string? Color,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt);
}

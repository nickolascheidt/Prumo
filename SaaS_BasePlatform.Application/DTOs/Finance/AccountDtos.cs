namespace SaaS_BasePlatform.Application.DTOs.Finance
{
    public record AccountDto(
        Guid Id,
        Guid TenantId,
        string Code,
        string Name,
        int Type,
        string TypeName,
        bool IsAnalytic,
        Guid? ParentId,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    public record CreateAccountRequestDto(
        string Code,
        string Name,
        int Type,
        bool IsAnalytic,
        Guid? ParentId
    );

    public record UpdateAccountRequestDto(
        string Code,
        string Name,
        int Type,
        bool IsAnalytic,
        Guid? ParentId,
        bool IsActive
    );
}

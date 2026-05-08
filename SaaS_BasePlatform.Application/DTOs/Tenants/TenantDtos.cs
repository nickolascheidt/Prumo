using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Application.DTOs.Tenants
{
    public record CreateTenantRequestDto(string Name, string Slug);

    public record TenantDto(
        Guid Id,
        string Name,
        string Slug,
        Guid OwnerUserId,
        DateTime CreatedAt);

    public record TenantMembershipDto(
        Guid TenantId,
        string TenantName,
        string TenantSlug,
        TenantRole Role,
        DateTime JoinedAt);

    public record AddTenantMemberRequestDto(Guid UserId, TenantRole Role);

    public record TenantMemberDto(
        Guid UserId,
        string Email,
        string? FullName,
        TenantRole Role,
        DateTime JoinedAt);

    public record UserLookupDto(Guid UserId, string Email, string? FullName);

    public record UpdateMemberRoleDto(TenantRole Role);
}

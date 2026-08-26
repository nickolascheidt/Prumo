using Prumo.Domain.Enums;

namespace Prumo.Application.DTOs.Tenants
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

    /// <param name="Role">Cargo administrativo no tenant (Owner/Admin/Member).</param>
    /// <param name="Roles">Feature roles concedidas <b>neste</b> tenant — as "chaves de módulo".</param>
    /// <param name="IsMasterAdmin">
    /// Role global do Identity. Fica fora de <paramref name="Roles"/> de propósito: a tela de
    /// membros gerencia feature roles do tenant, e o master não é revogável por lá.
    /// </param>
    public record TenantMemberDto(
        Guid UserId,
        string Email,
        string? FullName,
        TenantRole Role,
        DateTime JoinedAt,
        IReadOnlyList<string> Roles,
        bool IsMasterAdmin);

    public record CreateTenantUserDto(
        string Email,
        string Password,
        string FullName,
        string? Phone,
        TenantRole Role);

    public record UpdateMemberRoleDto(TenantRole Role);

    public record TenantMemberRolesDto(Guid UserId, IReadOnlyList<string> Roles);

    public record AssignFeatureRoleDto(string RoleName);
}

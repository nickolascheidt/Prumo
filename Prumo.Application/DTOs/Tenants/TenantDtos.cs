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

    /// <param name="Role">Administrative position in the tenant (Owner/Admin/Member).</param>
    /// <param name="Roles">Feature roles granted in <b>this</b> tenant — the "module keys".</param>
    /// <param name="IsMasterAdmin">
    /// Global Identity role. Kept out of <paramref name="Roles"/> on purpose: the members
    /// screen manages the tenant's feature roles, and the master role cannot be revoked there.
    /// </param>
    public record TenantMemberDto(
        Guid UserId,
        string Email,
        string? FullName,
        TenantRole Role,
        DateTime JoinedAt,
        IReadOnlyList<string> Roles,
        bool IsMasterAdmin);

    /// <summary>
    /// The admin invites by e-mail and picks the position. **There is no password here** —
    /// the person sets their own password when signing up.
    /// </summary>
    public record InviteMemberRequestDto(
        string Email,
        TenantRole Role);

    /// <param name="JoinedImmediately">
    /// True when the e-mail already had an account and the person became a member right
    /// away; false when a pending invitation was left, waiting for sign-up. The screen uses
    /// this to say which of the two happened.
    /// </param>
    public record InviteMemberResultDto(
        bool JoinedImmediately,
        Guid? UserId,
        string Email);

    public record TenantInvitationDto(
        Guid Id,
        string Email,
        TenantRole Role,
        DateTime CreatedAt);

    public record UpdateMemberRoleDto(TenantRole Role);

    public record TenantMemberRolesDto(Guid UserId, IReadOnlyList<string> Roles);

    public record AssignFeatureRoleDto(string RoleName);
}

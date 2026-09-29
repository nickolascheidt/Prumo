using Prumo.Domain.Enums;

namespace Prumo.Application.DTOs.Roles
{
    public record CreateTenantRoleDto(string Name, string? Description);

    /// <param name="Level"><c>None</c> revokes the role's access to the resource.</param>
    public record SetResourceLevelDto(PermissionLevel Level);

    /// <param name="IsCanonical">
    /// System role (null <c>TenantId</c>): visible in every tenant, cannot be deleted by
    /// anyone.
    /// </param>
    /// <param name="MemberCount">How many members of <b>this</b> tenant carry the role.</param>
    public record TenantRoleDto(
        Guid Id,
        string Name,
        string? Description,
        bool IsCanonical,
        int MemberCount);
}

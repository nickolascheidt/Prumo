namespace SaaS_BasePlatform.Application.DTOs.Auth
{
    public record AssignRoleDto(
        string Email,
        string RoleName
    );

    public record RemoveRoleDto(
        string Email,
        string RoleName
    );

    public record UserRolesDto(
        Guid UserId,
        string Email,
        string FullName,
        IEnumerable<string> Roles
    );

    public record UserLookupDto(
        Guid UserId,
        string Email,
        string? FullName
    );
}

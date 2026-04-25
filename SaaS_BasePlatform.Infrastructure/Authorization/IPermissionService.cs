using SaaS_BasePlatform.Domain.DTOs;
using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Infrastructure.Authorization
{
    public interface IPermissionService
    {
        Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyCollection<string>> GetRolePermissionsAsync(string roleName, CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<Permission>> GetAllPermissionsAsync(CancellationToken cancellationToken = default);
        Task<RolePermissionsDto> GetRolePermissionsDetailedAsync(string roleName, CancellationToken cancellationToken = default);
        Task GrantPermissionToRoleAsync(string roleName, string permissionName, Guid performedByUserId, string performedByUserEmail, string? reason = null, CancellationToken cancellationToken = default);
        Task RevokePermissionFromRoleAsync(string roleName, string permissionName, Guid performedByUserId, string performedByUserEmail, string? reason = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyCollection<PermissionAuditDto>> GetAuditLogsAsync(string? roleName = null, int take = 100, CancellationToken cancellationToken = default);
    }
}

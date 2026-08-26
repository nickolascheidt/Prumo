using Prumo.Application.DTOs.Roles;

namespace Prumo.Application.Services
{
    /// <summary>
    /// Administração das roles que um tenant pode criar para si.
    /// </summary>
    /// <remarks>
    /// Tudo aqui resolve role por <c>RoleId</c>, nunca por nome:
    /// <c>RoleManager.FindByNameAsync</c> pressupõe nome único no sistema inteiro e, com
    /// homônimas em tenants diferentes, devolveria uma delas arbitrariamente.
    /// </remarks>
    public interface ITenantRoleAdminService
    {
        /// <summary>As canônicas do sistema mais as criadas por este tenant.</summary>
        Task<IReadOnlyList<TenantRoleDto>> GetVisibleRolesAsync(Guid tenantId, CancellationToken ct = default);

        Task<TenantRoleDto> CreateAsync(Guid tenantId, CreateTenantRoleDto dto, CancellationToken ct = default);

        Task DeleteAsync(Guid tenantId, Guid roleId, CancellationToken ct = default);
    }
}

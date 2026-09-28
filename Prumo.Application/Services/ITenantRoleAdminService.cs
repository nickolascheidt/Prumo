using Prumo.Application.DTOs.Roles;

namespace Prumo.Application.Services
{
    /// <summary>
    /// Management of the roles a tenant can create for itself.
    /// </summary>
    /// <remarks>
    /// Everything here resolves a role by <c>RoleId</c>, never by name:
    /// <c>RoleManager.FindByNameAsync</c> assumes names are unique across the whole system
    /// and, with same-named roles in different tenants, would return one of them
    /// arbitrarily.
    /// </remarks>
    public interface ITenantRoleAdminService
    {
        /// <summary>The system's canonical roles plus the ones created by this tenant.</summary>
        Task<IReadOnlyList<TenantRoleDto>> GetVisibleRolesAsync(Guid tenantId, CancellationToken ct = default);

        /// <summary>
        /// Names a tenant admin can assign to a member: the canonical feature roles plus
        /// the ones the tenant created itself.
        /// </summary>
        /// <remarks>
        /// The master admin is left out on purpose — this list answers "what can a tenant
        /// admin grant", and the global master role cannot be granted from here.
        /// </remarks>
        Task<IReadOnlyList<string>> GetAssignableRoleNamesAsync(Guid tenantId, CancellationToken ct = default);

        Task<TenantRoleDto> CreateAsync(Guid tenantId, CreateTenantRoleDto dto, CancellationToken ct = default);

        Task DeleteAsync(Guid tenantId, Guid roleId, CancellationToken ct = default);
    }
}

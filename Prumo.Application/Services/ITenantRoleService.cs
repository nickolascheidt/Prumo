namespace Prumo.Application.Services
{
    public interface ITenantRoleService
    {
        /// <summary>Global roles (e.g. master Administrador) ∪ per-tenant feature roles.</summary>
        Task<IReadOnlyList<string>> GetEffectiveRoleNamesAsync(
            Guid userId, Guid tenantId, IReadOnlyCollection<string> globalRoleNames,
            CancellationToken ct = default);

        /// <summary>Per-tenant feature role names for a user (no global roles).</summary>
        Task<IReadOnlyList<string>> GetTenantRoleNamesAsync(
            Guid userId, Guid tenantId, CancellationToken ct = default);

        Task AssignFeatureRoleAsync(Guid tenantId, Guid userId, string roleName,
            Guid? grantedByUserId = null, CancellationToken ct = default);

        Task RevokeFeatureRoleAsync(Guid tenantId, Guid userId, string roleName,
            CancellationToken ct = default);
    }
}

using Microsoft.EntityFrameworkCore;
using Prumo.Domain.Authorization;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;

namespace Prumo.Application.Services
{
    public class TenantRoleService : ITenantRoleService
    {
        private readonly ApplicationDbContext _db;

        public TenantRoleService(ApplicationDbContext db) => _db = db;

        public async Task<IReadOnlyList<string>> GetTenantRoleNamesAsync(
            Guid userId, Guid tenantId, CancellationToken ct = default)
        {
            // This method runs while the token is being issued, in POST /tenants/select and
            // at login, when the TenantContext is still empty: the middleware fills it from
            // the `tenant_id` claim, which is exactly what the token being issued does not
            // have yet. TenantUserRole is ITenantScoped, so without the bypass the
            // fail-closed filter would return an empty list and the user would get a token
            // with no feature role at all — an empty menu.
            //
            // So: cross-tenant on purpose. The tenantId comes from the parameter and is
            // filtered in the Where below. Covered by TenantRoleServiceTenantlessTests.
            return await _db.TenantUserRoles
                .IgnoreQueryFilters()
                .Where(tur => tur.TenantId == tenantId && tur.UserId == userId)
                .Select(tur => tur.Role.Name!)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<string>> GetEffectiveRoleNamesAsync(
            Guid userId, Guid tenantId, IReadOnlyCollection<string> globalRoleNames,
            CancellationToken ct = default)
        {
            var tenantRoles = await GetTenantRoleNamesAsync(userId, tenantId, ct);
            return globalRoleNames.Concat(tenantRoles).Distinct().ToList();
        }

        public async Task AssignFeatureRoleAsync(
            Guid tenantId, Guid userId, string roleName,
            Guid? grantedByUserId = null, CancellationToken ct = default)
        {
            // Assignable = canonical from the Domain list, or a role created by THIS tenant.
            // The tenant filter is not decoration: without it an Owner could grant another
            // tenant's role just by knowing its name, and with same-named roles resolving
            // by name would pick the wrong row.
            // Cross-tenant: ApplicationRole is not ITenantScoped (canonical roles have a
            // null TenantId and must apply in every tenant); the filter is the Where below.
            var role = await _db.Roles.FirstOrDefaultAsync(
                r => r.Name == roleName && (r.TenantId == null || r.TenantId == tenantId), ct);

            var isCanonicalAssignable =
                role is not null && role.TenantId == null &&
                Permissions.Roles.AssignableFeatureRoles.Contains(roleName);

            var isOwnedByThisTenant = role is not null && role.TenantId == tenantId;

            if (role is null || (!isCanonicalAssignable && !isOwnedByThisTenant))
                throw new InvalidOperationException(
                    $"'{roleName}' is not an assignable tenant feature role.");

            // Cross-tenant on purpose: TenantsController does not use [TenantModule] — it
            // gates with GetUserRoleAsync(tenantId from the ROUTE), while the TenantContext
            // comes from the claim. An Owner of B acting with A's claim would make this
            // check look in A, find nothing and insert a duplicate row in B. The route's
            // tenantId is filtered in the AnyAsync below.
            var exists = await _db.TenantUserRoles
                .IgnoreQueryFilters()
                .AnyAsync(t => t.TenantId == tenantId && t.UserId == userId && t.RoleId == role.Id, ct);
            if (exists) return;

            _db.TenantUserRoles.Add(new TenantUserRole
            {
                TenantId = tenantId,
                UserId = userId,
                RoleId = role.Id,
                GrantedByUserId = grantedByUserId,
                GrantedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }

        public async Task RevokeFeatureRoleAsync(
            Guid tenantId, Guid userId, string roleName, CancellationToken ct = default)
        {
            // Cross-tenant on purpose, same reason as AssignFeatureRoleAsync. Here the
            // consequence would be worse: not finding the row turns the revoke into a
            // silent no-op, and the role stays granted without any error.
            var row = await _db.TenantUserRoles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId
                                       && t.UserId == userId
                                       && t.Role.Name == roleName, ct);
            if (row == null) return;
            _db.TenantUserRoles.Remove(row);
            await _db.SaveChangesAsync(ct);
        }
    }
}

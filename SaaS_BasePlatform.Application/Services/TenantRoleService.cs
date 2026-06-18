using Microsoft.EntityFrameworkCore;
using SaaS_BasePlatform.Domain.Authorization;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Infrastructure.Data;

namespace SaaS_BasePlatform.Application.Services
{
    public class TenantRoleService : ITenantRoleService
    {
        private readonly ApplicationDbContext _db;

        public TenantRoleService(ApplicationDbContext db) => _db = db;

        public async Task<IReadOnlyList<string>> GetTenantRoleNamesAsync(
            Guid userId, Guid tenantId, CancellationToken ct = default)
        {
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
            if (!Permissions.Roles.AssignableFeatureRoles.Contains(roleName))
                throw new InvalidOperationException(
                    $"'{roleName}' is not an assignable tenant feature role.");

            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct)
                ?? throw new InvalidOperationException($"Role '{roleName}' does not exist.");

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

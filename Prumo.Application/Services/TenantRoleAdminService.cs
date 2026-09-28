using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prumo.Application.DTOs.Roles;
using Prumo.Domain.Authorization;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;

namespace Prumo.Application.Services
{
    public class TenantRoleAdminService : ITenantRoleAdminService
    {
        private const int MaxNameLength = 64;

        private readonly ApplicationDbContext _db;
        private readonly RoleManager<ApplicationRole> _roleManager;

        public TenantRoleAdminService(ApplicationDbContext db, RoleManager<ApplicationRole> roleManager)
        {
            _db = db;
            _roleManager = roleManager;
        }

        public async Task<IReadOnlyList<TenantRoleDto>> GetVisibleRolesAsync(
            Guid tenantId, CancellationToken ct = default)
        {
            var roles = await _db.Roles
                .Where(r => r.TenantId == null || r.TenantId == tenantId)
                .OrderBy(r => r.Name)
                .ToListAsync(ct);

            // Cross-tenant: TenantUserRole is ITenantScoped and this service runs on paths
            // with no resolved TenantContext; the Where below is the real filter.
            var counts = await _db.TenantUserRoles
                .IgnoreQueryFilters()
                .Where(tur => tur.TenantId == tenantId)
                .GroupBy(tur => tur.RoleId)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);

            return roles
                .Select(r => new TenantRoleDto(
                    r.Id,
                    r.Name!,
                    r.Description,
                    r.TenantId == null,
                    counts.TryGetValue(r.Id, out var c) ? c : 0))
                .ToList();
        }

        public async Task<IReadOnlyList<string>> GetAssignableRoleNamesAsync(
            Guid tenantId, CancellationToken ct = default)
        {
            var ownNames = await _db.Roles
                .Where(r => r.TenantId == tenantId)
                .Select(r => r.Name!)
                .ToListAsync(ct);

            // Canonical roles come from the Domain list, not the database, because that
            // list leaves out the master admin on purpose — it exists as a role but a
            // tenant admin cannot grant it.
            return Permissions.Roles.AssignableFeatureRoles
                .Concat(ownNames)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public async Task<TenantRoleDto> CreateAsync(
            Guid tenantId, CreateTenantRoleDto dto, CancellationToken ct = default)
        {
            var name = (dto.Name ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The role name is required.");
            }

            if (name.Length > MaxNameLength)
            {
                throw new ArgumentException($"The role name must be at most {MaxNameLength} characters.");
            }

            var normalized = _roleManager.NormalizeKey(name);

            // Canonical names are reserved: letting a tenant create its own "HR" would make
            // the name ambiguous on every screen, in every claim and in every log.
            if (await _db.Roles.AnyAsync(r => r.NormalizedName == normalized && r.TenantId == null, ct))
            {
                throw new InvalidOperationException($"'{name}' is a system role and cannot be recreated.");
            }

            if (await _db.Roles.AnyAsync(r => r.NormalizedName == normalized && r.TenantId == tenantId, ct))
            {
                throw new InvalidOperationException($"A role named '{name}' already exists in this tenant.");
            }

            var role = new ApplicationRole
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                TenantId = tenantId
            };

            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" | ", result.Errors.Select(e => e.Description)));
            }

            // Born empty on purpose: zero ResourcePermission, zero access. The admin
            // grants access afterwards, in the grid.
            return new TenantRoleDto(role.Id, role.Name!, role.Description, false, 0);
        }

        public async Task DeleteAsync(Guid tenantId, Guid roleId, CancellationToken ct = default)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId, ct);

            if (role is null)
            {
                throw new KeyNotFoundException("Role not found.");
            }

            if (role.TenantId is null)
            {
                throw new InvalidOperationException("System roles cannot be deleted.");
            }

            // The gate that stops a tenant from deleting another tenant's role. 404, not
            // 403, on purpose: whoever does not own it should not even learn it exists.
            if (role.TenantId != tenantId)
            {
                throw new KeyNotFoundException("Role not found.");
            }

            // Cross-tenant: TenantUserRole is ITenantScoped and the Where restricts to this tenant.
            var inUse = await _db.TenantUserRoles
                .IgnoreQueryFilters()
                .AnyAsync(tur => tur.RoleId == roleId && tur.TenantId == tenantId, ct);

            if (inUse)
            {
                throw new InvalidOperationException(
                    "This role is still assigned to members. Remove it from them before deleting.");
            }

            // Resource permissions die with it: leaving them orphaned would let a reused Id
            // inherit access nobody granted.
            // Cross-tenant: ResourcePermission is ITenantScoped and the Where restricts here.
            var grants = await _db.ResourcePermissions
                .IgnoreQueryFilters()
                .Where(rp => rp.RoleId == roleId && rp.TenantId == tenantId)
                .ToListAsync(ct);

            if (grants.Count > 0)
            {
                _db.ResourcePermissions.RemoveRange(grants);
                await _db.SaveChangesAsync(ct);
            }

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" | ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}

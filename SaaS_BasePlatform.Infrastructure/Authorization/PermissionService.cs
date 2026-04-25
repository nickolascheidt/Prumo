using SaaS_BasePlatform.Domain.DTOs;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SaaS_BasePlatform.Infrastructure.Authorization
{
    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;

        public PermissionService(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return Array.Empty<string>();

            var roles = await _userManager.GetRolesAsync(user);

            var permissions = new HashSet<string>();

            foreach (var roleName in roles)
            {
                var rolePermissions = await GetRolePermissionsAsync(roleName, cancellationToken);
                foreach (var permission in rolePermissions)
                {
                    permissions.Add(permission);
                }
            }

            return permissions.ToList();
        }

        public async Task<IReadOnlyCollection<string>> GetRolePermissionsAsync(string roleName, CancellationToken cancellationToken = default)
        {
            var permissions = await _context.RolePermissions
                .Include(rp => rp.Permission)
                .Include(rp => rp.Role)
                .Where(rp => rp.Role.Name == roleName)
                .Select(rp => rp.Permission.Name)
                .ToListAsync(cancellationToken);

            return permissions;
        }

        public async Task<IReadOnlyCollection<Permission>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Permissions
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<RolePermissionsDto> GetRolePermissionsDetailedAsync(string roleName, CancellationToken cancellationToken = default)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
                throw new InvalidOperationException($"Role '{roleName}' não encontrada");

            var permissions = await _context.RolePermissions
                .Include(rp => rp.Permission)
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => new PermissionDto
                {
                    Id = rp.Permission.Id,
                    Name = rp.Permission.Name,
                    Description = rp.Permission.Description
                })
                .ToListAsync(cancellationToken);

            return new RolePermissionsDto
            {
                RoleName = roleName,
                Permissions = permissions
            };
        }

        public async Task GrantPermissionToRoleAsync(
            string roleName, 
            string permissionName, 
            Guid performedByUserId, 
            string performedByUserEmail,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
                throw new InvalidOperationException($"Role '{roleName}' não encontrada");

            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Name == permissionName, cancellationToken);

            if (permission == null)
                throw new InvalidOperationException($"Permissão '{permissionName}' não encontrada");

            var exists = await _context.RolePermissions
                .AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id, cancellationToken);

            if (exists)
                throw new InvalidOperationException($"Role '{roleName}' já possui a permissão '{permissionName}'");

            var rolePermission = new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permission.Id,
                GrantedAt = DateTime.UtcNow,
                GrantedByUserId = performedByUserId,
                GrantedByUserEmail = performedByUserEmail
            };

            _context.RolePermissions.Add(rolePermission);

            var auditLog = new PermissionAuditLog
            {
                RoleId = role.Id,
                RoleName = roleName,
                PermissionId = permission.Id,
                PermissionName = permissionName,
                Action = "GRANTED",
                PerformedByUserId = performedByUserId,
                PerformedByUserEmail = performedByUserEmail,
                PerformedAt = DateTime.UtcNow,
                Reason = reason
            };

            _context.PermissionAuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task RevokePermissionFromRoleAsync(
            string roleName, 
            string permissionName, 
            Guid performedByUserId, 
            string performedByUserEmail,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
                throw new InvalidOperationException($"Role '{roleName}' não encontrada");

            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Name == permissionName, cancellationToken);

            if (permission == null)
                throw new InvalidOperationException($"Permissão '{permissionName}' não encontrada");

            var rolePermission = await _context.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id, cancellationToken);

            if (rolePermission == null)
                throw new InvalidOperationException($"Role '{roleName}' não possui a permissão '{permissionName}'");

            _context.RolePermissions.Remove(rolePermission);

            var auditLog = new PermissionAuditLog
            {
                RoleId = role.Id,
                RoleName = roleName,
                PermissionId = permission.Id,
                PermissionName = permissionName,
                Action = "REVOKED",
                PerformedByUserId = performedByUserId,
                PerformedByUserEmail = performedByUserEmail,
                PerformedAt = DateTime.UtcNow,
                Reason = reason
            };

            _context.PermissionAuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyCollection<PermissionAuditDto>> GetAuditLogsAsync(
            string? roleName = null, 
            int take = 100, 
            CancellationToken cancellationToken = default)
        {
            var query = _context.PermissionAuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(roleName))
                query = query.Where(log => log.RoleName == roleName);

            var logs = await query
                .OrderByDescending(log => log.PerformedAt)
                .Take(take)
                .Select(log => new PermissionAuditDto
                {
                    Id = log.Id,
                    RoleName = log.RoleName,
                    PermissionName = log.PermissionName,
                    Action = log.Action,
                    PerformedByUserEmail = log.PerformedByUserEmail,
                    PerformedAt = log.PerformedAt,
                    Reason = log.Reason
                })
                .ToListAsync(cancellationToken);

            return logs;
        }
    }
}

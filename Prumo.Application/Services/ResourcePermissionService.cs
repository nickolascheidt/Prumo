using Prumo.Application.DTOs;
using Prumo.Domain.Authorization;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Prumo.Application.Services
{
    public interface IResourcePermissionService
    {
        // Resource management
        Task<List<ResourceDto>> GetAllResourcesAsync();
        Task<ResourceDto?> GetResourceByIdAsync(Guid id);
        Task<ResourceDto> CreateResourceAsync(CreateResourceDto dto);
        Task<ResourceDto?> UpdateResourceAsync(Guid id, UpdateResourceDto dto);
        Task<bool> DeleteResourceAsync(Guid id);

        // Permission management
        Task<bool> AssignPermissionAsync(
            AssignResourcePermissionDto dto, string? grantedByEmail = null, Guid? performedByUserId = null);

        Task<bool> RemovePermissionAsync(
            Guid roleId, Guid resourceId, string? removedByEmail = null, Guid? performedByUserId = null);
        Task<List<ResourcePermissionDto>> GetRolePermissionsAsync(Guid roleId);

        // Current user's permissions
        Task<UserPermissionsDto?> GetUserPermissionsAsync(Guid userId);
        Task<UserPermissionsDto?> GetUserPermissionsByEmailAsync(string email);
        Task<PermissionLevel> GetUserPermissionForResourceAsync(Guid userId, string resourceCode);
        Task<bool> UserHasAccessAsync(Guid userId, string resourceCode, PermissionLevel minimumLevel = PermissionLevel.Read);
    }

    public class ResourcePermissionService : IResourcePermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITenantContext _tenantContext;

        public ResourcePermissionService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITenantContext tenantContext)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
        }

        #region Resource Management

        public async Task<List<ResourceDto>> GetAllResourcesAsync()
        {
            return await _context.Resources
                .Where(r => r.IsActive)
                .OrderBy(r => r.Module)
                .ThenBy(r => r.DisplayOrder)
                .Select(r => new ResourceDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Name = r.Name,
                    Description = r.Description,
                    Module = r.Module,
                    FrontendRoute = r.FrontendRoute,
                    Icon = r.Icon,
                    DisplayOrder = r.DisplayOrder,
                    UserPermissionLevel = PermissionLevel.None
                })
                .ToListAsync();
        }

        public async Task<ResourceDto?> GetResourceByIdAsync(Guid id)
        {
            var resource = await _context.Resources
                .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);

            if (resource == null) return null;

            return new ResourceDto
            {
                Id = resource.Id,
                Code = resource.Code,
                Name = resource.Name,
                Description = resource.Description,
                Module = resource.Module,
                FrontendRoute = resource.FrontendRoute,
                Icon = resource.Icon,
                DisplayOrder = resource.DisplayOrder,
                UserPermissionLevel = PermissionLevel.None
            };
        }

        public async Task<ResourceDto> CreateResourceAsync(CreateResourceDto dto)
        {
            var existingResource = await _context.Resources
                .FirstOrDefaultAsync(r => r.Code == dto.Code);

            if (existingResource != null)
            {
                throw new InvalidOperationException($"Resource with code '{dto.Code}' already exists.");
            }

            var resource = new Resource
            {
                Code = dto.Code,
                Name = dto.Name,
                Description = dto.Description,
                Module = dto.Module,
                FrontendRoute = dto.FrontendRoute,
                Icon = dto.Icon,
                DisplayOrder = dto.DisplayOrder
            };

            _context.Resources.Add(resource);
            await _context.SaveChangesAsync();

            return new ResourceDto
            {
                Id = resource.Id,
                Code = resource.Code,
                Name = resource.Name,
                Description = resource.Description,
                Module = resource.Module,
                FrontendRoute = resource.FrontendRoute,
                Icon = resource.Icon,
                DisplayOrder = resource.DisplayOrder,
                UserPermissionLevel = PermissionLevel.None
            };
        }

        public async Task<ResourceDto?> UpdateResourceAsync(Guid id, UpdateResourceDto dto)
        {
            var resource = await _context.Resources
                .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);

            if (resource == null) return null;

            resource.Name = dto.Name;
            resource.Description = dto.Description;
            resource.Module = dto.Module;
            resource.FrontendRoute = dto.FrontendRoute;
            resource.Icon = dto.Icon;
            resource.DisplayOrder = dto.DisplayOrder;

            await _context.SaveChangesAsync();

            return new ResourceDto
            {
                Id = resource.Id,
                Code = resource.Code,
                Name = resource.Name,
                Description = resource.Description,
                Module = resource.Module,
                FrontendRoute = resource.FrontendRoute,
                Icon = resource.Icon,
                DisplayOrder = resource.DisplayOrder,
                UserPermissionLevel = PermissionLevel.None
            };
        }

        public async Task<bool> DeleteResourceAsync(Guid id)
        {
            var resource = await _context.Resources
                .FirstOrDefaultAsync(r => r.Id == id);

            if (resource == null) return false;

            resource.IsActive = false;
            await _context.SaveChangesAsync();

            return true;
        }

        #endregion

        #region Permission Management

        public async Task<bool> AssignPermissionAsync(
            AssignResourcePermissionDto dto, string? grantedByEmail = null, Guid? performedByUserId = null)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == dto.RoleId);
            var resource = await _context.Resources.FirstOrDefaultAsync(r => r.Id == dto.ResourceId);

            if (role is null || resource is null)
            {
                return false;
            }

            var existingPermission = await _context.ResourcePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == dto.RoleId && rp.ResourceId == dto.ResourceId);

            var previousLevel = existingPermission?.Level ?? PermissionLevel.None;

            if (existingPermission != null)
            {
                existingPermission.Level = dto.Level;
                existingPermission.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var permission = new ResourcePermission
                {
                    RoleId = dto.RoleId,
                    ResourceId = dto.ResourceId,
                    Level = dto.Level,
                    CreatedByUserEmail = grantedByEmail
                };

                _context.ResourcePermissions.Add(permission);
            }

            WriteAudit(role, resource, previousLevel, dto.Level, grantedByEmail, performedByUserId);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemovePermissionAsync(
            Guid roleId, Guid resourceId, string? removedByEmail = null, Guid? performedByUserId = null)
        {
            var permission = await _context.ResourcePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.ResourceId == resourceId);

            if (permission == null) return false;

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == roleId);
            var resource = await _context.Resources.FirstOrDefaultAsync(r => r.Id == resourceId);

            _context.ResourcePermissions.Remove(permission);

            if (role is not null && resource is not null)
            {
                WriteAudit(role, resource, permission.Level, PermissionLevel.None,
                    removedByEmail, performedByUserId);
            }

            await _context.SaveChangesAsync();

            return true;
        }

        /// <summary>
        /// Records the level change. Role and resource names are <b>copied</b>: the role
        /// may be deleted later, and the history must not turn into a list of GUIDs.
        /// </summary>
        private void WriteAudit(
            ApplicationRole role,
            Resource resource,
            PermissionLevel previousLevel,
            PermissionLevel newLevel,
            string? performedByEmail,
            Guid? performedByUserId)
        {
            if (previousLevel == newLevel) return;

            _context.ResourcePermissionAuditLogs.Add(new ResourcePermissionAuditLog
            {
                // TenantId comes from the resource itself, which is ITenantScoped:
                // SaveChanges would fill it from the TenantContext, and this path does not
                // always have one.
                TenantId = resource.TenantId,
                RoleId = role.Id,
                RoleName = role.Name ?? role.Id.ToString(),
                ResourceId = resource.Id,
                ResourceCode = resource.Code,
                PreviousLevel = previousLevel,
                NewLevel = newLevel,
                PerformedByUserId = performedByUserId ?? Guid.Empty,
                PerformedByUserEmail = performedByEmail ?? "desconhecido",
                PerformedAt = DateTime.UtcNow
            });
        }

        public async Task<List<ResourcePermissionDto>> GetRolePermissionsAsync(Guid roleId)
        {
            return await _context.ResourcePermissions
                .Where(rp => rp.RoleId == roleId)
                .Include(rp => rp.Role)
                .Include(rp => rp.Resource)
                .Select(rp => new ResourcePermissionDto
                {
                    RoleId = rp.RoleId,
                    RoleName = rp.Role.Name ?? "",
                    ResourceId = rp.ResourceId,
                    ResourceCode = rp.Resource.Code,
                    ResourceName = rp.Resource.Name,
                    Level = rp.Level
                })
                .ToListAsync();
        }

        #endregion

        #region User Permissions Query

        public async Task<UserPermissionsDto?> GetUserPermissionsAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return null;

            return await BuildUserPermissionsDto(user);
        }

        public async Task<UserPermissionsDto?> GetUserPermissionsByEmailAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return null;

            return await BuildUserPermissionsDto(user);
        }

        public async Task<PermissionLevel> GetUserPermissionForResourceAsync(Guid userId, string resourceCode)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return PermissionLevel.None;

            var userRoles = await _userManager.GetRolesAsync(user);

            if (userRoles.Contains(Permissions.Roles.MasterAdmin))
                return PermissionLevel.Full;

            if (await IsTenantAdminAsync(userId))
                return PermissionLevel.Full;

            // Fix 1: no ambient tenant — ResourcePermissions filter is disabled, risk of cross-tenant leak.
            if (!_tenantContext.HasTenant)
                return PermissionLevel.None;

            var roleIds = await GetEffectiveRoleIdsAsync(userId, userRoles);
            if (roleIds.Count == 0) return PermissionLevel.None;

            var maxPermission = await _context.ResourcePermissions
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Where(rp => rp.Resource.Code == resourceCode && rp.Resource.IsActive)
                .MaxAsync(rp => (PermissionLevel?)rp.Level);

            return maxPermission ?? PermissionLevel.None;
        }

        public async Task<bool> UserHasAccessAsync(Guid userId, string resourceCode, PermissionLevel minimumLevel = PermissionLevel.Read)
        {
            var userLevel = await GetUserPermissionForResourceAsync(userId, resourceCode);
            return userLevel >= minimumLevel;
        }

        #endregion

        #region Private Helpers

        private async Task<List<Guid>> GetEffectiveRoleIdsAsync(Guid userId, IList<string> userRoles)
        {
            var globalRoleIds = await _context.Roles
                .Where(r => userRoles.Contains(r.Name!))
                .Select(r => r.Id)
                .ToListAsync();

            var tenantRoleIds = _tenantContext.HasTenant
                ? await _context.TenantUserRoles
                    .Where(tur => tur.UserId == userId)
                    .Select(tur => tur.RoleId)
                    .ToListAsync()
                : new List<Guid>();

            return globalRoleIds.Concat(tenantRoleIds).Distinct().ToList();
        }

        private async Task<bool> IsTenantAdminAsync(Guid userId)
        {
            if (!_tenantContext.HasTenant) return false;
            var tenantId = _tenantContext.TenantId!.Value;
            var role = await _context.TenantUsers
                .Where(tu => tu.TenantId == tenantId && tu.UserId == userId)
                .Select(tu => (TenantRole?)tu.Role)
                .FirstOrDefaultAsync();
            return role is TenantRole.Owner or TenantRole.Admin;
        }

        private async Task<UserPermissionsDto> BuildUserPermissionsDto(ApplicationUser user)
        {
            var userRoles = await _userManager.GetRolesAsync(user);

            // No ambient tenant: the Resources query filter is disabled, so every branch
            // below would read across tenants — duplicate resource codes (each tenant is
            // seeded with the same catalog) and a cross-tenant leak. Resource access is
            // only meaningful once a tenant is selected.
            if (!_tenantContext.HasTenant)
            {
                return new UserPermissionsDto
                {
                    UserId = user.Id,
                    Email = user.Email ?? "",
                    FullName = user.FullName,
                    Roles = userRoles.ToList(),
                    AllowedResources = new List<ResourceDto>(),
                    ResourcePermissions = new Dictionary<string, PermissionLevel>()
                };
            }

            if (userRoles.Contains(Permissions.Roles.MasterAdmin))
            {
                var allResources = await _context.Resources
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.Module)
                    .ThenBy(r => r.DisplayOrder)
                    .ToListAsync();

                var adminAllowed = allResources.Select(r => new ResourceDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Name = r.Name,
                    Description = r.Description,
                    Module = r.Module,
                    FrontendRoute = r.FrontendRoute,
                    Icon = r.Icon,
                    DisplayOrder = r.DisplayOrder,
                    UserPermissionLevel = PermissionLevel.Full
                }).ToList();

                return new UserPermissionsDto
                {
                    UserId = user.Id,
                    Email = user.Email ?? "",
                    FullName = user.FullName,
                    Roles = userRoles.ToList(),
                    AllowedResources = adminAllowed,
                    ResourcePermissions = adminAllowed.ToDictionary(r => r.Code, _ => PermissionLevel.Full)
                };
            }

            if (await IsTenantAdminAsync(user.Id))
            {
                var tenantResources = await _context.Resources
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.Module).ThenBy(r => r.DisplayOrder)
                    .ToListAsync();

                var allowed = tenantResources.Select(r => new ResourceDto
                {
                    Id = r.Id, Code = r.Code, Name = r.Name, Description = r.Description,
                    Module = r.Module, FrontendRoute = r.FrontendRoute, Icon = r.Icon,
                    DisplayOrder = r.DisplayOrder, UserPermissionLevel = PermissionLevel.Full
                }).ToList();

                return new UserPermissionsDto
                {
                    UserId = user.Id, Email = user.Email ?? "", FullName = user.FullName,
                    Roles = userRoles.ToList(), AllowedResources = allowed,
                    ResourcePermissions = allowed.ToDictionary(r => r.Code, _ => PermissionLevel.Full)
                };
            }

            var roleIds = await GetEffectiveRoleIdsAsync(user.Id, userRoles);

            // Every permission of the user, aggregated per resource, keeping the highest level
            var userResourcePermissions = await _context.ResourcePermissions
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Include(rp => rp.Resource)
                .Where(rp => rp.Resource.IsActive)
                .GroupBy(rp => new 
                { 
                    rp.Resource.Id,
                    rp.Resource.Code,
                    rp.Resource.Name,
                    rp.Resource.Description,
                    rp.Resource.Module,
                    rp.Resource.FrontendRoute,
                    rp.Resource.Icon,
                    rp.Resource.DisplayOrder
                })
                .Select(g => new 
                {
                    Resource = g.Key,
                    MaxLevel = g.Max(rp => rp.Level)
                })
                .ToListAsync();

            var allowedResources = userResourcePermissions
                .Where(rp => rp.MaxLevel > PermissionLevel.None)
                .Select(rp => new ResourceDto
                {
                    Id = rp.Resource.Id,
                    Code = rp.Resource.Code,
                    Name = rp.Resource.Name,
                    Description = rp.Resource.Description,
                    Module = rp.Resource.Module,
                    FrontendRoute = rp.Resource.FrontendRoute,
                    Icon = rp.Resource.Icon,
                    DisplayOrder = rp.Resource.DisplayOrder,
                    UserPermissionLevel = rp.MaxLevel
                })
                .OrderBy(r => r.Module)
                .ThenBy(r => r.DisplayOrder)
                .ToList();

            var resourcePermissions = userResourcePermissions
                .ToDictionary(rp => rp.Resource.Code, rp => rp.MaxLevel);

            return new UserPermissionsDto
            {
                UserId = user.Id,
                Email = user.Email ?? "",
                FullName = user.FullName,
                Roles = userRoles.ToList(),
                AllowedResources = allowedResources,
                ResourcePermissions = resourcePermissions
            };
        }

        #endregion
    }
}

using BiomePampa.Application.DTOs;
using BiomePampa.Domain.Entities;
using BiomePampa.Domain.Enums;
using BiomePampa.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BiomePampa.Application.Services
{
    public interface IResourcePermissionService
    {
        // Gerenciamento de Resources
        Task<List<ResourceDto>> GetAllResourcesAsync();
        Task<ResourceDto?> GetResourceByIdAsync(Guid id);
        Task<ResourceDto> CreateResourceAsync(CreateResourceDto dto);
        Task<ResourceDto?> UpdateResourceAsync(Guid id, UpdateResourceDto dto);
        Task<bool> DeleteResourceAsync(Guid id);

        // Gerenciamento de Permissões
        Task<bool> AssignPermissionAsync(AssignResourcePermissionDto dto, string? grantedByEmail = null);
        Task<bool> RemovePermissionAsync(Guid roleId, Guid resourceId);
        Task<List<ResourcePermissionDto>> GetRolePermissionsAsync(Guid roleId);

        // Consulta de Permissões do Usuário
        Task<UserPermissionsDto?> GetUserPermissionsAsync(Guid userId);
        Task<UserPermissionsDto?> GetUserPermissionsByEmailAsync(string email);
        Task<PermissionLevel> GetUserPermissionForResourceAsync(Guid userId, string resourceCode);
        Task<bool> UserHasAccessAsync(Guid userId, string resourceCode, PermissionLevel minimumLevel = PermissionLevel.Read);
    }

    public class ResourcePermissionService : IResourcePermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ResourcePermissionService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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

        public async Task<bool> AssignPermissionAsync(AssignResourcePermissionDto dto, string? grantedByEmail = null)
        {
            var roleExists = await _context.Roles.AnyAsync(r => r.Id == dto.RoleId);
            var resourceExists = await _context.Resources.AnyAsync(r => r.Id == dto.ResourceId);

            if (!roleExists || !resourceExists)
            {
                return false;
            }

            var existingPermission = await _context.ResourcePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == dto.RoleId && rp.ResourceId == dto.ResourceId);

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

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemovePermissionAsync(Guid roleId, Guid resourceId)
        {
            var permission = await _context.ResourcePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.ResourceId == resourceId);

            if (permission == null) return false;

            _context.ResourcePermissions.Remove(permission);
            await _context.SaveChangesAsync();

            return true;
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
            if (!userRoles.Any()) return PermissionLevel.None;

            if (userRoles.Contains("Administrador"))
                return PermissionLevel.Full;

            var roleIds = await _context.Roles
                .Where(r => userRoles.Contains(r.Name!))
                .Select(r => r.Id)
                .ToListAsync();

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

        private async Task<UserPermissionsDto> BuildUserPermissionsDto(ApplicationUser user)
        {
            var userRoles = await _userManager.GetRolesAsync(user);

            if (userRoles.Contains("Administrador"))
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

            var roleIds = await _context.Roles
                .Where(r => userRoles.Contains(r.Name!))
                .Select(r => r.Id)
                .ToListAsync();

            // Busca todas as permissões do usuário (agregando por resource, pegando o maior nível)
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

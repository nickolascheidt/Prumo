using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Prumo.Api.Attributes;
using Prumo.Application.DTOs;
using Prumo.Application.DTOs.Roles;
using Prumo.Application.Services;

namespace Prumo.Api.Controllers
{
    /// <summary>
    /// Roles a tenant manages: the system's canonical ones (read-only) and the ones it
    /// created itself.
    /// </summary>
    /// <remarks>
    /// The required level is inferred from the verb by <see cref="TenantModuleAttribute"/>:
    /// GET → Read, POST/PUT → Write, DELETE → Full. Deleting a role requiring the highest
    /// level on <c>Role.Management</c> is intentional. Setting a role's level on a resource
    /// is a PUT, revoking included (level <c>None</c>), so granting and revoking need the
    /// same level.
    /// </remarks>
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/roles")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [TenantModule("Role.Management")]
    public class TenantRolesController : ControllerBase
    {
        private readonly ITenantRoleAdminService _service;
        private readonly IResourcePermissionService _permissions;

        public TenantRolesController(
            ITenantRoleAdminService service, IResourcePermissionService permissions)
        {
            _service = service;
            _permissions = permissions;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TenantRoleDto>>> GetAll(
            Guid tenantId, CancellationToken ct)
            => Ok(await _service.GetVisibleRolesAsync(tenantId, ct));

        [HttpPost]
        public async Task<ActionResult<TenantRoleDto>> Create(
            Guid tenantId, [FromBody] CreateTenantRoleDto dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(tenantId, dto, ct);
            return CreatedAtAction(nameof(GetAll), new { tenantId }, created);
        }

        [HttpDelete("{roleId:guid}")]
        public async Task<IActionResult> Delete(Guid tenantId, Guid roleId, CancellationToken ct)
        {
            await _service.DeleteAsync(tenantId, roleId, ct);
            return NoContent();
        }

        /// <summary>The tenant's resource catalog: the rows of the level grid.</summary>
        [HttpGet("resources")]
        public async Task<ActionResult<List<ResourceDto>>> GetResources(Guid tenantId)
            => Ok(await _permissions.GetTenantResourcesAsync(tenantId));

        /// <summary>The role's level on each resource of this tenant.</summary>
        [HttpGet("{roleId:guid}/permissions")]
        public async Task<ActionResult<List<ResourcePermissionDto>>> GetPermissions(
            Guid tenantId, Guid roleId)
        {
            var permissions = await _permissions.GetTenantRolePermissionsAsync(tenantId, roleId);
            return permissions is null ? NotFound(new { message = "Role not found." }) : Ok(permissions);
        }

        /// <summary>Sets the role's level on a resource of this tenant. <c>None</c> revokes.</summary>
        [HttpPut("{roleId:guid}/permissions/{resourceId:guid}")]
        public async Task<IActionResult> SetPermission(
            Guid tenantId, Guid roleId, Guid resourceId, [FromBody] SetResourceLevelDto dto)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var performedBy = Guid.TryParse(claim, out var id) ? id : Guid.Empty;

            var done = await _permissions.SetTenantRolePermissionAsync(
                tenantId, roleId, resourceId, dto.Level,
                User.FindFirst(ClaimTypes.Email)?.Value, performedBy);

            return done ? NoContent() : NotFound(new { message = "Role or resource not found." });
        }
    }
}

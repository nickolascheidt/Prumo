using Prumo.Application.DTOs;
using Prumo.Application.Services;
using Prumo.Domain.Authorization;
using Prumo.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Prumo.Api.Controllers
{
    /// <summary>
    /// UI resources and fine-grained access control
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ResourcesController : ControllerBase
    {
        private readonly IResourcePermissionService _permissionService;

        public ResourcesController(IResourcePermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        /// <summary>
        /// Gets every resource permission of the current user. This is what the frontend
        /// uses to build the menu and control access to screens.
        /// </summary>
        [HttpGet("my-permissions")]
        [ProducesResponseType(typeof(UserPermissionsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UserPermissionsDto>> GetMyPermissions()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var permissions = await _permissionService.GetUserPermissionsAsync(userId);
            
            if (permissions == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(permissions);
        }

        /// <summary>
        /// Checks whether the current user can access a specific resource
        /// </summary>
        [HttpGet("check-access/{resourceCode}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> CheckAccess(
            string resourceCode, 
            [FromQuery] PermissionLevel minimumLevel = PermissionLevel.Read)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var hasAccess = await _permissionService.UserHasAccessAsync(userId, resourceCode, minimumLevel);
            var userLevel = await _permissionService.GetUserPermissionForResourceAsync(userId, resourceCode);

            return Ok(new 
            { 
                resourceCode,
                hasAccess,
                userLevel = userLevel.ToString(),
                minimumLevel = minimumLevel.ToString(),
                canRead = userLevel >= PermissionLevel.Read,
                canWrite = userLevel >= PermissionLevel.Write,
                hasFull = userLevel >= PermissionLevel.Full
            });
        }

        /// <summary>
        /// Gets a specific user's permissions (master admin only)
        /// </summary>
        [HttpGet("user/{userId}")]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
        [ProducesResponseType(typeof(UserPermissionsDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<UserPermissionsDto>> GetUserPermissions(Guid userId)
        {
            var permissions = await _permissionService.GetUserPermissionsAsync(userId);
            
            if (permissions == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(permissions);
        }

        #region Resource Management (Admin Only)

        /// <summary>
        /// Lists every resource in the system
        /// </summary>
        [HttpGet]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
        [ProducesResponseType(typeof(List<ResourceDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<ResourceDto>>> GetAllResources()
        {
            var resources = await _permissionService.GetAllResourcesAsync();
            return Ok(resources);
        }

        /// <summary>
        /// Gets a specific resource
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
        [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<ResourceDto>> GetResourceById(Guid id)
        {
            var resource = await _permissionService.GetResourceByIdAsync(id);
            
            if (resource == null)
            {
                return NotFound(new { message = "Resource not found" });
            }

            return Ok(resource);
        }

        /// <summary>
        /// Creates a resource
        /// </summary>
        [HttpPost]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
        [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<ResourceDto>> CreateResource([FromBody] CreateResourceDto dto)
        {
            try
            {
                var resource = await _permissionService.CreateResourceAsync(dto);
                return CreatedAtAction(nameof(GetResourceById), new { id = resource.Id }, resource);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Updates an existing resource
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
        [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<ResourceDto>> UpdateResource(Guid id, [FromBody] UpdateResourceDto dto)
        {
            var resource = await _permissionService.UpdateResourceAsync(id, dto);
            
            if (resource == null)
            {
                return NotFound(new { message = "Resource not found" });
            }

            return Ok(resource);
        }

        /// <summary>
        /// Deletes a resource (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteResource(Guid id)
        {
            var success = await _permissionService.DeleteResourceAsync(id);
            
            if (!success)
            {
                return NotFound(new { message = "Resource not found" });
            }

            return NoContent();
        }

        #endregion

        #region Role Permission Management (Admin Only)

        /// <summary>
        /// Assigns or updates a role's permission on a resource
        /// </summary>
        [HttpPost("assign")]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> AssignPermission([FromBody] AssignResourcePermissionDto dto)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var success = await _permissionService.AssignPermissionAsync(dto, email, CurrentUserId());

            if (!success)
            {
                return BadRequest(new { message = "Role or Resource not found" });
            }

            return Ok(new { message = "Permission assigned successfully" });
        }

        /// <summary>
        /// Removes a role's permission on a resource
        /// </summary>
        [HttpDelete("remove")]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> RemovePermission([FromQuery] Guid roleId, [FromQuery] Guid resourceId)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var success = await _permissionService.RemovePermissionAsync(
                roleId, resourceId, email, CurrentUserId());

            if (!success)
            {
                return NotFound(new { message = "Permission not found" });
            }

            return NoContent();
        }

        /// <summary>
        /// Gets every permission of a role
        /// </summary>
        [HttpGet("role/{roleId}")]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
        [ProducesResponseType(typeof(List<ResourcePermissionDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<ResourcePermissionDto>>> GetRolePermissions(Guid roleId)
        {
            var permissions = await _permissionService.GetRolePermissionsAsync(roleId);
            return Ok(permissions);
        }

        #endregion

        /// <summary>
        /// Id of the authenticated user, or <see cref="Guid.Empty"/> if the claim is
        /// missing — auditing must not bring the operation down for not knowing the author.
        /// </summary>
        private Guid CurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }
}

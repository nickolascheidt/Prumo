using Prumo.Application.DTOs;
using Prumo.Application.Services;
using Prumo.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Prumo.Api.Controllers
{
    /// <summary>
    /// Controller para gerenciamento de recursos de UI e controle de acesso granular
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
        /// Obtém todas as permissões de recursos do usuário atual
        /// Este endpoint é fundamental para o frontend construir o menu e controlar acesso às telas
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
        /// Verifica se o usuário atual tem acesso a um recurso específico
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
        /// Obtém permissões de um usuário específico (apenas admins)
        /// </summary>
        [HttpGet("user/{userId}")]
        [Authorize(Roles = "Administrador")]
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
        /// Lista todos os recursos disponíveis no sistema
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Administrador")]
        [ProducesResponseType(typeof(List<ResourceDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<ResourceDto>>> GetAllResources()
        {
            var resources = await _permissionService.GetAllResourcesAsync();
            return Ok(resources);
        }

        /// <summary>
        /// Obtém um recurso específico
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = "Administrador")]
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
        /// Cria um novo recurso
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Administrador")]
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
        /// Atualiza um recurso existente
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador")]
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
        /// Deleta um recurso (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrador")]
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
        /// Atribui ou atualiza permissão de uma role para um recurso
        /// </summary>
        [HttpPost("assign")]
        [Authorize(Roles = "Administrador")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> AssignPermission([FromBody] AssignResourcePermissionDto dto)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var success = await _permissionService.AssignPermissionAsync(dto, email);
            
            if (!success)
            {
                return BadRequest(new { message = "Role or Resource not found" });
            }

            return Ok(new { message = "Permission assigned successfully" });
        }

        /// <summary>
        /// Remove permissão de uma role para um recurso
        /// </summary>
        [HttpDelete("remove")]
        [Authorize(Roles = "Administrador")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> RemovePermission([FromQuery] Guid roleId, [FromQuery] Guid resourceId)
        {
            var success = await _permissionService.RemovePermissionAsync(roleId, resourceId);
            
            if (!success)
            {
                return NotFound(new { message = "Permission not found" });
            }

            return NoContent();
        }

        /// <summary>
        /// Obtém todas as permissões de uma role
        /// </summary>
        [HttpGet("role/{roleId}")]
        [Authorize(Roles = "Administrador")]
        [ProducesResponseType(typeof(List<ResourcePermissionDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<ResourcePermissionDto>>> GetRolePermissions(Guid roleId)
        {
            var permissions = await _permissionService.GetRolePermissionsAsync(roleId);
            return Ok(permissions);
        }

        #endregion
    }
}

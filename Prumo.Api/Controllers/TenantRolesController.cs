using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prumo.Api.Attributes;
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
    /// GET → Read, POST → Write, DELETE → Full. Deleting a role requiring the highest level
    /// on <c>Role.Management</c> is intentional.
    /// </remarks>
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/roles")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [TenantModule("Role.Management")]
    public class TenantRolesController : ControllerBase
    {
        private readonly ITenantRoleAdminService _service;

        public TenantRolesController(ITenantRoleAdminService service) => _service = service;

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
    }
}

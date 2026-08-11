using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prumo.Application.DTOs.HR;
using Prumo.Application.Services;
using Prumo.Domain.Enums;
using System.Security.Claims;

namespace Prumo.Api.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/employees")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class EmployeesController : ControllerBase
    {
        private readonly IEmployeeService _service;
        private readonly ITenantService _tenantService;

        public EmployeesController(IEmployeeService service, ITenantService tenantService)
        {
            _service       = service;
            _tenantService = tenantService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private async Task<TenantRole?> GetRoleAsync(Guid tenantId, CancellationToken ct) =>
            await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);

        private static bool CanAccess(TenantRole? role) => role.HasValue;
        private static bool CanManage(TenantRole? role) => role is TenantRole.Owner or TenantRole.Admin;

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<EmployeeDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> List(
            Guid tenantId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.ListAsync(tenantId, includeInactive, ct));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EmployeeDto>> GetById(
            Guid tenantId, Guid id, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            var employee = await _service.GetAsync(tenantId, id, ct);
            return employee == null ? NotFound() : Ok(employee);
        }

        [HttpPost]
        [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<EmployeeDto>> Create(
            Guid tenantId, [FromBody] CreateEmployeeRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            var employee = await _service.CreateAsync(tenantId, request, ct);
            return CreatedAtAction(nameof(GetById), new { tenantId, id = employee.Id }, employee);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EmployeeDto>> Update(
            Guid tenantId, Guid id, [FromBody] UpdateEmployeeRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.UpdateAsync(tenantId, id, request, ct));
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Deactivate(
            Guid tenantId, Guid id, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            await _service.DeactivateAsync(tenantId, id, ct);
            return NoContent();
        }
    }
}

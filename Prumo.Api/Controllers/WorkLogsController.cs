using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prumo.Api.Attributes;
using Prumo.Application.DTOs.HR;
using Prumo.Application.Services;
using Prumo.Domain.Enums;
using System.Security.Claims;

namespace Prumo.Api.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/employees/{employeeId:guid}/worklogs")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [TenantModule("HR.WorkLogs")]
    public class WorkLogsController : ControllerBase
    {
        private readonly IWorkLogService _service;
        private readonly ITenantService _tenantService;

        public WorkLogsController(IWorkLogService service, ITenantService tenantService)
        {
            _service       = service;
            _tenantService = tenantService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private async Task<TenantRole?> GetRoleAsync(Guid tenantId, CancellationToken ct) =>
            await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);

        private static bool CanManage(TenantRole? role) => role is TenantRole.Owner or TenantRole.Admin;

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<WorkLogDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<WorkLogDto>>> List(
            Guid tenantId, Guid employeeId, [FromQuery] WorkLogQueryDto query, CancellationToken ct)
        {
            return Ok(await _service.ListByEmployeeAsync(tenantId, employeeId, query, ct));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(WorkLogDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<WorkLogDto>> GetById(
            Guid tenantId, Guid employeeId, Guid id, CancellationToken ct)
        {
            var log = await _service.GetByIdAsync(tenantId, id, ct);
            return log == null ? NotFound() : Ok(log);
        }

        [HttpPost]
        [ProducesResponseType(typeof(WorkLogDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<WorkLogDto>> Create(
            Guid tenantId, Guid employeeId,
            [FromBody] CreateWorkLogRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            var actualRequest = request with { EmployeeId = employeeId };
            var log = await _service.CreateAsync(tenantId, actualRequest, ct);
            return CreatedAtAction(nameof(GetById), new { tenantId, employeeId, id = log.Id }, log);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(WorkLogDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<WorkLogDto>> Update(
            Guid tenantId, Guid employeeId, Guid id,
            [FromBody] UpdateWorkLogRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.UpdateAsync(tenantId, id, request, ct));
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            Guid tenantId, Guid employeeId, Guid id, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            await _service.DeleteAsync(tenantId, id, ct);
            return NoContent();
        }
    }
}

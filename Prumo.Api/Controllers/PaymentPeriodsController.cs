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
    [Route("api/tenants/{tenantId:guid}/employees/{employeeId:guid}/payment-periods")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class PaymentPeriodsController : ControllerBase
    {
        private readonly IPaymentPeriodService _service;
        private readonly ITenantService _tenantService;

        public PaymentPeriodsController(IPaymentPeriodService service, ITenantService tenantService)
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

        [HttpGet("/api/tenants/{tenantId:guid}/payment-periods")]
        [ProducesResponseType(typeof(IReadOnlyList<PaymentPeriodSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<PaymentPeriodSummaryDto>>> ListAll(
            Guid tenantId, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.ListAllByTenantAsync(tenantId, ct));
        }

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<PaymentPeriodSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<PaymentPeriodSummaryDto>>> List(
            Guid tenantId, Guid employeeId, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.ListByEmployeeAsync(tenantId, employeeId, ct));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(PaymentPeriodDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentPeriodDto>> GetById(
            Guid tenantId, Guid employeeId, Guid id, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            var period = await _service.GetByIdAsync(tenantId, id, ct);
            return period == null ? NotFound() : Ok(period);
        }

        [HttpPost("generate")]
        [ProducesResponseType(typeof(PaymentPeriodDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<PaymentPeriodDto>> Generate(
            Guid tenantId, Guid employeeId,
            [FromBody] GeneratePaymentPeriodRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            var actualRequest = request with { EmployeeId = employeeId };
            var period = await _service.GenerateAsync(tenantId, actualRequest, ct);
            return CreatedAtAction(nameof(GetById), new { tenantId, employeeId, id = period.Id }, period);
        }

        [HttpPatch("{id:guid}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStatus(
            Guid tenantId, Guid employeeId, Guid id,
            [FromBody] UpdatePaymentPeriodStatusDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            if (!Enum.IsDefined(typeof(PaymentStatus), request.Status))
                return BadRequest($"Invalid status: {request.Status}.");
            await _service.UpdateStatusAsync(tenantId, id, (PaymentStatus)request.Status, ct);
            return NoContent();
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

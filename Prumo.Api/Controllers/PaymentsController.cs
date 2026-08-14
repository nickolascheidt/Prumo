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
    [Route("api/tenants/{tenantId:guid}/payments")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [TenantModule("HR.Payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _service;
        private readonly ITenantService _tenantService;

        public PaymentsController(IPaymentService service, ITenantService tenantService)
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

        [HttpGet("recent")]
        [ProducesResponseType(typeof(IReadOnlyList<PaymentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<PaymentDto>>> ListRecent(
            Guid tenantId, [FromQuery] int count = 20, CancellationToken ct = default)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.ListRecentAsync(tenantId, count, ct));
        }

        [HttpGet("employee/{employeeId:guid}")]
        [ProducesResponseType(typeof(IReadOnlyList<PaymentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<PaymentDto>>> ListByEmployee(
            Guid tenantId, Guid employeeId, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.ListByEmployeeAsync(tenantId, employeeId, ct));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentDto>> GetById(
            Guid tenantId, Guid id, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            var payment = await _service.GetByIdAsync(tenantId, id, ct);
            return payment == null ? NotFound() : Ok(payment);
        }

        [HttpPost]
        [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<PaymentDto>> Create(
            Guid tenantId, [FromBody] CreatePaymentRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            var payment = await _service.CreateAsync(tenantId, CurrentUserId, request, ct);
            return CreatedAtAction(nameof(GetById), new { tenantId, id = payment.Id }, payment);
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            Guid tenantId, Guid id, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            await _service.DeleteAsync(tenantId, id, ct);
            return NoContent();
        }
    }
}

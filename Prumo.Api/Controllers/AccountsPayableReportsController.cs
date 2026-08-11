using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prumo.Application.DTOs.AccountsPayable;
using Prumo.Application.Services;
using Prumo.Domain.Enums;
using System.Security.Claims;

namespace Prumo.Api.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/accounts-payable")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AccountsPayableReportsController : ControllerBase
    {
        private readonly IAccountsPayableService _service;
        private readonly ITenantService _tenantService;

        public AccountsPayableReportsController(
            IAccountsPayableService service,
            ITenantService tenantService)
        {
            _service = service;
            _tenantService = tenantService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private async Task<TenantRole?> GetRoleAsync(Guid tenantId, CancellationToken ct) =>
            await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);

        [HttpGet("summary")]
        [ProducesResponseType(typeof(SummaryResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<SummaryResponseDto>> Summary(
            Guid tenantId,
            [FromQuery] SummaryQueryDto query,
            CancellationToken ct)
        {
            if (await GetRoleAsync(tenantId, ct) == null) return Forbid();

            var summary = await _service.GetSummaryAsync(tenantId, query, ct);
            return Ok(summary);
        }

        [HttpGet("export/csv")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ExportCsv(
            Guid tenantId,
            [FromQuery] EntryListQueryDto query,
            CancellationToken ct)
        {
            if (await GetRoleAsync(tenantId, ct) == null) return Forbid();

            var bytes = await _service.ExportEntriesCsvAsync(tenantId, query, ct);
            var fileName = $"accounts-payable-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }
    }
}

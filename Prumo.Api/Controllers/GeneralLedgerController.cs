using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prumo.Api.Attributes;
using Prumo.Application.DTOs.Finance;
using Prumo.Application.Services;
using Prumo.Domain.Common;
using Prumo.Domain.Enums;
using System.Security.Claims;

namespace Prumo.Api.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/general-ledger")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [TenantModule("GeneralLedger.Management")]
    public class GeneralLedgerController : ControllerBase
    {
        private readonly IJournalService _journalService;
        private readonly ITenantGlSettingsService _settingsService;
        private readonly ITenantService _tenantService;

        public GeneralLedgerController(
            IJournalService journalService,
            ITenantGlSettingsService settingsService,
            ITenantService tenantService)
        {
            _journalService  = journalService;
            _settingsService = settingsService;
            _tenantService   = tenantService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private async Task<TenantRole?> GetRoleAsync(Guid tenantId, CancellationToken ct) =>
            await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);

        private static bool CanManage(TenantRole? role) => role is TenantRole.Owner or TenantRole.Admin;

        [HttpGet("entries")]
        [ProducesResponseType(typeof(PagedResult<JournalEntryListItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<PagedResult<JournalEntryListItemDto>>> ListEntries(
            Guid tenantId, [FromQuery] JournalEntryQueryDto query, CancellationToken ct)
        {
            return Ok(await _journalService.ListEntriesAsync(tenantId, query, ct));
        }

        [HttpGet("entries/{id:guid}")]
        [ProducesResponseType(typeof(JournalEntryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<JournalEntryDto>> GetEntry(
            Guid tenantId, Guid id, CancellationToken ct)
        {
            var entry = await _journalService.GetEntryAsync(tenantId, id, ct);
            return entry == null ? NotFound() : Ok(entry);
        }

        [HttpPost("entries")]
        [ProducesResponseType(typeof(JournalEntryDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<JournalEntryDto>> CreateEntry(
            Guid tenantId, [FromBody] CreateJournalEntryRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            var entry = await _journalService.CreateJournalEntryAsync(tenantId, CurrentUserId, request, ct: ct);
            return CreatedAtAction(nameof(GetEntry), new { tenantId, id = entry.Id }, entry);
        }

        [HttpGet("accounts/{accountId:guid}/statement")]
        [ProducesResponseType(typeof(AccountStatementDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AccountStatementDto>> GetStatement(
            Guid tenantId, Guid accountId,
            [FromQuery] DateTime? from, [FromQuery] DateTime? to,
            CancellationToken ct)
        {
            return Ok(await _journalService.GetAccountStatementAsync(tenantId, accountId, from, to, ct));
        }

        [HttpGet("settings")]
        [ProducesResponseType(typeof(TenantGlSettingsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<TenantGlSettingsDto>> GetSettings(
            Guid tenantId, CancellationToken ct)
        {
            return Ok(await _settingsService.GetSettingsAsync(tenantId, ct));
        }

        [HttpPut("settings")]
        [ProducesResponseType(typeof(TenantGlSettingsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<TenantGlSettingsDto>> UpdateSettings(
            Guid tenantId, [FromBody] UpdateTenantGlSettingsDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _settingsService.UpdateSettingsAsync(tenantId, request, ct));
        }
    }
}

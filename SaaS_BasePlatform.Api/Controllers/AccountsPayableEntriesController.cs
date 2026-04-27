using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS_BasePlatform.Application.DTOs.AccountsPayable;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Common;
using SaaS_BasePlatform.Domain.Enums;
using System.Security.Claims;

namespace SaaS_BasePlatform.Api.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/accounts-payable/entries")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AccountsPayableEntriesController : ControllerBase
    {
        private readonly IAccountsPayableService _service;
        private readonly ITenantService _tenantService;

        public AccountsPayableEntriesController(
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

        private static bool CanAccess(TenantRole? role) => role.HasValue;

        private static bool CanManageCategories(TenantRole? role) =>
            role is TenantRole.Owner or TenantRole.Admin;

        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<EntryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<PagedResult<EntryDto>>> List(
            Guid tenantId,
            [FromQuery] EntryListQueryDto query,
            CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();

            var result = await _service.ListEntriesAsync(tenantId, query, ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(EntryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EntryDto>> GetById(Guid tenantId, Guid id, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();

            var entry = await _service.GetEntryAsync(tenantId, id, ct);
            return entry == null ? NotFound() : Ok(entry);
        }

        [HttpPost]
        [ProducesResponseType(typeof(EntryDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<EntryDto>> Create(
            Guid tenantId,
            [FromBody] CreateEntryRequestDto request,
            CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();

            var entry = await _service.CreateEntryAsync(tenantId, CurrentUserId, request, ct);
            return CreatedAtAction(nameof(GetById), new { tenantId, id = entry.Id }, entry);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(EntryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EntryDto>> Update(
            Guid tenantId,
            Guid id,
            [FromBody] UpdateEntryRequestDto request,
            CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();

            var entry = await _service.UpdateEntryAsync(tenantId, id, request, ct);
            return Ok(entry);
        }

        [HttpPost("{id:guid}/mark-paid")]
        [ProducesResponseType(typeof(EntryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EntryDto>> MarkPaid(
            Guid tenantId,
            Guid id,
            [FromBody] MarkPaidRequestDto request,
            CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();

            var entry = await _service.MarkEntryPaidAsync(tenantId, id, request, ct);
            return Ok(entry);
        }

        [HttpPost("{id:guid}/cancel")]
        [ProducesResponseType(typeof(EntryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EntryDto>> Cancel(
            Guid tenantId,
            Guid id,
            [FromBody] CancelEntryRequestDto request,
            CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();

            var entry = await _service.CancelEntryAsync(tenantId, id, request, ct);
            return Ok(entry);
        }

        [HttpPost("bulk")]
        [ProducesResponseType(typeof(BulkEntriesResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<BulkEntriesResponseDto>> Bulk(
            Guid tenantId,
            [FromBody] BulkEntriesRequestDto request,
            CancellationToken ct)
        {
            var role = await GetRoleAsync(tenantId, ct);
            if (!CanAccess(role)) return Forbid();

            var response = await _service.BulkCreateAsync(
                tenantId,
                CurrentUserId,
                request,
                CanManageCategories(role),
                ct);
            return Ok(response);
        }
    }
}

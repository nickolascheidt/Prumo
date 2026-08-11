using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prumo.Application.DTOs.Finance;
using Prumo.Application.Services;
using Prumo.Domain.Enums;
using System.Security.Claims;

namespace Prumo.Api.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/chart-of-accounts")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ChartOfAccountsController : ControllerBase
    {
        private readonly IAccountService _service;
        private readonly ITenantService _tenantService;

        public ChartOfAccountsController(IAccountService service, ITenantService tenantService)
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
        [ProducesResponseType(typeof(IReadOnlyList<AccountDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<AccountDto>>> List(
            Guid tenantId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.ListAccountsAsync(tenantId, includeInactive, ct));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AccountDto>> GetById(
            Guid tenantId, Guid id, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            var account = await _service.GetAccountAsync(tenantId, id, ct);
            return account == null ? NotFound() : Ok(account);
        }

        [HttpPost]
        [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<AccountDto>> Create(
            Guid tenantId, [FromBody] CreateAccountRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            var account = await _service.CreateAccountAsync(tenantId, request, ct);
            return CreatedAtAction(nameof(GetById), new { tenantId, id = account.Id }, account);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AccountDto>> Update(
            Guid tenantId, Guid id, [FromBody] UpdateAccountRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.UpdateAccountAsync(tenantId, id, request, ct));
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Deactivate(
            Guid tenantId, Guid id, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            await _service.DeactivateAccountAsync(tenantId, id, ct);
            return NoContent();
        }
    }
}

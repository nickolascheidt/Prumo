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
    [Route("api/tenants/{tenantId:guid}/accounts-payable/categories")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AccountsPayableCategoriesController : ControllerBase
    {
        private readonly IAccountsPayableService _service;
        private readonly ITenantService _tenantService;

        public AccountsPayableCategoriesController(
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

        private static bool CanManage(TenantRole? role) =>
            role is TenantRole.Owner or TenantRole.Admin;

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List(
            Guid tenantId,
            [FromQuery] bool includeInactive,
            CancellationToken ct)
        {
            var role = await GetRoleAsync(tenantId, ct);
            if (role == null) return Forbid();

            var categories = await _service.ListCategoriesAsync(tenantId, includeInactive, ct);
            return Ok(categories);
        }

        [HttpPost]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<CategoryDto>> Create(
            Guid tenantId,
            [FromBody] CreateCategoryRequestDto request,
            CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();

            var category = await _service.CreateCategoryAsync(tenantId, request, ct);
            return CreatedAtAction(nameof(List), new { tenantId }, category);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CategoryDto>> Update(
            Guid tenantId,
            Guid id,
            [FromBody] UpdateCategoryRequestDto request,
            CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();

            var category = await _service.UpdateCategoryAsync(tenantId, id, request, ct);
            return Ok(category);
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid tenantId, Guid id, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();

            await _service.DeactivateCategoryAsync(tenantId, id, ct);
            return NoContent();
        }
    }
}

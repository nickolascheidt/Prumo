using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS_BasePlatform.Application.DTOs.ApiKeys;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Enums;
using System.Security.Claims;

namespace SaaS_BasePlatform.Api.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/api-keys")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ApiKeysController : ControllerBase
    {
        private readonly IApiKeyService _apiKeyService;
        private readonly ITenantService _tenantService;

        public ApiKeysController(IApiKeyService apiKeyService, ITenantService tenantService)
        {
            _apiKeyService = apiKeyService;
            _tenantService = tenantService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private async Task<bool> CanManageAsync(Guid tenantId, CancellationToken ct)
        {
            var role = await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);
            return role is TenantRole.Owner or TenantRole.Admin;
        }

        [HttpPost]
        [ProducesResponseType(typeof(CreateApiKeyResponseDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<CreateApiKeyResponseDto>> Create(Guid tenantId, [FromBody] CreateApiKeyRequestDto request, CancellationToken ct)
        {
            if (!await CanManageAsync(tenantId, ct)) return Forbid();

            var key = await _apiKeyService.CreateAsync(tenantId, CurrentUserId, request, ct);
            return CreatedAtAction(nameof(List), new { tenantId }, key);
        }

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<ApiKeyDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<ApiKeyDto>>> List(Guid tenantId, CancellationToken ct)
        {
            if (!await CanManageAsync(tenantId, ct)) return Forbid();

            var keys = await _apiKeyService.ListAsync(tenantId, ct);
            return Ok(keys);
        }

        [HttpDelete("{apiKeyId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Revoke(Guid tenantId, Guid apiKeyId, CancellationToken ct)
        {
            if (!await CanManageAsync(tenantId, ct)) return Forbid();

            await _apiKeyService.RevokeAsync(tenantId, apiKeyId, ct);
            return NoContent();
        }
    }
}

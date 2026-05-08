using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS_BasePlatform.Application.DTOs.Auth;
using SaaS_BasePlatform.Application.DTOs.Tenants;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Enums;
using System.Security.Claims;

namespace SaaS_BasePlatform.Api.Controllers
{
    [ApiController]
    [Route("api/tenants")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class TenantsController : ControllerBase
    {
        private readonly ITenantService _tenantService;
        private readonly IAuthService _authService;

        public TenantsController(ITenantService tenantService, IAuthService authService)
        {
            _tenantService = tenantService;
            _authService = authService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        [HttpPost]
        [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<TenantDto>> Create([FromBody] CreateTenantRequestDto request, CancellationToken ct)
        {
            var tenant = await _tenantService.CreateAsync(CurrentUserId, request, ct);
            return CreatedAtAction(nameof(GetById), new { tenantId = tenant.Id }, tenant);
        }

        [HttpGet("me")]
        [ProducesResponseType(typeof(IReadOnlyList<TenantMembershipDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<TenantMembershipDto>>> GetMyMemberships(CancellationToken ct)
        {
            var memberships = await _tenantService.GetUserMembershipsAsync(CurrentUserId, ct);
            return Ok(memberships);
        }

        [HttpGet("{tenantId:guid}")]
        [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TenantDto>> GetById(Guid tenantId, CancellationToken ct)
        {
            if (!await _tenantService.IsMemberAsync(tenantId, CurrentUserId, ct))
                return Forbid();

            var tenant = await _tenantService.GetByIdAsync(tenantId, ct);
            return tenant == null ? NotFound() : Ok(tenant);
        }

        [HttpPost("select")]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<LoginResponseDto>> Select([FromBody] SelectTenantRequestDto request, CancellationToken ct)
        {
            var response = await _authService.SelectTenantAsync(CurrentUserId, request.TenantId, ct);
            return Ok(response);
        }

        [HttpGet("{tenantId:guid}/members")]
        [ProducesResponseType(typeof(IReadOnlyList<TenantMemberDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<TenantMemberDto>>> GetMembers(Guid tenantId, CancellationToken ct)
        {
            var role = await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);
            if (role == null) return Forbid();

            var members = await _tenantService.GetMembersAsync(tenantId, ct);
            return Ok(members);
        }

        [HttpPost("{tenantId:guid}/members")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> AddMember(Guid tenantId, [FromBody] AddTenantMemberRequestDto request, CancellationToken ct)
        {
            var role = await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);
            if (role is not (TenantRole.Owner or TenantRole.Admin)) return Forbid();

            await _tenantService.AddMemberAsync(tenantId, request.UserId, request.Role, ct);
            return NoContent();
        }

        [HttpDelete("{tenantId:guid}/members/{userId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> RemoveMember(Guid tenantId, Guid userId, CancellationToken ct)
        {
            var role = await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);
            if (role is not (TenantRole.Owner or TenantRole.Admin)) return Forbid();

            await _tenantService.RemoveMemberAsync(tenantId, userId, ct);
            return NoContent();
        }

        [HttpGet("users/lookup")]
        [ProducesResponseType(typeof(UserLookupDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserLookupDto>> LookupUser(
            [FromQuery] string email, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("email query parameter is required.");

            var user = await _tenantService.LookupUserByEmailAsync(email, ct);
            return user == null ? NotFound() : Ok(user);
        }

        [HttpPut("{tenantId:guid}/members/{userId:guid}/role")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateMemberRole(
            Guid tenantId, Guid userId, [FromBody] UpdateMemberRoleDto request, CancellationToken ct)
        {
            var callerRole = await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);
            if (callerRole is not (TenantRole.Owner or TenantRole.Admin)) return Forbid();

            await _tenantService.UpdateMemberRoleAsync(tenantId, userId, request.Role, ct);
            return NoContent();
        }
    }
}

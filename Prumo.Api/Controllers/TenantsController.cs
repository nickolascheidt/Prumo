using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prumo.Application.DTOs.Auth;
using Prumo.Application.DTOs.Tenants;
using Prumo.Application.Services;
using Prumo.Domain.Enums;
using System.Security.Claims;

namespace Prumo.Api.Controllers
{
    [ApiController]
    [Route("api/tenants")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class TenantsController : ControllerBase
    {
        private readonly ITenantService _tenantService;
        private readonly IAuthService _authService;
        private readonly ITenantRoleService _tenantRoleService;

        public TenantsController(ITenantService tenantService, IAuthService authService, ITenantRoleService tenantRoleService)
        {
            _tenantService = tenantService;
            _authService = authService;
            _tenantRoleService = tenantRoleService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        [HttpPost]
        [Authorize(Roles = "Administrador")]
        [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<TenantDto>> Create([FromBody] CreateTenantRequestDto request, CancellationToken ct)
        {
            var tenant = await _tenantService.CreateAsync(CurrentUserId, request, ct);
            return CreatedAtAction(nameof(GetById), new { tenantId = tenant.Id }, tenant);
        }

        /// <summary>
        /// Insere o master admin como membro de um tenant que ele não criou, para suporte.
        /// Não existe bypass da checagem de associação: um bypass reintroduziria o vazamento
        /// cross-tenant que o trabalho de RBAC removeu, e seria um caminho que o teste de
        /// arquitetura não consegue ver. Aqui o acesso vira uma linha no banco, auditada.
        /// </summary>
        [HttpPost("{tenantId:guid}/support-access")]
        [Authorize(Roles = "Administrador")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GrantSupportAccess(Guid tenantId, CancellationToken ct)
        {
            var granted = await _tenantService.GrantSupportAccessAsync(tenantId, CurrentUserId, ct);
            return granted ? NoContent() : NotFound();
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

        [HttpPost("{tenantId:guid}/users")]
        [ProducesResponseType(typeof(TenantMemberDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<TenantMemberDto>> CreateUser(
            Guid tenantId, [FromBody] CreateTenantUserDto request, CancellationToken ct)
        {
            var role = await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);
            if (role is not (TenantRole.Owner or TenantRole.Admin)) return Forbid();

            var member = await _tenantService.CreateAndAddMemberAsync(tenantId, request, ct);
            return StatusCode(StatusCodes.Status201Created, member);
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

        [HttpGet("{tenantId:guid}/assignable-roles")]
        [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
        public ActionResult<IReadOnlyList<string>> GetAssignableRoles(Guid tenantId)
            => Ok(Prumo.Domain.Authorization.Permissions.Roles.AssignableFeatureRoles);

        [HttpGet("{tenantId:guid}/members/{userId:guid}/roles")]
        [ProducesResponseType(typeof(TenantMemberRolesDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<TenantMemberRolesDto>> GetMemberRoles(
            Guid tenantId, Guid userId, CancellationToken ct)
        {
            if (!await _tenantService.IsMemberAsync(tenantId, CurrentUserId, ct)) return Forbid();
            var roles = await _tenantRoleService.GetTenantRoleNamesAsync(userId, tenantId, ct);
            return Ok(new TenantMemberRolesDto(userId, roles));
        }

        [HttpPost("{tenantId:guid}/members/{userId:guid}/roles")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AssignMemberRole(
            Guid tenantId, Guid userId, [FromBody] AssignFeatureRoleDto request, CancellationToken ct)
        {
            var callerRole = await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);
            if (callerRole is not (TenantRole.Owner or TenantRole.Admin)) return Forbid();
            if (!await _tenantService.IsMemberAsync(tenantId, userId, ct))
                return BadRequest(new { message = "User is not a member of this tenant." });

            await _tenantRoleService.AssignFeatureRoleAsync(tenantId, userId, request.RoleName, CurrentUserId, ct);
            return NoContent();
        }

        [HttpDelete("{tenantId:guid}/members/{userId:guid}/roles/{roleName}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RevokeMemberRole(
            Guid tenantId, Guid userId, string roleName, CancellationToken ct)
        {
            var callerRole = await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);
            if (callerRole is not (TenantRole.Owner or TenantRole.Admin)) return Forbid();

            await _tenantRoleService.RevokeFeatureRoleAsync(tenantId, userId, roleName, ct);
            return NoContent();
        }
    }
}

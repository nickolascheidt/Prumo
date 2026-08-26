using Prumo.Application.DTOs.Auth;
using Prumo.Application.Services;
using Prumo.Domain.Authorization;
using Prumo.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Prumo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Login de usuário
        /// </summary>
        [HttpPost("login")]
        [EnableRateLimiting("public")]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
        {
            var response = await _authService.LoginAsync(request, cancellationToken);
            return Ok(response);
        }

        /// <summary>
        /// Registrar novo usuário (sem role global; roles de feature são atribuídas por tenant)
        /// </summary>
        [HttpPost("register")]
        [EnableRateLimiting("public")]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<LoginResponseDto>> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
        {
            var response = await _authService.RegisterAsync(request, null, cancellationToken);
            return CreatedAtAction(nameof(GetCurrentUser), new { }, response);
        }

        /// <summary>
        /// Registrar novo administrador (requer autenticação como Admin)
        /// </summary>
        [HttpPost("register/admin")]
        [Authorize(Roles = "Administrador")]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<LoginResponseDto>> RegisterAdmin([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
        {
            var response = await _authService.RegisterAsync(request, "Administrador", cancellationToken);
            return CreatedAtAction(nameof(GetCurrentUser), new { }, response);
        }

        /// <summary>
        /// Obter dados do usuário atual
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        [EnableRateLimiting("authenticated")]
        [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<CurrentUserDto>> GetCurrentUser(CancellationToken cancellationToken)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            // Roles and permissions are tenant-scoped once a tenant has been selected,
            // so /me must resolve them against the same tenant the JWT was issued for.
            Guid? tenantId = Guid.TryParse(User.FindFirst("tenant_id")?.Value, out var tid)
                ? tid
                : null;

            var user = await _authService.GetUserByIdAsync(userId, tenantId, cancellationToken);
            if (user == null)
                return NotFound();

            // As permission strings saíram no item 3B: o catálogo que as produzia não
            // gateava nada. O que o SPA usa para montar menu e telas é
            // GET /api/resources/my-permissions, que devolve nível por recurso.
            var permissions = Array.Empty<string>();

            return Ok(new CurrentUserDto(
                user.Id,
                user.Email,
                user.FullName,
                user.Roles,
                permissions.OrderBy(p => p).ToList(),
                user.CreatedAt,
                user.LastLoginAt
            ));
        }

        /// <summary>
        /// Listar todos os usuários (requer autenticação como Admin)
        /// </summary>
        [HttpGet("users")]
        [Authorize(Roles = "Administrador")]
        [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers(CancellationToken cancellationToken)
        {
            var users = await _authService.GetAllUsersAsync(cancellationToken);
            return Ok(users);
        }

        [HttpGet("users/lookup")]
        [Authorize]
        [ProducesResponseType(typeof(UserLookupDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserLookupDto>> LookupUserByEmail(
            [FromQuery] string email, CancellationToken cancellationToken)
        {
            var result = await _authService.LookupUserByEmailAsync(email, cancellationToken);
            return result == null ? NotFound() : Ok(result);
        }

        /// <summary>
        /// Alterar senha
        /// </summary>
        [HttpPost("change-password")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto request, CancellationToken cancellationToken)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var success = await _authService.ChangePasswordAsync(userId, request, cancellationToken);
            if (!success)
                return BadRequest(new { message = "Não foi possível alterar a senha. Verifique a senha atual." });

            return Ok(new { message = "Senha alterada com sucesso" });
        }

        /// <summary>
        /// Atribuir role global a um usuário (requer autenticação como Admin).
        /// Apenas a role master 'Administrador' pode ser atribuída globalmente;
        /// roles de funcionalidade são atribuídas por tenant.
        /// </summary>
        [HttpPost("users/{userId}/roles")]
        [Authorize(Roles = "Administrador")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AssignRoleToUser(
            Guid userId,
            [FromBody] AssignRoleDto request,
            CancellationToken cancellationToken)
        {
            await _authService.AssignRoleToUserAsync(userId, request.RoleName, cancellationToken);
            return Ok(new { message = $"Role '{request.RoleName}' atribuída ao usuário com sucesso" });
        }

        /// <summary>
        /// Remover role de um usuário (requer autenticação como Admin)
        /// </summary>
        [HttpDelete("users/{userId}/roles/{roleName}")]
        [Authorize(Roles = "Administrador")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RemoveRoleFromUser(
            Guid userId,
            string roleName,
            CancellationToken cancellationToken)
        {
            await _authService.RemoveRoleFromUserAsync(userId, roleName, cancellationToken);
            return Ok(new { message = $"Role '{roleName}' removida do usuário com sucesso" });
        }

        /// <summary>
        /// Obter roles de um usuário específico (requer autenticação como Admin)
        /// </summary>
        [HttpGet("users/{userId}/roles")]
        [Authorize(Roles = "Administrador")]
        [ProducesResponseType(typeof(UserRolesDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<UserRolesDto>> GetUserRoles(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var userRoles = await _authService.GetUserRolesAsync(userId, cancellationToken);
            return Ok(userRoles);
        }

        /// <summary>
        /// Desativar usuário (requer autenticação como Admin)
        /// </summary>
        [HttpDelete("users/{userId}")]
        [Authorize(Roles = "Administrador")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteUser(Guid userId, CancellationToken cancellationToken)
        {
            var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(currentUserIdClaim, out var currentUserId) && currentUserId == userId)
                return BadRequest(new { message = "Não é possível desativar o próprio usuário" });

            var result = await _authService.DeleteUserAsync(userId, cancellationToken);
            if (!result)
                return NotFound();

            return NoContent();
        }
    }
}
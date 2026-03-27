using BiomePampa.Application.DTOs.Auth;
using BiomePampa.Application.Services;
using BiomePampa.Domain.Authorization;
using BiomePampa.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace BiomePampa.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IPermissionService _permissionService;

        public AuthController(IAuthService authService, IPermissionService permissionService)
        {
            _authService = authService;
            _permissionService = permissionService;
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
            try
            {
                var response = await _authService.LoginAsync(request, cancellationToken);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message }); //
            }
        }

        /// <summary>
        /// Registrar novo usuário (role: Usuario)
        /// </summary>
        [HttpPost("register")]
        [EnableRateLimiting("public")]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<LoginResponseDto>> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _authService.RegisterAsync(request, "Usuario", cancellationToken);
                return CreatedAtAction(nameof(GetCurrentUser), new { }, response);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
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
            try
            {
                var response = await _authService.RegisterAsync(request, "Administrador", cancellationToken);
                return CreatedAtAction(nameof(GetCurrentUser), new { }, response);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Obter dados do usuário atual
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        [EnableRateLimiting("authenticated")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult> GetCurrentUser(CancellationToken cancellationToken)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var user = await _authService.GetUserByIdAsync(userId, cancellationToken);
            if (user == null)
                return NotFound();

            var permissions = await _permissionService.GetUserPermissionsAsync(userId, cancellationToken);

            return Ok(new
            {
                userId = user.Id,
                username = user.Email,
                fullName = user.FullName,
                roles = user.Roles,
                permissions = permissions.OrderBy(p => p).ToList(),
                createdAt = user.CreatedAt,
                lastLoginAt = user.LastLoginAt
            });
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
        /// Atribuir role a um usuário (requer autenticação como Admin)
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
            try
            {
                await _authService.AssignRoleToUserAsync(userId, request.RoleName, cancellationToken);
                return Ok(new { message = $"Role '{request.RoleName}' atribuída ao usuário com sucesso" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
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
            try
            {
                await _authService.RemoveRoleFromUserAsync(userId, roleName, cancellationToken);
                return Ok(new { message = $"Role '{roleName}' removida do usuário com sucesso" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
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
            try
            {
                var userRoles = await _authService.GetUserRolesAsync(userId, cancellationToken);
                return Ok(userRoles);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}

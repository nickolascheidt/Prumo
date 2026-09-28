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
        /// User login
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
        /// Register a new user (no global role; feature roles are assigned per tenant)
        /// </summary>
        [HttpPost("register")]
        [EnableRateLimiting("public")]
        [ProducesResponseType(typeof(RegistrationResultDto), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<RegistrationResultDto>> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
        {
            var response = await _authService.RegisterAsync(request, null, cancellationToken);

            // 202, not 201: the account exists but is useless until the e-mail is
            // confirmed. There is no resource to point a Location at.
            return Accepted(response);
        }

        /// <summary>
        /// Register a new administrator (master admin only)
        /// </summary>
        [HttpPost("register/admin")]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
        [ProducesResponseType(typeof(RegistrationResultDto), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<RegistrationResultDto>> RegisterAdmin([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
        {
            var response = await _authService.RegisterAsync(request, Permissions.Roles.MasterAdmin, cancellationToken);
            return Accepted(response);
        }

        /// <summary>
        /// Confirm the e-mail from the link sent at sign-up
        /// </summary>
        [HttpPost("confirm-email")]
        [EnableRateLimiting("public")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequestDto request, CancellationToken cancellationToken)
        {
            var confirmed = await _authService.ConfirmEmailAsync(request, cancellationToken);

            // One answer for an expired link, a tampered one or a nonexistent user:
            // telling them apart would tell a stranger which ids exist.
            return confirmed
                ? NoContent()
                : BadRequest(new { message = "Invalid or expired link. Request a new confirmation e-mail." });
        }

        /// <summary>
        /// Resend the confirmation e-mail
        /// </summary>
        [HttpPost("resend-confirmation")]
        [EnableRateLimiting("public")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public async Task<IActionResult> ResendConfirmation([FromBody] EmailOnlyRequestDto request, CancellationToken cancellationToken)
        {
            await _authService.ResendConfirmationAsync(request.Email, cancellationToken);
            return Accepted();
        }

        /// <summary>
        /// Request the password reset e-mail
        /// </summary>
        [HttpPost("forgot-password")]
        [EnableRateLimiting("public")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public async Task<IActionResult> ForgotPassword([FromBody] EmailOnlyRequestDto request, CancellationToken cancellationToken)
        {
            await _authService.ForgotPasswordAsync(request.Email, cancellationToken);

            // Always 202, whether the e-mail exists or not. Answering differently for an
            // unknown address would turn this endpoint into an account enumerator.
            return Accepted();
        }

        /// <summary>
        /// Set the new password from the reset link
        /// </summary>
        [HttpPost("reset-password")]
        [EnableRateLimiting("public")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request, CancellationToken cancellationToken)
        {
            var reset = await _authService.ResetPasswordAsync(request, cancellationToken);

            return reset
                ? NoContent()
                : BadRequest(new { message = "Invalid or expired link, or the password does not meet the requirements." });
        }

        /// <summary>
        /// Get the current user
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

            // There are no permission strings: what the SPA uses to build the menu and
            // screens is GET /api/resources/my-permissions, which returns a level per
            // resource.
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
        /// List all users (master admin only)
        /// </summary>
        [HttpGet("users")]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
        [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers(CancellationToken cancellationToken)
        {
            var users = await _authService.GetAllUsersAsync(cancellationToken);
            return Ok(users);
        }

        /// <summary>
        /// Resolves any e-mail to id and name — data about every user in every tenant.
        /// Master admin only.
        /// </summary>
        [HttpGet("users/lookup")]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
        [ProducesResponseType(typeof(UserLookupDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserLookupDto>> LookupUserByEmail(
            [FromQuery] string email, CancellationToken cancellationToken)
        {
            var result = await _authService.LookupUserByEmailAsync(email, cancellationToken);
            return result == null ? NotFound() : Ok(result);
        }

        /// <summary>
        /// Change password
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
                return BadRequest(new { message = "Could not change the password. Check the current password." });

            return Ok(new { message = "Password changed" });
        }

        /// <summary>
        /// Assign a global role to a user (master admin only). Only the master
        /// 'Administrator' role can be assigned globally; feature roles are assigned per
        /// tenant.
        /// </summary>
        [HttpPost("users/{userId}/roles")]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
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
            return Ok(new { message = $"Role '{request.RoleName}' assigned to the user" });
        }

        /// <summary>
        /// Remove a role from a user (master admin only)
        /// </summary>
        [HttpDelete("users/{userId}/roles/{roleName}")]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
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
            return Ok(new { message = $"Role '{roleName}' removed from the user" });
        }

        /// <summary>
        /// Get a user's roles (master admin only)
        /// </summary>
        [HttpGet("users/{userId}/roles")]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
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
        /// Deactivate a user (master admin only)
        /// </summary>
        [HttpDelete("users/{userId}")]
        [Authorize(Roles = Permissions.Roles.MasterAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteUser(Guid userId, CancellationToken cancellationToken)
        {
            var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(currentUserIdClaim, out var currentUserId) && currentUserId == userId)
                return BadRequest(new { message = "You cannot deactivate your own user" });

            var result = await _authService.DeleteUserAsync(userId, cancellationToken);
            if (!result)
                return NotFound();

            return NoContent();
        }
    }
}
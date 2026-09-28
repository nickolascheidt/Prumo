using Prumo.Application.DTOs.Auth;
using Prumo.Domain.Authorization;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Authorization;
using Prumo.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Prumo.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly ITenantService _tenantService;
        private readonly ITenantRoleService _tenantRoleService;
        private readonly INotificationPublisher _notifications;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            ITenantService tenantService,
            ITenantRoleService tenantRoleService,
            INotificationPublisher notifications)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _tenantService = tenantService;
            _tenantRoleService = tenantRoleService;
            _notifications = notifications;
        }

        private string AppBaseUrl =>
            _configuration["App:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:4200";

        private int TokenExpirationHours =>
            int.TryParse(_configuration["Jwt:TokenExpirationHours"], out var hours) ? hours : 8;

        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !user.IsActive)
                throw new UnauthorizedAccessException("Invalid credentials");

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                    throw new UnauthorizedAccessException("Account temporarily locked. Try again later.");

                throw new UnauthorizedAccessException("Invalid credentials");
            }

            // After the password, on purpose: refusing earlier would tell anyone that this
            // address has an account. And it is here, not in SignIn.RequireConfirmedEmail,
            // because CheckPasswordSignInAsync does not apply that option — only
            // PasswordSignInAsync does.
            if (!user.EmailConfirmed)
                throw new EmailNotConfirmedException();

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            Guid? tenantId = null;
            if (!string.IsNullOrWhiteSpace(request.TenantSlug))
            {
                var tenant = await _tenantService.GetBySlugAsync(request.TenantSlug, cancellationToken);
                if (tenant == null)
                    throw new UnauthorizedAccessException("Tenant not found");

                if (!await _tenantService.IsMemberAsync(tenant.Id, user.Id, cancellationToken))
                    throw new UnauthorizedAccessException("User does not belong to this tenant");

                tenantId = tenant.Id;
            }

            var token = await GenerateJwtToken(user, tenantId);
            // tenantId may be null (login without a slug) — then MapToUserDto returns only
            // the global roles, which is correct. With a slug, the body must agree with the
            // claims: see the comment in SelectTenantAsync.
            var userDto = await MapToUserDto(user, tenantId, cancellationToken);

            return new LoginResponseDto(
                token,
                DateTime.UtcNow.AddHours(TokenExpirationHours),
                userDto,
                tenantId
            );
        }

        public async Task<LoginResponseDto> SelectTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null || !user.IsActive)
                throw new UnauthorizedAccessException("Invalid user");

            if (!await _tenantService.IsMemberAsync(tenantId, userId, cancellationToken))
                throw new UnauthorizedAccessException("User does not belong to this tenant");

            var token = await GenerateJwtToken(user, tenantId);
            // The body has to agree with the claims just issued: the SPA stores this user
            // and draws the dashboard from it, without re-reading the token. Without the
            // tenantId here, someone with a feature role sees "No role assigned".
            var userDto = await MapToUserDto(user, tenantId, cancellationToken);

            return new LoginResponseDto(
                token,
                DateTime.UtcNow.AddHours(TokenExpirationHours),
                userDto,
                tenantId
            );
        }

        public async Task<RegistrationResultDto> RegisterAsync(RegisterRequestDto request, string? roleName, CancellationToken cancellationToken = default)
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
                throw new InvalidOperationException("E-mail already registered");

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create user: {errors}");
            }

            // Self-registration grants no global role; tenant feature roles are assigned per-tenant.
            if (!string.IsNullOrWhiteSpace(roleName))
                await _userManager.AddToRoleAsync(user, roleName);

            // Invitations already waiting for this address become memberships now. The
            // e-mail is not what authorizes: whoever signed up with the invited address
            // proved they own the mailbox, which is the same proof confirmation asks for.
            await _tenantService.AcceptPendingInvitationsAsync(user.Id, user.Email!, cancellationToken);

            await PublishEmailConfirmationAsync(user, cancellationToken);

            return new RegistrationResultDto(user.Id, user.Email!);
        }

        public async Task<bool> ConfirmEmailAsync(ConfirmEmailRequestDto request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(request.UserId.ToString());
            if (user is null)
                return false;

            var result = await _userManager.ConfirmEmailAsync(user, DecodeToken(request.Token));
            return result.Succeeded;
        }

        public async Task ResendConfirmationAsync(string email, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByEmailAsync(email);

            // Deliberate silence: a missing account and an already confirmed one leave here
            // the same way as the one that got the e-mail.
            if (user is null || user.EmailConfirmed)
                return;

            await PublishEmailConfirmationAsync(user, cancellationToken);
        }

        public async Task ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user is null || !user.IsActive)
                return;

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            await _notifications.PublishAsync(
                new NotificationMessage
                {
                    Type = NotificationTypes.PasswordReset,
                    To = user.Email!,
                    CorrelationId = Guid.NewGuid(),
                    Data = new Dictionary<string, string>
                    {
                        ["link"] = $"{AppBaseUrl}/auth/reset-password?uid={user.Id}&token={EncodeToken(token)}"
                    }
                },
                cancellationToken);
        }

        public async Task<bool> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(request.UserId.ToString());
            if (user is null)
                return false;

            var result = await _userManager.ResetPasswordAsync(
                user, DecodeToken(request.Token), request.NewPassword);

            if (!result.Succeeded)
                return false;

            // Whoever proved access to the mailbox proved what confirmation asks for.
            // Without this, someone who forgot their password before confirming would be
            // stuck forever: they reset it and still cannot get in.
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

            return true;
        }

        private async Task PublishEmailConfirmationAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            await _notifications.PublishAsync(
                new NotificationMessage
                {
                    Type = NotificationTypes.EmailConfirmation,
                    To = user.Email!,
                    CorrelationId = Guid.NewGuid(),
                    Data = new Dictionary<string, string>
                    {
                        ["name"] = user.FullName ?? user.Email!,
                        ["link"] = $"{AppBaseUrl}/auth/confirm-email?uid={user.Id}&token={EncodeToken(token)}"
                    }
                },
                cancellationToken);
        }

        // Identity tokens are opaque and contain characters that do not survive a query
        // string. URL-safe Base64 on the way out, the inverse on the way back.
        private static string EncodeToken(string token) =>
            WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        private static string DecodeToken(string encoded)
        {
            try
            {
                return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded));
            }
            catch (FormatException)
            {
                // A truncated or hand-edited link. Returning something invalid makes
                // ConfirmEmailAsync/ResetPasswordAsync answer "invalid token", which is the
                // same answer as an expired token — and it is the right one.
                return string.Empty;
            }
        }

        public async Task<UserDto?> GetUserByIdAsync(Guid userId, Guid? tenantId = null, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return null;

            return await MapToUserDto(user, tenantId, cancellationToken);
        }

        public async Task<IEnumerable<UserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
        {
            var users = _userManager.Users.ToList();
            var userDtos = new List<UserDto>();

            foreach (var user in users)
            {
                userDtos.Add(await MapToUserDto(user));
            }

            return userDtos;
        }

        public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            return result.Succeeded;
        }

        public async Task AssignRoleToUserAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
        {
            if (roleName != Permissions.Roles.MasterAdmin)
                throw new InvalidOperationException(
                    "Only the master admin role can be assigned globally. Use per-tenant role assignment for feature roles.");

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                throw new KeyNotFoundException($"User with ID '{userId}' not found");

            var roleExists = await _userManager.GetRolesAsync(user);
            if (roleExists.Contains(roleName))
                throw new InvalidOperationException($"User already has the role '{roleName}'");

            var result = await _userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to assign role: {errors}");
            }
        }

        public async Task RemoveRoleFromUserAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                throw new KeyNotFoundException($"User with ID '{userId}' not found");

            var userRoles = await _userManager.GetRolesAsync(user);
            if (!userRoles.Contains(roleName))
                throw new InvalidOperationException($"User does not have the role '{roleName}'");

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to remove role: {errors}");
            }
        }

        public async Task<UserRolesDto> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                throw new KeyNotFoundException($"User with ID '{userId}' not found");

            var roles = await _userManager.GetRolesAsync(user);

            return new UserRolesDto(
                user.Id,
                user.Email!,
                user.FullName ?? string.Empty,
                roles
            );
        }

        public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            user.IsActive = false;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to deactivate user: {errors}");
            }

            return true;
        }

        private async Task<string> GenerateJwtToken(ApplicationUser user, Guid? tenantId)
        {
            var globalRoles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.UserName!),
                new(ClaimTypes.Email, user.Email!),
                new("FullName", user.FullName ?? string.Empty)
            };

            IEnumerable<string> roleClaims = globalRoles;

            if (tenantId.HasValue)
            {
                claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));

                // Effective roles = global (master) ∪ per-tenant feature roles.
                roleClaims = await _tenantRoleService.GetEffectiveRoleNamesAsync(
                    user.Id, tenantId.Value, (IReadOnlyCollection<string>)globalRoles);

                var tenantRole = await _tenantService.GetUserRoleAsync(tenantId.Value, user.Id);
                if (tenantRole.HasValue)
                    claims.Add(new Claim("tenant_role", tenantRole.Value.ToString()));

                // There is no "permission" claim: access is decided by ResourcePermission,
                // resolved per request, not by the token.
            }

            claims.AddRange(roleClaims.Distinct().Select(r => new Claim(ClaimTypes.Role, r)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is not configured")));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(TokenExpirationHours),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<UserDto> MapToUserDto(
            ApplicationUser user, Guid? tenantId = null, CancellationToken cancellationToken = default)
        {
            var globalRoles = await _userManager.GetRolesAsync(user);

            // Mirror GenerateJwtToken: inside a tenant the effective roles are
            // global (master) ∪ per-tenant feature roles.
            IEnumerable<string> roles = tenantId.HasValue
                ? await _tenantRoleService.GetEffectiveRoleNamesAsync(
                    user.Id, tenantId.Value, (IReadOnlyCollection<string>)globalRoles, cancellationToken)
                : globalRoles;

            return new UserDto(
                user.Id,
                user.Email!,
                user.FullName,
                user.PhoneNumber,
                roles,
                user.CreatedAt,
                user.LastLoginAt
            );
        }

        public async Task<UserLookupDto?> LookupUserByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return null;
            return new UserLookupDto(user.Id, user.Email!, user.FullName);
        }
    }
}

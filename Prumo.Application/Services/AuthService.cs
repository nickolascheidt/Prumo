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
                throw new UnauthorizedAccessException("Credenciais inválidas");

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                    throw new UnauthorizedAccessException("Conta bloqueada temporariamente. Tente novamente mais tarde.");

                throw new UnauthorizedAccessException("Credenciais inválidas");
            }

            // Depois da senha, de propósito: recusar antes contaria a qualquer um que este
            // endereço tem conta. E é aqui, e não em SignIn.RequireConfirmedEmail, porque
            // CheckPasswordSignInAsync não aplica aquela opção — só PasswordSignInAsync.
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
                    throw new UnauthorizedAccessException("Tenant não encontrado");

                if (!await _tenantService.IsMemberAsync(tenant.Id, user.Id, cancellationToken))
                    throw new UnauthorizedAccessException("Usuário não pertence a este tenant");

                tenantId = tenant.Id;
            }

            var token = await GenerateJwtToken(user, tenantId);
            // tenantId pode ser null (login sem slug) — e aí MapToUserDto devolve só as
            // roles globais, que é o correto. Com slug, o corpo precisa concordar com os
            // claims: ver o comentário em SelectTenantAsync.
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
                throw new UnauthorizedAccessException("Usuário inválido");

            if (!await _tenantService.IsMemberAsync(tenantId, userId, cancellationToken))
                throw new UnauthorizedAccessException("Usuário não pertence a este tenant");

            var token = await GenerateJwtToken(user, tenantId);
            // O corpo tem de concordar com os claims que acabaram de ser emitidos: o SPA
            // guarda este user e desenha o dashboard a partir dele, sem reler o token.
            // Sem o tenantId aqui, quem tem feature role vê "Nenhuma role atribuída".
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
                throw new InvalidOperationException("Email já cadastrado");

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
                throw new InvalidOperationException($"Erro ao criar usuário: {errors}");
            }

            // Self-registration grants no global role; tenant feature roles are assigned per-tenant.
            if (!string.IsNullOrWhiteSpace(roleName))
                await _userManager.AddToRoleAsync(user, roleName);

            // Convites que já esperavam por este endereço viram associação agora. O e-mail
            // não é o que autoriza: quem se cadastrou com o endereço convidado provou ser
            // dono da caixa, que é a mesma prova da confirmação.
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

            // Silêncio deliberado: conta inexistente e conta já confirmada saem daqui do
            // mesmo jeito que a que recebeu o e-mail.
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

            // Quem provou ter acesso à caixa de e-mail provou o que a confirmação pede.
            // Sem isto, quem esqueceu a senha antes de confirmar ficaria travado para
            // sempre: redefine e ainda assim não entra.
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

        // Os tokens do Identity são opacos e contêm caracteres que não sobrevivem a uma
        // query string. Base64 URL-safe na ida, o inverso na volta.
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
                // Link truncado ou mexido à mão. Devolver algo inválido faz o
                // ConfirmEmailAsync/ResetPasswordAsync responder "token inválido", que é a
                // mesma resposta de um token expirado — e é a resposta certa.
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
                throw new KeyNotFoundException($"Usuário com ID '{userId}' não encontrado");

            var roleExists = await _userManager.GetRolesAsync(user);
            if (roleExists.Contains(roleName))
                throw new InvalidOperationException($"Usuário já possui a role '{roleName}'");

            var result = await _userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Erro ao atribuir role: {errors}");
            }
        }

        public async Task RemoveRoleFromUserAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                throw new KeyNotFoundException($"Usuário com ID '{userId}' não encontrado");

            var userRoles = await _userManager.GetRolesAsync(user);
            if (!userRoles.Contains(roleName))
                throw new InvalidOperationException($"Usuário não possui a role '{roleName}'");

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Erro ao remover role: {errors}");
            }
        }

        public async Task<UserRolesDto> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                throw new KeyNotFoundException($"Usuário com ID '{userId}' não encontrado");

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
                throw new InvalidOperationException($"Erro ao desativar usuário: {errors}");
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

                // O claim "permission" saiu no item 3B junto com o RolePermission: ele
                // carregava as strings de um catálogo que nenhum endpoint consultava
                // (zero [Authorize(Policy=...)]) e que o frontend nunca lia. Quem gateia
                // é ResourcePermission, resolvida por request e não pelo token.
            }

            claims.AddRange(roleClaims.Distinct().Select(r => new Claim(ClaimTypes.Role, r)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key não configurada")));
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

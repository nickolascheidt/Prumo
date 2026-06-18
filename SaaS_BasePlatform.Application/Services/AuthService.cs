using SaaS_BasePlatform.Application.DTOs.Auth;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Infrastructure.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SaaS_BasePlatform.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly IPermissionService _permissionService;
        private readonly ITenantService _tenantService;
        private readonly ITenantRoleService _tenantRoleService;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            IPermissionService permissionService,
            ITenantService tenantService,
            ITenantRoleService tenantRoleService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _permissionService = permissionService;
            _tenantService = tenantService;
            _tenantRoleService = tenantRoleService;
        }

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
            var userDto = await MapToUserDto(user);

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
            var userDto = await MapToUserDto(user);

            return new LoginResponseDto(
                token,
                DateTime.UtcNow.AddHours(TokenExpirationHours),
                userDto,
                tenantId
            );
        }

        public async Task<LoginResponseDto> RegisterAsync(RegisterRequestDto request, string roleName, CancellationToken cancellationToken = default)
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

            // Add role
            await _userManager.AddToRoleAsync(user, roleName);

            var token = await GenerateJwtToken(user, null);
            var userDto = await MapToUserDto(user);

            return new LoginResponseDto(
                token,
                DateTime.UtcNow.AddHours(TokenExpirationHours),
                userDto
            );
        }

        public async Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return null;

            return await MapToUserDto(user);
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

                var permissions = await _permissionService.GetUserPermissionsForTenantAsync(user.Id, tenantId.Value);
                claims.AddRange(permissions.Select(p => new Claim("permission", p)));
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

        private async Task<UserDto> MapToUserDto(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);

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

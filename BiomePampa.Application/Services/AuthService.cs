using BiomePampa.Application.DTOs.Auth;
using BiomePampa.Domain.Entities;
using BiomePampa.Infrastructure.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BiomePampa.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly IPermissionService _permissionService;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            IPermissionService permissionService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _permissionService = permissionService;
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

            var token = await GenerateJwtToken(user);
            var userDto = await MapToUserDto(user);

            return new LoginResponseDto(
                token,
                DateTime.UtcNow.AddHours(TokenExpirationHours),
                userDto
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

            var token = await GenerateJwtToken(user);
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

        private async Task<string> GenerateJwtToken(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim("FullName", user.FullName ?? string.Empty)
            };

            // Adicionar claims de roles
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            // Adicionar claims de permissions
            var permissions = await _permissionService.GetUserPermissionsAsync(user.Id);
            claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key não configurada")));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(TokenExpirationHours),
                signingCredentials: creds
            );

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
    }
}

using SaaS_BasePlatform.Application.DTOs.Auth;

namespace SaaS_BasePlatform.Application.Services
{
    public interface IAuthService
    {
        Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
        Task<LoginResponseDto> SelectTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
        Task<LoginResponseDto> RegisterAsync(RegisterRequestDto request, string roleName, CancellationToken cancellationToken = default);
        Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<UserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default);
        Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto request, CancellationToken cancellationToken = default);
        Task AssignRoleToUserAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
        Task RemoveRoleFromUserAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
        Task<UserRolesDto> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserLookupDto?> LookupUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    }
}

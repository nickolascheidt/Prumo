using Prumo.Application.DTOs.Auth;

namespace Prumo.Application.Services
{
    public interface IAuthService
    {
        Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
        Task<LoginResponseDto> SelectTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
        /// <summary>
        /// Creates the account **unconfirmed** and publishes the confirmation e-mail. Does
        /// not return a session: confirming the address is a prerequisite for signing in.
        /// </summary>
        Task<RegistrationResultDto> RegisterAsync(RegisterRequestDto request, string? roleName, CancellationToken cancellationToken = default);

        Task<bool> ConfirmEmailAsync(ConfirmEmailRequestDto request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resends the confirmation. **Does not reveal** whether the e-mail exists or was
        /// already confirmed — the answer is the same in all three cases.
        /// </summary>
        Task ResendConfirmationAsync(string email, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends the password reset e-mail. **Does not reveal** whether the e-mail exists:
        /// answering differently for an unknown address would turn the endpoint into an
        /// account enumerator.
        /// </summary>
        Task ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);

        Task<bool> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
        /// <summary>
        /// Reads a user. When <paramref name="tenantId"/> is supplied the returned roles are the
        /// effective roles for that tenant (global ∪ per-tenant feature roles), matching the JWT;
        /// when it is null only global roles are returned.
        /// </summary>
        Task<UserDto?> GetUserByIdAsync(Guid userId, Guid? tenantId = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<UserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default);
        Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto request, CancellationToken cancellationToken = default);
        Task AssignRoleToUserAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
        Task RemoveRoleFromUserAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
        Task<UserRolesDto> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserLookupDto?> LookupUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    }
}

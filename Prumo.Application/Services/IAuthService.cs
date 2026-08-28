using Prumo.Application.DTOs.Auth;

namespace Prumo.Application.Services
{
    public interface IAuthService
    {
        Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
        Task<LoginResponseDto> SelectTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
        /// <summary>
        /// Cria a conta **não confirmada** e publica o e-mail de confirmação. Não devolve
        /// sessão: confirmar o endereço é pré-requisito para entrar.
        /// </summary>
        Task<RegistrationResultDto> RegisterAsync(RegisterRequestDto request, string? roleName, CancellationToken cancellationToken = default);

        Task<bool> ConfirmEmailAsync(ConfirmEmailRequestDto request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reenvia a confirmação. **Não revela** se o e-mail existe ou se já foi
        /// confirmado — a resposta é a mesma nos três casos.
        /// </summary>
        Task ResendConfirmationAsync(string email, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dispara o e-mail de redefinição. **Não revela** se o e-mail existe: responder
        /// diferente para endereço desconhecido transformaria o endpoint num enumerador de
        /// contas.
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

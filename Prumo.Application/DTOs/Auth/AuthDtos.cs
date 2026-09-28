namespace Prumo.Application.DTOs.Auth
{
    public record LoginRequestDto(
        string Email,
        string Password,
        string? TenantSlug = null
    );

    public record SelectTenantRequestDto(Guid TenantId);

    public record RegisterRequestDto(
        string Email,
        string Password,
        string FullName,
        string? PhoneNumber
    );

    public record LoginResponseDto(
        string Token,
        DateTime ExpiresAt,
        UserDto User,
        Guid? TenantId = null
    );

    /// <summary>
    /// What sign-up returns. **No token**: someone who just signed up has not proved the
    /// e-mail is theirs yet, and returning a session here would make confirmation
    /// decorative.
    /// </summary>
    public record RegistrationResultDto(
        Guid UserId,
        string Email
    );

    public record ConfirmEmailRequestDto(Guid UserId, string Token);

    public record EmailOnlyRequestDto(string Email);

    public record ResetPasswordRequestDto(Guid UserId, string Token, string NewPassword);

    public record UserDto(
        Guid Id,
        string Email,
        string? FullName,
        string? PhoneNumber,
        IEnumerable<string> Roles,
        DateTime CreatedAt,
        DateTime? LastLoginAt
    );

    public record ChangePasswordDto(
        string CurrentPassword,
        string NewPassword
    );

    public record CurrentUserDto(
        Guid UserId,
        string Email,
        string? FullName,
        IEnumerable<string> Roles,
        IReadOnlyList<string> Permissions,
        DateTime CreatedAt,
        DateTime? LastLoginAt
    );
}

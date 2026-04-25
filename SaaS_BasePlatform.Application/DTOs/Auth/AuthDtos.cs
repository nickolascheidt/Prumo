namespace SaaS_BasePlatform.Application.DTOs.Auth
{
    public record LoginRequestDto(
        string Email,
        string Password
    );

    public record RegisterRequestDto(
        string Email,
        string Password,
        string FullName,
        string? PhoneNumber
    );

    public record LoginResponseDto(
        string Token,
        DateTime ExpiresAt,
        UserDto User
    );

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

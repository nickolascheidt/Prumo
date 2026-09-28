namespace Prumo.Infrastructure.Services;

/// <summary>
/// A notification the application wants delivered: one recipient, a type, and the
/// type-specific fields in <see cref="Data"/>.
///
/// **No credentials here.** The tokens carried in <see cref="Data"/> are ASP.NET Identity
/// tokens — single-use and expiring. Passwords, hashes and session tokens never go in.
/// </summary>
public sealed record NotificationMessage
{
    /// <summary>Discriminates the contents of <see cref="Data"/>. See <see cref="NotificationTypes"/>.</summary>
    public required string Type { get; init; }

    /// <summary>Recipient. One per message.</summary>
    public required string To { get; init; }

    /// <summary>Follows the message through the logs.</summary>
    public required Guid CorrelationId { get; init; }

    /// <summary>Type-specific fields.</summary>
    public required IReadOnlyDictionary<string, string> Data { get; init; }
}

/// <summary>The notification types that exist.</summary>
public static class NotificationTypes
{
    public const string EmailConfirmation = "email.confirmation";
    public const string PasswordReset = "email.password-reset";
    public const string TenantInvitation = "email.tenant-invitation";

    public static readonly IReadOnlyList<string> All =
        [EmailConfirmation, PasswordReset, TenantInvitation];
}

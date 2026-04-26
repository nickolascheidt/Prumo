using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Application.DTOs.ApiKeys
{
    public record CreateApiKeyRequestDto(string Name, ApiKeyType Type, DateTime? ExpiresAt);

    public record CreateApiKeyResponseDto(
        Guid Id,
        string Name,
        ApiKeyType Type,
        string Key,
        string Prefix,
        DateTime? ExpiresAt,
        DateTime CreatedAt);

    public record ApiKeyDto(
        Guid Id,
        string Name,
        ApiKeyType Type,
        string Prefix,
        DateTime CreatedAt,
        DateTime? ExpiresAt,
        DateTime? RevokedAt,
        DateTime? LastUsedAt);
}

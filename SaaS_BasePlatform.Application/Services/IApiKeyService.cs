using SaaS_BasePlatform.Application.DTOs.ApiKeys;
using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Application.Services
{
    public interface IApiKeyService
    {
        Task<CreateApiKeyResponseDto> CreateAsync(Guid tenantId, Guid createdByUserId, CreateApiKeyRequestDto request, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApiKeyDto>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task RevokeAsync(Guid tenantId, Guid apiKeyId, CancellationToken cancellationToken = default);
        Task<ApiKey?> ValidateAsync(string presentedKey, CancellationToken cancellationToken = default);
    }
}

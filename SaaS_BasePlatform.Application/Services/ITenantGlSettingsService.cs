using SaaS_BasePlatform.Application.DTOs.Finance;

namespace SaaS_BasePlatform.Application.Services
{
    public interface ITenantGlSettingsService
    {
        Task<TenantGlSettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken ct = default);
        Task<TenantGlSettingsDto> UpdateSettingsAsync(Guid tenantId, UpdateTenantGlSettingsDto request, CancellationToken ct = default);
    }
}

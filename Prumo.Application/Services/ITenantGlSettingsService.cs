using Prumo.Application.DTOs.Finance;

namespace Prumo.Application.Services
{
    public interface ITenantGlSettingsService
    {
        Task<TenantGlSettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken ct = default);
        Task<TenantGlSettingsDto> UpdateSettingsAsync(Guid tenantId, UpdateTenantGlSettingsDto request, CancellationToken ct = default);
    }
}

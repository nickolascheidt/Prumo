using Microsoft.EntityFrameworkCore;
using SaaS_BasePlatform.Application.DTOs.Finance;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Infrastructure.Data;

namespace SaaS_BasePlatform.Application.Services
{
    public class TenantGlSettingsService : ITenantGlSettingsService
    {
        private readonly ApplicationDbContext _db;

        public TenantGlSettingsService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<TenantGlSettingsDto> GetSettingsAsync(
            Guid tenantId, CancellationToken ct = default)
        {
            var settings = await _db.TenantGlSettings
                .Include(s => s.DefaultCashAccount)
                .Include(s => s.DefaultAccountsPayableAccount)
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

            if (settings == null)
                return new TenantGlSettingsDto(tenantId, null, null, null, null);

            return new TenantGlSettingsDto(
                tenantId,
                settings.DefaultCashAccountId,
                settings.DefaultCashAccount?.Code,
                settings.DefaultAccountsPayableAccountId,
                settings.DefaultAccountsPayableAccount?.Code
            );
        }

        public async Task<TenantGlSettingsDto> UpdateSettingsAsync(
            Guid tenantId, UpdateTenantGlSettingsDto request, CancellationToken ct = default)
        {
            if (request.DefaultCashAccountId.HasValue)
                await ValidateAnalyticAccountAsync(tenantId, request.DefaultCashAccountId.Value, "Cash account", ct);

            if (request.DefaultAccountsPayableAccountId.HasValue)
                await ValidateAnalyticAccountAsync(tenantId, request.DefaultAccountsPayableAccountId.Value, "Accounts payable account", ct);

            var settings = await _db.TenantGlSettings
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

            if (settings == null)
            {
                settings = new TenantGlSettings { TenantId = tenantId };
                _db.TenantGlSettings.Add(settings);
            }

            settings.DefaultCashAccountId = request.DefaultCashAccountId;
            settings.DefaultAccountsPayableAccountId = request.DefaultAccountsPayableAccountId;

            await _db.SaveChangesAsync(ct);
            return await GetSettingsAsync(tenantId, ct);
        }

        private async Task ValidateAnalyticAccountAsync(
            Guid tenantId, Guid accountId, string label, CancellationToken ct)
        {
            var exists = await _db.Accounts
                .IgnoreQueryFilters()
                .AnyAsync(a => a.TenantId == tenantId && a.Id == accountId && a.IsAnalytic && a.IsActive, ct);
            if (!exists)
                throw new KeyNotFoundException($"{label} not found or is not an active analytic account.");
        }
    }
}

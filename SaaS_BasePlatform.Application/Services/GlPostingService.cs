using SaaS_BasePlatform.Application.DTOs.Finance;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Application.Services
{
    public class GlPostingService : IGlPostingService
    {
        private readonly ITenantGlSettingsService _settingsService;
        private readonly IJournalService _journalService;

        public GlPostingService(ITenantGlSettingsService settingsService, IJournalService journalService)
        {
            _settingsService = settingsService;
            _journalService  = journalService;
        }

        public async Task PostApPaymentAsync(
            Guid tenantId, Guid apEntryId, string description,
            decimal amount, DateTime paidAt, Guid userId,
            CancellationToken ct = default)
        {
            var settings = await _settingsService.GetSettingsAsync(tenantId, ct);

            if (settings.DefaultCashAccountId == null || settings.DefaultAccountsPayableAccountId == null)
                return;

            var request = new CreateJournalEntryRequestDto(
                Date: paidAt,
                Description: $"AP payment: {description}",
                Lines: new[]
                {
                    new CreateJournalLineDto(
                        settings.DefaultAccountsPayableAccountId.Value,
                        (int)JournalEntryType.Debit,
                        amount),
                    new CreateJournalLineDto(
                        settings.DefaultCashAccountId.Value,
                        (int)JournalEntryType.Credit,
                        amount)
                }
            );

            await _journalService.CreateJournalEntryAsync(
                tenantId, userId, request,
                sourceModule:     "AccountsPayable",
                sourceDocumentId: apEntryId,
                ct:               ct);
        }
    }
}

using SaaS_BasePlatform.Application.DTOs.Finance;
using SaaS_BasePlatform.Domain.Common;

namespace SaaS_BasePlatform.Application.Services
{
    public interface IJournalService
    {
        Task<PagedResult<JournalEntryListItemDto>> ListEntriesAsync(
            Guid tenantId, JournalEntryQueryDto query, CancellationToken ct = default);
        Task<JournalEntryDto?> GetEntryAsync(
            Guid tenantId, Guid entryId, CancellationToken ct = default);
        Task<JournalEntryDto> CreateJournalEntryAsync(
            Guid tenantId, Guid userId, CreateJournalEntryRequestDto request,
            string? sourceModule = null, Guid? sourceDocumentId = null,
            CancellationToken ct = default);
        Task<AccountStatementDto> GetAccountStatementAsync(
            Guid tenantId, Guid accountId,
            DateTime? from = null, DateTime? to = null,
            CancellationToken ct = default);
    }
}

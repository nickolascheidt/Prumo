using Prumo.Application.DTOs.AccountsPayable;
using Prumo.Domain.Common;

namespace Prumo.Application.Services
{
    public interface IAccountsPayableService
    {
        // Categories
        Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(Guid tenantId, bool includeInactive, CancellationToken cancellationToken = default);
        Task<CategoryDto> CreateCategoryAsync(Guid tenantId, CreateCategoryRequestDto request, CancellationToken cancellationToken = default);
        Task<CategoryDto> UpdateCategoryAsync(Guid tenantId, Guid categoryId, UpdateCategoryRequestDto request, CancellationToken cancellationToken = default);
        Task DeactivateCategoryAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default);

        // Entries
        Task<PagedResult<EntryDto>> ListEntriesAsync(Guid tenantId, EntryListQueryDto query, CancellationToken cancellationToken = default);
        Task<EntryDto?> GetEntryAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken = default);
        Task<EntryDto> CreateEntryAsync(Guid tenantId, Guid createdByUserId, CreateEntryRequestDto request, CancellationToken cancellationToken = default);
        Task<EntryDto> UpdateEntryAsync(Guid tenantId, Guid entryId, UpdateEntryRequestDto request, CancellationToken cancellationToken = default);
        Task<EntryDto> MarkEntryPaidAsync(Guid tenantId, Guid entryId, Guid paidByUserId, MarkPaidRequestDto request, CancellationToken cancellationToken = default);
        Task<EntryDto> CancelEntryAsync(Guid tenantId, Guid entryId, CancelEntryRequestDto request, CancellationToken cancellationToken = default);

        // Bulk
        Task<BulkEntriesResponseDto> BulkCreateAsync(Guid tenantId, Guid createdByUserId, BulkEntriesRequestDto request, bool canManageCategories, CancellationToken cancellationToken = default);

        // Reports
        Task<SummaryResponseDto> GetSummaryAsync(Guid tenantId, SummaryQueryDto query, CancellationToken cancellationToken = default);
        Task<byte[]> ExportEntriesCsvAsync(Guid tenantId, EntryListQueryDto query, CancellationToken cancellationToken = default);
    }
}

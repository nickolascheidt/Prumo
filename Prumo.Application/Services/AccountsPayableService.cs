using Microsoft.EntityFrameworkCore;
using Prumo.Application.DTOs.AccountsPayable;
using Prumo.Application.Services.AccountsPayable;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data;

namespace Prumo.Application.Services
{
    public class AccountsPayableService : IAccountsPayableService
    {
        private const int MaxPageSize = 100;
        private const int DefaultPageSize = 20;
        private const int MaxBulkLines = 500;

        private readonly ApplicationDbContext _db;
        private readonly IGlPostingService _glPosting;

        public AccountsPayableService(ApplicationDbContext db, IGlPostingService glPosting)
        {
            _db        = db;
            _glPosting = glPosting;
        }

        // ---------- Categories ----------

        public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(Guid tenantId, bool includeInactive, CancellationToken cancellationToken = default)
        {
            var query = _db.AccountsPayableCategories
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId);

            if (!includeInactive)
                query = query.Where(c => c.IsActive);

            return await query
                .OrderBy(c => c.Name)
                .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.Color, c.IsActive, c.CreatedAt, c.UpdatedAt))
                .ToListAsync(cancellationToken);
        }

        public async Task<CategoryDto> CreateCategoryAsync(Guid tenantId, CreateCategoryRequestDto request, CancellationToken cancellationToken = default)
        {
            var name = (request.Name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Category name is required.", nameof(request));

            var nameTaken = await _db.AccountsPayableCategories
                .IgnoreQueryFilters()
                .AnyAsync(c => c.TenantId == tenantId && c.Name == name, cancellationToken);
            if (nameTaken)
                throw new InvalidOperationException($"A category named '{name}' already exists.");

            var category = new AccountsPayableCategory
            {
                TenantId = tenantId,
                Name = name,
                Description = request.Description,
                Color = request.Color
            };

            _db.AccountsPayableCategories.Add(category);
            await _db.SaveChangesAsync(cancellationToken);

            return ToCategoryDto(category);
        }

        public async Task<CategoryDto> UpdateCategoryAsync(Guid tenantId, Guid categoryId, UpdateCategoryRequestDto request, CancellationToken cancellationToken = default)
        {
            var category = await _db.AccountsPayableCategories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == categoryId, cancellationToken)
                ?? throw new KeyNotFoundException("Category not found.");

            var name = (request.Name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Category name is required.", nameof(request));

            if (!string.Equals(category.Name, name, StringComparison.Ordinal))
            {
                var nameTaken = await _db.AccountsPayableCategories
                    .IgnoreQueryFilters()
                    .AnyAsync(c => c.TenantId == tenantId && c.Id != categoryId && c.Name == name, cancellationToken);
                if (nameTaken)
                    throw new InvalidOperationException($"A category named '{name}' already exists.");
            }

            category.Name = name;
            category.Description = request.Description;
            category.Color = request.Color;
            category.IsActive = request.IsActive;

            await _db.SaveChangesAsync(cancellationToken);
            return ToCategoryDto(category);
        }

        public async Task DeactivateCategoryAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default)
        {
            var category = await _db.AccountsPayableCategories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == categoryId, cancellationToken)
                ?? throw new KeyNotFoundException("Category not found.");

            if (!category.IsActive) return;

            category.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ---------- Entries ----------

        public async Task<PagedResult<EntryDto>> ListEntriesAsync(Guid tenantId, EntryListQueryDto query, CancellationToken cancellationToken = default)
        {
            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize <= 0 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);

            var baseQuery = BuildEntriesQuery(tenantId, query);
            var totalCount = await baseQuery.CountAsync(cancellationToken);

            baseQuery = ApplySorting(baseQuery, query.SortBy, query.SortDir);

            var today = DateTime.UtcNow.Date;
            var items = await baseQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new EntryDto(
                    e.Id,
                    e.Description,
                    e.Amount,
                    e.DueDate,
                    e.CategoryId,
                    e.Category.Name,
                    e.Status,
                    e.PaidAt,
                    e.PaymentMethod,
                    e.SupplierName,
                    e.Notes,
                    e.CancelledAt,
                    e.CancellationReason,
                    e.Status == AccountsPayableStatus.Pending && e.DueDate.Date < today,
                    e.CreatedAt,
                    e.UpdatedAt))
                .ToListAsync(cancellationToken);

            return new PagedResult<EntryDto>(items, totalCount, page, pageSize);
        }

        public async Task<EntryDto?> GetEntryAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken = default)
        {
            var today = DateTime.UtcNow.Date;
            return await _db.AccountsPayableEntries
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == tenantId && e.Id == entryId)
                .Select(e => new EntryDto(
                    e.Id,
                    e.Description,
                    e.Amount,
                    e.DueDate,
                    e.CategoryId,
                    e.Category.Name,
                    e.Status,
                    e.PaidAt,
                    e.PaymentMethod,
                    e.SupplierName,
                    e.Notes,
                    e.CancelledAt,
                    e.CancellationReason,
                    e.Status == AccountsPayableStatus.Pending && e.DueDate.Date < today,
                    e.CreatedAt,
                    e.UpdatedAt))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<EntryDto> CreateEntryAsync(Guid tenantId, Guid createdByUserId, CreateEntryRequestDto request, CancellationToken cancellationToken = default)
        {
            ValidateEntryFields(request.Description, request.Amount);

            var category = await GetActiveCategoryAsync(tenantId, request.CategoryId, cancellationToken)
                ?? throw new KeyNotFoundException("Category not found or inactive.");

            var entry = new AccountsPayableEntry
            {
                TenantId = tenantId,
                Description = request.Description.Trim(),
                Amount = request.Amount,
                DueDate = ToUtc(request.DueDate),
                CategoryId = category.Id,
                Status = AccountsPayableStatus.Pending,
                PaymentMethod = request.PaymentMethod,
                SupplierName = string.IsNullOrWhiteSpace(request.SupplierName) ? null : request.SupplierName.Trim(),
                Notes = request.Notes,
                CreatedByUserId = createdByUserId
            };

            _db.AccountsPayableEntries.Add(entry);
            await _db.SaveChangesAsync(cancellationToken);

            return await GetEntryAsync(tenantId, entry.Id, cancellationToken)
                ?? throw new InvalidOperationException("Failed to read created entry.");
        }

        public async Task<EntryDto> UpdateEntryAsync(Guid tenantId, Guid entryId, UpdateEntryRequestDto request, CancellationToken cancellationToken = default)
        {
            ValidateEntryFields(request.Description, request.Amount);

            var entry = await _db.AccountsPayableEntries
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == entryId, cancellationToken)
                ?? throw new KeyNotFoundException("Entry not found.");

            if (entry.Status == AccountsPayableStatus.Cancelled)
                throw new InvalidOperationException("Cannot edit a cancelled entry.");

            var category = await GetActiveCategoryAsync(tenantId, request.CategoryId, cancellationToken)
                ?? throw new KeyNotFoundException("Category not found or inactive.");

            entry.Description = request.Description.Trim();
            entry.Amount = request.Amount;
            entry.DueDate = ToUtc(request.DueDate);
            entry.CategoryId = category.Id;
            entry.PaymentMethod = request.PaymentMethod;
            entry.SupplierName = string.IsNullOrWhiteSpace(request.SupplierName) ? null : request.SupplierName.Trim();
            entry.Notes = request.Notes;

            await _db.SaveChangesAsync(cancellationToken);

            return await GetEntryAsync(tenantId, entry.Id, cancellationToken)
                ?? throw new InvalidOperationException("Failed to read updated entry.");
        }

        public async Task<EntryDto> MarkEntryPaidAsync(Guid tenantId, Guid entryId, Guid paidByUserId, MarkPaidRequestDto request, CancellationToken cancellationToken = default)
        {
            var entry = await _db.AccountsPayableEntries
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == entryId, cancellationToken)
                ?? throw new KeyNotFoundException("Entry not found.");

            entry.MarkPaid(request.PaidAt, request.PaymentMethod);
            await _db.SaveChangesAsync(cancellationToken);

            await _glPosting.PostApPaymentAsync(
                tenantId, entry.Id, entry.Description,
                entry.Amount, request.PaidAt, paidByUserId, cancellationToken);

            return await GetEntryAsync(tenantId, entry.Id, cancellationToken)
                ?? throw new InvalidOperationException("Failed to read updated entry.");
        }

        public async Task<EntryDto> CancelEntryAsync(Guid tenantId, Guid entryId, CancelEntryRequestDto request, CancellationToken cancellationToken = default)
        {
            var entry = await _db.AccountsPayableEntries
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == entryId, cancellationToken)
                ?? throw new KeyNotFoundException("Entry not found.");

            entry.Cancel(request.Reason, DateTime.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);

            return await GetEntryAsync(tenantId, entry.Id, cancellationToken)
                ?? throw new InvalidOperationException("Failed to read updated entry.");
        }

        // ---------- Bulk ----------

        public async Task<BulkEntriesResponseDto> BulkCreateAsync(Guid tenantId, Guid createdByUserId, BulkEntriesRequestDto request, bool canManageCategories, CancellationToken cancellationToken = default)
        {
            if (request.Lines == null || request.Lines.Count == 0)
                throw new ArgumentException("At least one line is required.", nameof(request));
            if (request.Lines.Count > MaxBulkLines)
                throw new ArgumentException($"Bulk request exceeds the maximum of {MaxBulkLines} lines.", nameof(request));

            var existingCategories = await _db.AccountsPayableCategories
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId)
                .ToListAsync(cancellationToken);

            var byId = existingCategories.ToDictionary(c => c.Id);
            var byName = existingCategories
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var results = new List<BulkEntryResultDto>(request.Lines.Count);
            var toAdd = new List<AccountsPayableEntry>();
            var pendingNewCategories = new Dictionary<string, AccountsPayableCategory>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < request.Lines.Count; i++)
            {
                var line = request.Lines[i];
                var errors = new List<string>();

                if (string.IsNullOrWhiteSpace(line.Description))
                    errors.Add("Description is required.");
                if (line.Amount <= 0)
                    errors.Add("Amount must be greater than zero.");

                AccountsPayableCategory? category = null;

                if (line.CategoryId.HasValue)
                {
                    if (!byId.TryGetValue(line.CategoryId.Value, out category) || !category.IsActive)
                        errors.Add("Category not found or inactive.");
                }
                else if (!string.IsNullOrWhiteSpace(line.CategoryName))
                {
                    var name = line.CategoryName.Trim();
                    if (!byName.TryGetValue(name, out category))
                    {
                        if (!canManageCategories)
                        {
                            errors.Add($"Category '{name}' does not exist and you lack permission to create categories.");
                        }
                        else if (!pendingNewCategories.TryGetValue(name, out category))
                        {
                            category = new AccountsPayableCategory
                            {
                                TenantId = tenantId,
                                Name = name
                            };
                            pendingNewCategories[name] = category;
                            byName[name] = category;
                        }
                    }
                    else if (!category.IsActive)
                    {
                        errors.Add($"Category '{name}' is inactive.");
                        category = null;
                    }
                }
                else
                {
                    errors.Add("Either categoryId or categoryName must be provided.");
                }

                if (errors.Count > 0 || category == null)
                {
                    results.Add(new BulkEntryResultDto(i, false, null, errors));
                    continue;
                }

                var entry = new AccountsPayableEntry
                {
                    TenantId = tenantId,
                    Description = line.Description.Trim(),
                    Amount = line.Amount,
                    DueDate = ToUtc(line.DueDate),
                    Category = category,
                    Status = AccountsPayableStatus.Pending,
                    PaymentMethod = line.PaymentMethod,
                    SupplierName = string.IsNullOrWhiteSpace(line.SupplierName) ? null : line.SupplierName.Trim(),
                    Notes = line.Notes,
                    CreatedByUserId = createdByUserId
                };
                toAdd.Add(entry);
                results.Add(new BulkEntryResultDto(i, true, entry.Id, Array.Empty<string>()));
            }

            if (pendingNewCategories.Count > 0)
                _db.AccountsPayableCategories.AddRange(pendingNewCategories.Values);
            if (toAdd.Count > 0)
                _db.AccountsPayableEntries.AddRange(toAdd);

            if (toAdd.Count > 0 || pendingNewCategories.Count > 0)
                await _db.SaveChangesAsync(cancellationToken);

            return new BulkEntriesResponseDto(
                request.Lines.Count,
                results.Count(r => r.Success),
                results.Count(r => !r.Success),
                results);
        }

        // ---------- Reports ----------

        public async Task<SummaryResponseDto> GetSummaryAsync(Guid tenantId, SummaryQueryDto query, CancellationToken cancellationToken = default)
        {
            var baseQuery = _db.AccountsPayableEntries
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == tenantId);

            var from = ToUtc(query.From);
            var to = ToUtc(query.To);
            if (from.HasValue) baseQuery = baseQuery.Where(e => e.DueDate >= from.Value);
            if (to.HasValue) baseQuery = baseQuery.Where(e => e.DueDate <= to.Value);

            var statusGroups = await baseQuery
                .GroupBy(e => e.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Total = g.Sum(x => x.Amount),
                    Count = g.Count()
                })
                .ToListAsync(cancellationToken);

            decimal totalPending = 0, totalPaid = 0, totalCancelled = 0;
            int countPending = 0, countPaid = 0, countCancelled = 0;

            foreach (var s in statusGroups)
            {
                switch (s.Status)
                {
                    case AccountsPayableStatus.Pending: totalPending = s.Total; countPending = s.Count; break;
                    case AccountsPayableStatus.Paid: totalPaid = s.Total; countPaid = s.Count; break;
                    case AccountsPayableStatus.Cancelled: totalCancelled = s.Total; countCancelled = s.Count; break;
                }
            }

            var perCategory = await baseQuery
                .GroupBy(e => new { e.CategoryId, e.Category.Name })
                .Select(g => new CategoryTotalsDto(
                    g.Key.CategoryId,
                    g.Key.Name,
                    g.Where(x => x.Status == AccountsPayableStatus.Pending).Sum(x => (decimal?)x.Amount) ?? 0m,
                    g.Where(x => x.Status == AccountsPayableStatus.Paid).Sum(x => (decimal?)x.Amount) ?? 0m,
                    g.Where(x => x.Status == AccountsPayableStatus.Cancelled).Sum(x => (decimal?)x.Amount) ?? 0m))
                .ToListAsync(cancellationToken);

            return new SummaryResponseDto(
                query.From,
                query.To,
                totalPending,
                totalPaid,
                totalCancelled,
                countPending,
                countPaid,
                countCancelled,
                perCategory);
        }

        public async Task<byte[]> ExportEntriesCsvAsync(Guid tenantId, EntryListQueryDto query, CancellationToken cancellationToken = default)
        {
            var baseQuery = ApplySorting(BuildEntriesQuery(tenantId, query), query.SortBy, query.SortDir);

            var today = DateTime.UtcNow.Date;
            var items = await baseQuery
                .Select(e => new EntryDto(
                    e.Id,
                    e.Description,
                    e.Amount,
                    e.DueDate,
                    e.CategoryId,
                    e.Category.Name,
                    e.Status,
                    e.PaidAt,
                    e.PaymentMethod,
                    e.SupplierName,
                    e.Notes,
                    e.CancelledAt,
                    e.CancellationReason,
                    e.Status == AccountsPayableStatus.Pending && e.DueDate.Date < today,
                    e.CreatedAt,
                    e.UpdatedAt))
                .ToListAsync(cancellationToken);

            return CsvWriter.WriteEntries(items);
        }

        // ---------- Helpers ----------

        private IQueryable<AccountsPayableEntry> BuildEntriesQuery(Guid tenantId, EntryListQueryDto query)
        {
            var q = _db.AccountsPayableEntries
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == tenantId);

            var from = ToUtc(query.From);
            var to = ToUtc(query.To);
            if (from.HasValue) q = q.Where(e => e.DueDate >= from.Value);
            if (to.HasValue) q = q.Where(e => e.DueDate <= to.Value);
            if (query.Status.HasValue) q = q.Where(e => e.Status == query.Status.Value);
            if (query.CategoryId.HasValue) q = q.Where(e => e.CategoryId == query.CategoryId.Value);
            if (query.PaymentMethod.HasValue) q = q.Where(e => e.PaymentMethod == query.PaymentMethod.Value);
            if (query.MinAmount.HasValue) q = q.Where(e => e.Amount >= query.MinAmount.Value);
            if (query.MaxAmount.HasValue) q = q.Where(e => e.Amount <= query.MaxAmount.Value);

            if (!string.IsNullOrWhiteSpace(query.SupplierName))
            {
                var s = query.SupplierName.Trim();
                q = q.Where(e => e.SupplierName != null && EF.Functions.ILike(e.SupplierName, $"%{s}%"));
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                q = q.Where(e =>
                    EF.Functions.ILike(e.Description, $"%{s}%")
                    || (e.Notes != null && EF.Functions.ILike(e.Notes, $"%{s}%")));
            }

            return q;
        }

        private static IQueryable<AccountsPayableEntry> ApplySorting(IQueryable<AccountsPayableEntry> q, string? sortBy, string? sortDir)
        {
            var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            return (sortBy?.ToLowerInvariant()) switch
            {
                "amount" => desc ? q.OrderByDescending(e => e.Amount) : q.OrderBy(e => e.Amount),
                "description" => desc ? q.OrderByDescending(e => e.Description) : q.OrderBy(e => e.Description),
                "status" => desc ? q.OrderByDescending(e => e.Status) : q.OrderBy(e => e.Status),
                "createdat" => desc ? q.OrderByDescending(e => e.CreatedAt) : q.OrderBy(e => e.CreatedAt),
                "duedate" or null or "" => desc ? q.OrderByDescending(e => e.DueDate) : q.OrderBy(e => e.DueDate),
                _ => q.OrderBy(e => e.DueDate)
            };
        }

        private static DateTime ToUtc(DateTime value) =>
            value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };

        private static DateTime? ToUtc(DateTime? value) => value.HasValue ? ToUtc(value.Value) : null;

        private static void ValidateEntryFields(string description, decimal amount)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Description is required.");
            if (amount <= 0)
                throw new ArgumentException("Amount must be greater than zero.");
        }

        private async Task<AccountsPayableCategory?> GetActiveCategoryAsync(Guid tenantId, Guid categoryId, CancellationToken ct)
        {
            return await _db.AccountsPayableCategories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == categoryId && c.IsActive, ct);
        }

        private static CategoryDto ToCategoryDto(AccountsPayableCategory c) =>
            new(c.Id, c.Name, c.Description, c.Color, c.IsActive, c.CreatedAt, c.UpdatedAt);
    }
}

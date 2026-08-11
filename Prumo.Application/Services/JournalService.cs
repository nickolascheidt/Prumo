using Microsoft.EntityFrameworkCore;
using Prumo.Application.DTOs.Finance;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data;

namespace Prumo.Application.Services
{
    public class JournalService : IJournalService
    {
        private const int MaxPageSize = 100;

        private readonly ApplicationDbContext _db;

        public JournalService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<PagedResult<JournalEntryListItemDto>> ListEntriesAsync(
            Guid tenantId, JournalEntryQueryDto query, CancellationToken ct = default)
        {
            var q = _db.JournalEntries
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == tenantId);

            if (query.From.HasValue) q = q.Where(e => e.Date >= DateTime.SpecifyKind(query.From.Value, DateTimeKind.Utc));
            if (query.To.HasValue)   q = q.Where(e => e.Date <= DateTime.SpecifyKind(query.To.Value, DateTimeKind.Utc));
            if (!string.IsNullOrEmpty(query.SourceModule))
                q = q.Where(e => e.SourceModule == query.SourceModule);

            var total    = await q.CountAsync(ct);
            var pageSize = Math.Min(Math.Max(query.PageSize, 1), MaxPageSize);
            var page     = Math.Max(query.Page, 1);

            var items = await q
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new JournalEntryListItemDto(
                    e.Id, e.Date, e.Description, e.SourceModule,
                    e.Lines.Sum(l => (int)l.EntryType == 1 ? l.Amount : 0m),
                    e.Lines.Count,
                    e.CreatedAt
                ))
                .ToListAsync(ct);

            return new PagedResult<JournalEntryListItemDto>(items, total, page, pageSize);
        }

        public async Task<JournalEntryDto?> GetEntryAsync(
            Guid tenantId, Guid entryId, CancellationToken ct = default)
        {
            var entry = await _db.JournalEntries
                .IgnoreQueryFilters()
                .Include(e => e.Lines)
                    .ThenInclude(l => l.Account)
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == entryId, ct);

            return entry == null ? null : ToDto(entry);
        }

        public async Task<JournalEntryDto> CreateJournalEntryAsync(
            Guid tenantId, Guid userId, CreateJournalEntryRequestDto request,
            string? sourceModule = null, Guid? sourceDocumentId = null,
            CancellationToken ct = default)
        {
            var description = (request.Description ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(description))
                throw new ArgumentException("Description is required.");
            if (request.Lines == null || request.Lines.Count < 2)
                throw new ArgumentException("A journal entry must have at least 2 lines.");
            if (request.Lines.Any(l => l.Amount <= 0))
                throw new ArgumentException("All line amounts must be greater than zero.");

            var accountIds = request.Lines.Select(l => l.AccountId).Distinct().ToList();
            var accounts = await _db.Accounts
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == tenantId && accountIds.Contains(a.Id))
                .ToListAsync(ct);

            foreach (var lineDto in request.Lines)
            {
                var account = accounts.FirstOrDefault(a => a.Id == lineDto.AccountId)
                    ?? throw new KeyNotFoundException($"Account {lineDto.AccountId} not found.");
                if (!account.IsAnalytic)
                    throw new InvalidOperationException($"Account '{account.Code}' is synthetic and cannot be posted to.");
                if (!account.IsActive)
                    throw new InvalidOperationException($"Account '{account.Code}' is inactive.");
            }

            var lines = request.Lines
                .Select(l => new JournalLine
                {
                    AccountId = l.AccountId,
                    EntryType = (JournalEntryType)l.EntryType,
                    Amount    = l.Amount
                })
                .ToList();

            JournalEntry.ValidateBalance(lines);

            var entry = new JournalEntry
            {
                TenantId         = tenantId,
                Date             = DateTime.SpecifyKind(request.Date, DateTimeKind.Utc),
                Description      = description,
                SourceModule     = sourceModule,
                SourceDocumentId = sourceDocumentId,
                CreatedByUserId  = userId
            };

            foreach (var line in lines)
            {
                line.JournalEntryId = entry.Id;
                entry.Lines.Add(line);
            }

            _db.JournalEntries.Add(entry);
            await _db.SaveChangesAsync(ct);

            return await GetEntryAsync(tenantId, entry.Id, ct)
                ?? throw new InvalidOperationException("Failed to read created entry.");
        }

        public async Task<AccountStatementDto> GetAccountStatementAsync(
            Guid tenantId, Guid accountId,
            DateTime? from = null, DateTime? to = null,
            CancellationToken ct = default)
        {
            var account = await _db.Accounts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == accountId, ct)
                ?? throw new KeyNotFoundException("Account not found.");

            var query = _db.JournalLines
                .Include(l => l.JournalEntry)
                .Where(l => l.AccountId == accountId && l.JournalEntry.TenantId == tenantId);

            if (from.HasValue) query = query.Where(l => l.JournalEntry.Date >= DateTime.SpecifyKind(from.Value, DateTimeKind.Utc));
            if (to.HasValue)   query = query.Where(l => l.JournalEntry.Date <= DateTime.SpecifyKind(to.Value, DateTimeKind.Utc));

            var rawLines = await query
                .OrderBy(l => l.JournalEntry.Date)
                .ThenBy(l => l.JournalEntry.CreatedAt)
                .Select(l => new
                {
                    JournalEntryId = l.JournalEntry.Id,
                    l.JournalEntry.Date,
                    l.JournalEntry.Description,
                    l.EntryType,
                    l.Amount
                })
                .ToListAsync(ct);

            decimal runningBalance = 0m;
            var statementLines = rawLines.Select(l =>
            {
                runningBalance += l.EntryType == JournalEntryType.Debit ? l.Amount : -l.Amount;
                return new AccountStatementLineDto(
                    l.JournalEntryId, l.Date, l.Description,
                    (int)l.EntryType, l.Amount, runningBalance);
            }).ToList();

            return new AccountStatementDto(
                accountId, account.Code, account.Name,
                from, to,
                OpeningBalance: 0m,
                statementLines,
                ClosingBalance: runningBalance
            );
        }

        private static JournalEntryDto ToDto(JournalEntry e) => new(
            e.Id, e.TenantId, e.Date, e.Description,
            e.SourceModule, e.SourceDocumentId, e.CreatedByUserId, e.CreatedAt,
            e.Lines.Select(l => new JournalLineDto(
                l.Id, l.AccountId,
                l.Account?.Code ?? string.Empty,
                l.Account?.Name ?? string.Empty,
                (int)l.EntryType, l.Amount
            )).ToList()
        );
    }
}

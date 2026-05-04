using Microsoft.EntityFrameworkCore;
using SaaS_BasePlatform.Application.DTOs.Finance;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;
using SaaS_BasePlatform.Infrastructure.Data;

namespace SaaS_BasePlatform.Application.Services
{
    public class AccountService : IAccountService
    {
        private readonly ApplicationDbContext _db;

        public AccountService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<AccountDto>> ListAccountsAsync(
            Guid tenantId, bool includeInactive = false, CancellationToken ct = default)
        {
            var query = _db.Accounts
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == tenantId);

            if (!includeInactive)
                query = query.Where(a => a.IsActive);

            return await query
                .OrderBy(a => a.Code)
                .Select(a => ToDto(a))
                .ToListAsync(ct);
        }

        public async Task<AccountDto?> GetAccountAsync(
            Guid tenantId, Guid accountId, CancellationToken ct = default)
        {
            var account = await _db.Accounts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == accountId, ct);
            return account == null ? null : ToDto(account);
        }

        public async Task<AccountDto> CreateAccountAsync(
            Guid tenantId, CreateAccountRequestDto request, CancellationToken ct = default)
        {
            var code = (request.Code ?? string.Empty).Trim();
            var name = (request.Name ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(code)) throw new ArgumentException("Account code is required.");
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Account name is required.");
            if (!Enum.IsDefined(typeof(AccountType), request.Type))
                throw new ArgumentException($"Invalid account type: {request.Type}.");

            var codeTaken = await _db.Accounts
                .IgnoreQueryFilters()
                .AnyAsync(a => a.TenantId == tenantId && a.Code == code, ct);
            if (codeTaken)
                throw new InvalidOperationException($"An account with code '{code}' already exists.");

            if (request.ParentId.HasValue)
                await ValidateParentAsync(tenantId, request.ParentId.Value, ct);

            var account = new Account
            {
                TenantId = tenantId,
                Code = code,
                Name = name,
                Type = (AccountType)request.Type,
                IsAnalytic = request.IsAnalytic,
                ParentId = request.ParentId
            };

            _db.Accounts.Add(account);
            await _db.SaveChangesAsync(ct);
            return ToDto(account);
        }

        public async Task<AccountDto> UpdateAccountAsync(
            Guid tenantId, Guid accountId, UpdateAccountRequestDto request, CancellationToken ct = default)
        {
            var account = await _db.Accounts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == accountId, ct)
                ?? throw new KeyNotFoundException("Account not found.");

            var code = (request.Code ?? string.Empty).Trim();
            var name = (request.Name ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(code)) throw new ArgumentException("Account code is required.");
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Account name is required.");
            if (!Enum.IsDefined(typeof(AccountType), request.Type))
                throw new ArgumentException($"Invalid account type: {request.Type}.");

            if (!string.Equals(account.Code, code, StringComparison.Ordinal))
            {
                var codeTaken = await _db.Accounts
                    .IgnoreQueryFilters()
                    .AnyAsync(a => a.TenantId == tenantId && a.Id != accountId && a.Code == code, ct);
                if (codeTaken)
                    throw new InvalidOperationException($"An account with code '{code}' already exists.");
            }

            if (request.ParentId.HasValue && request.ParentId != account.ParentId)
                await ValidateParentAsync(tenantId, request.ParentId.Value, ct);

            account.Code = code;
            account.Name = name;
            account.Type = (AccountType)request.Type;
            account.IsAnalytic = request.IsAnalytic;
            account.ParentId = request.ParentId;
            account.IsActive = request.IsActive;

            await _db.SaveChangesAsync(ct);
            return ToDto(account);
        }

        public async Task DeactivateAccountAsync(
            Guid tenantId, Guid accountId, CancellationToken ct = default)
        {
            var account = await _db.Accounts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == accountId, ct)
                ?? throw new KeyNotFoundException("Account not found.");

            var hasLines = await _db.JournalLines
                .AnyAsync(l => l.AccountId == accountId, ct);
            if (hasLines)
                throw new InvalidOperationException("Cannot deactivate an account that has journal entries.");

            account.IsActive = false;
            await _db.SaveChangesAsync(ct);
        }

        private async Task ValidateParentAsync(Guid tenantId, Guid parentId, CancellationToken ct)
        {
            var parent = await _db.Accounts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == parentId && a.IsActive, ct)
                ?? throw new KeyNotFoundException("Parent account not found.");
            if (parent.IsAnalytic)
                throw new InvalidOperationException("Cannot use an analytic account as parent.");
        }

        private static AccountDto ToDto(Account a) => new(
            a.Id, a.TenantId, a.Code, a.Name,
            (int)a.Type, a.Type.ToString(),
            a.IsAnalytic, a.ParentId,
            a.IsActive, a.CreatedAt, a.UpdatedAt
        );
    }
}

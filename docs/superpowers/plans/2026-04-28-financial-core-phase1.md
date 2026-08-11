# Financial Core Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Chart of Accounts and General Ledger to the SaaS platform, with automatic GL posting when an AP entry is marked paid.

**Architecture:** Clean Architecture — new Domain entities (Account, JournalEntry, JournalLine, TenantGlSettings) flow through Application services (IAccountService, IJournalService, IGlPostingService, ITenantGlSettingsService) and expose REST controllers. The AP service gains a single injection of IGlPostingService and calls it inside MarkEntryPaidAsync. A new ChartOfAccountsSeeder runs alongside TenantBootstrapSeeder to seed 18 standard accounts plus default GL settings per tenant.

**Tech Stack:** .NET 10, EF Core + PostgreSQL, xUnit + NSubstitute (tests), FluentValidation (already wired), JWT auth (existing pattern)

---

## File Map

### New — Domain
- `Prumo.Domain/Enums/AccountType.cs`
- `Prumo.Domain/Enums/JournalEntryType.cs`
- `Prumo.Domain/Entities/Account.cs`
- `Prumo.Domain/Entities/JournalEntry.cs` — includes static `ValidateBalance()`
- `Prumo.Domain/Entities/JournalLine.cs`
- `Prumo.Domain/Entities/TenantGlSettings.cs`

### New — Application DTOs
- `Prumo.Application/DTOs/Finance/AccountDtos.cs`
- `Prumo.Application/DTOs/Finance/JournalDtos.cs`
- `Prumo.Application/DTOs/Finance/GlSettingsDtos.cs`

### New — Application Services
- `Prumo.Application/Services/IAccountService.cs`
- `Prumo.Application/Services/AccountService.cs`
- `Prumo.Application/Services/IJournalService.cs`
- `Prumo.Application/Services/JournalService.cs`
- `Prumo.Application/Services/ITenantGlSettingsService.cs`
- `Prumo.Application/Services/TenantGlSettingsService.cs`
- `Prumo.Application/Services/IGlPostingService.cs`
- `Prumo.Application/Services/GlPostingService.cs`

### New — Infrastructure
- `Prumo.Infrastructure/Data/Configurations/AccountConfiguration.cs`
- `Prumo.Infrastructure/Data/Configurations/JournalEntryConfiguration.cs`
- `Prumo.Infrastructure/Data/Configurations/JournalLineConfiguration.cs`
- `Prumo.Infrastructure/Data/Configurations/TenantGlSettingsConfiguration.cs`
- `Prumo.Infrastructure/Data/Seeders/ChartOfAccountsSeeder.cs`

### New — API Controllers
- `Prumo.Api/Controllers/ChartOfAccountsController.cs`
- `Prumo.Api/Controllers/GeneralLedgerController.cs`

### New — Tests
- `Prumo.Tests/Domain/JournalEntryTests.cs`
- `Prumo.Tests/Services/GlPostingServiceTests.cs`

### Modified
- `Prumo.Infrastructure/Data/ApplicationDbContext.cs` — add 4 DbSets
- `Prumo.Infrastructure/Data/DbInitializer.cs` — call ChartOfAccountsSeeder
- `Prumo.Api/Configuration/DependencyInjectionConfiguration.cs` — register 4 new services
- `Prumo.Application/Services/AccountsPayableService.cs` — inject IGlPostingService, call PostApPaymentAsync
- `Prumo.Application/Services/IAccountsPayableService.cs` — add userId param to MarkEntryPaidAsync
- `Prumo.Api/Controllers/AccountsPayableEntriesController.cs` — pass CurrentUserId to MarkEntryPaidAsync

---

## Task 1: Create Feature Branch

**Files:** none

- [ ] **Step 1: Create and switch to feature branch**

```bash
git checkout -b feature/financial-core-phase1
```

Expected output: `Switched to a new branch 'feature/financial-core-phase1'`

---

## Task 2: Domain Enums

**Files:**
- Create: `Prumo.Domain/Enums/AccountType.cs`
- Create: `Prumo.Domain/Enums/JournalEntryType.cs`

- [ ] **Step 1: Create AccountType.cs**

```csharp
namespace Prumo.Domain.Enums
{
    public enum AccountType
    {
        Asset = 1,
        Liability = 2,
        Equity = 3,
        Revenue = 4,
        Expense = 5
    }
}
```

- [ ] **Step 2: Create JournalEntryType.cs**

```csharp
namespace Prumo.Domain.Enums
{
    public enum JournalEntryType
    {
        Debit = 1,
        Credit = 2
    }
}
```

- [ ] **Step 3: Build to confirm no errors**

```bash
dotnet build Prumo.Domain
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 4: Commit**

```bash
git add Prumo.Domain/Enums/AccountType.cs Prumo.Domain/Enums/JournalEntryType.cs
git commit -m "feat: add AccountType and JournalEntryType enums"
```

---

## Task 3: Domain Entities + Tests

**Files:**
- Create: `Prumo.Domain/Entities/Account.cs`
- Create: `Prumo.Domain/Entities/JournalEntry.cs`
- Create: `Prumo.Domain/Entities/JournalLine.cs`
- Create: `Prumo.Domain/Entities/TenantGlSettings.cs`
- Create: `Prumo.Tests/Domain/JournalEntryTests.cs`

- [ ] **Step 1: Write failing tests for JournalEntry.ValidateBalance**

Create `Prumo.Tests/Domain/JournalEntryTests.cs`:

```csharp
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;

namespace Prumo.Tests.Domain;

public class JournalEntryTests
{
    [Fact]
    public void ValidateBalance_WhenDebitsEqualCredits_DoesNotThrow()
    {
        var lines = new List<JournalLine>
        {
            new() { EntryType = JournalEntryType.Debit,  Amount = 100m },
            new() { EntryType = JournalEntryType.Credit, Amount = 100m }
        };

        var ex = Record.Exception(() => JournalEntry.ValidateBalance(lines));

        Assert.Null(ex);
    }

    [Fact]
    public void ValidateBalance_WhenUnbalanced_ThrowsInvalidOperationException()
    {
        var lines = new List<JournalLine>
        {
            new() { EntryType = JournalEntryType.Debit,  Amount = 100m },
            new() { EntryType = JournalEntryType.Credit, Amount = 90m }
        };

        Assert.Throws<InvalidOperationException>(() => JournalEntry.ValidateBalance(lines));
    }

    [Fact]
    public void ValidateBalance_WithOnlyOneLine_ThrowsInvalidOperationException()
    {
        var lines = new List<JournalLine>
        {
            new() { EntryType = JournalEntryType.Debit, Amount = 100m }
        };

        Assert.Throws<InvalidOperationException>(() => JournalEntry.ValidateBalance(lines));
    }

    [Fact]
    public void ValidateBalance_WithMultipleBalancedLines_DoesNotThrow()
    {
        var lines = new List<JournalLine>
        {
            new() { EntryType = JournalEntryType.Debit,  Amount = 60m },
            new() { EntryType = JournalEntryType.Debit,  Amount = 40m },
            new() { EntryType = JournalEntryType.Credit, Amount = 100m }
        };

        var ex = Record.Exception(() => JournalEntry.ValidateBalance(lines));

        Assert.Null(ex);
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail (JournalEntry does not exist yet)**

```bash
dotnet test --filter "FullyQualifiedName~JournalEntryTests"
```

Expected: Build error — `JournalEntry` and `JournalLine` types not found.

- [ ] **Step 3: Create JournalLine.cs**

```csharp
using Prumo.Domain.Enums;

namespace Prumo.Domain.Entities
{
    public class JournalLine
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid JournalEntryId { get; set; }
        public JournalEntry JournalEntry { get; set; } = null!;
        public Guid AccountId { get; set; }
        public Account Account { get; set; } = null!;
        public JournalEntryType EntryType { get; set; }
        public decimal Amount { get; set; }
    }
}
```

- [ ] **Step 4: Create JournalEntry.cs**

```csharp
using Prumo.Domain.Common;
using Prumo.Domain.Enums;

namespace Prumo.Domain.Entities
{
    public class JournalEntry : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? SourceModule { get; set; }
        public Guid? SourceDocumentId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();

        public static void ValidateBalance(IReadOnlyList<JournalLine> lines)
        {
            if (lines.Count < 2)
                throw new InvalidOperationException("A journal entry must have at least 2 lines.");

            var totalDebits  = lines.Where(l => l.EntryType == JournalEntryType.Debit).Sum(l => l.Amount);
            var totalCredits = lines.Where(l => l.EntryType == JournalEntryType.Credit).Sum(l => l.Amount);

            if (totalDebits != totalCredits)
                throw new InvalidOperationException(
                    $"Journal entry is unbalanced: debits {totalDebits:F2} ≠ credits {totalCredits:F2}.");
        }
    }
}
```

- [ ] **Step 5: Create Account.cs**

```csharp
using Prumo.Domain.Common;
using Prumo.Domain.Enums;

namespace Prumo.Domain.Entities
{
    public class Account : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AccountType Type { get; set; }
        public bool IsAnalytic { get; set; }
        public Guid? ParentId { get; set; }
        public Account? Parent { get; set; }
        public ICollection<Account> Children { get; set; } = new List<Account>();
        public ICollection<JournalLine> JournalLines { get; set; } = new List<JournalLine>();
    }
}
```

- [ ] **Step 6: Create TenantGlSettings.cs**

```csharp
namespace Prumo.Domain.Entities
{
    public class TenantGlSettings
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        public Guid? DefaultCashAccountId { get; set; }
        public Account? DefaultCashAccount { get; set; }
        public Guid? DefaultAccountsPayableAccountId { get; set; }
        public Account? DefaultAccountsPayableAccount { get; set; }
    }
}
```

- [ ] **Step 7: Run tests — expect pass**

```bash
dotnet test --filter "FullyQualifiedName~JournalEntryTests"
```

Expected: 4 tests passing.

- [ ] **Step 8: Commit**

```bash
git add Prumo.Domain/Entities/Account.cs \
        Prumo.Domain/Entities/JournalEntry.cs \
        Prumo.Domain/Entities/JournalLine.cs \
        Prumo.Domain/Entities/TenantGlSettings.cs \
        Prumo.Tests/Domain/JournalEntryTests.cs
git commit -m "feat: add GL domain entities with balance validation"
```

---

## Task 4: Application DTOs

**Files:**
- Create: `Prumo.Application/DTOs/Finance/AccountDtos.cs`
- Create: `Prumo.Application/DTOs/Finance/JournalDtos.cs`
- Create: `Prumo.Application/DTOs/Finance/GlSettingsDtos.cs`

- [ ] **Step 1: Create AccountDtos.cs**

```csharp
namespace Prumo.Application.DTOs.Finance
{
    public record AccountDto(
        Guid Id,
        Guid TenantId,
        string Code,
        string Name,
        int Type,
        string TypeName,
        bool IsAnalytic,
        Guid? ParentId,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    public record CreateAccountRequestDto(
        string Code,
        string Name,
        int Type,
        bool IsAnalytic,
        Guid? ParentId
    );

    public record UpdateAccountRequestDto(
        string Code,
        string Name,
        int Type,
        bool IsAnalytic,
        Guid? ParentId,
        bool IsActive
    );
}
```

- [ ] **Step 2: Create JournalDtos.cs**

```csharp
using Prumo.Domain.Common;

namespace Prumo.Application.DTOs.Finance
{
    public record JournalLineDto(
        Guid Id,
        Guid AccountId,
        string AccountCode,
        string AccountName,
        int EntryType,
        decimal Amount
    );

    public record JournalEntryDto(
        Guid Id,
        Guid TenantId,
        DateTime Date,
        string Description,
        string? SourceModule,
        Guid? SourceDocumentId,
        Guid CreatedByUserId,
        DateTime CreatedAt,
        IReadOnlyList<JournalLineDto> Lines
    );

    public record JournalEntryListItemDto(
        Guid Id,
        DateTime Date,
        string Description,
        string? SourceModule,
        decimal TotalAmount,
        int LineCount,
        DateTime CreatedAt
    );

    public record CreateJournalLineDto(
        Guid AccountId,
        int EntryType,
        decimal Amount
    );

    public record CreateJournalEntryRequestDto(
        DateTime Date,
        string Description,
        IReadOnlyList<CreateJournalLineDto> Lines
    );

    public record JournalEntryQueryDto(
        DateTime? From = null,
        DateTime? To = null,
        string? SourceModule = null,
        int Page = 1,
        int PageSize = 20
    );

    public record AccountStatementLineDto(
        Guid JournalEntryId,
        DateTime Date,
        string Description,
        int EntryType,
        decimal Amount,
        decimal RunningBalance
    );

    public record AccountStatementDto(
        Guid AccountId,
        string AccountCode,
        string AccountName,
        DateTime? From,
        DateTime? To,
        decimal OpeningBalance,
        IReadOnlyList<AccountStatementLineDto> Lines,
        decimal ClosingBalance
    );
}
```

- [ ] **Step 3: Create GlSettingsDtos.cs**

```csharp
namespace Prumo.Application.DTOs.Finance
{
    public record TenantGlSettingsDto(
        Guid TenantId,
        Guid? DefaultCashAccountId,
        string? DefaultCashAccountCode,
        Guid? DefaultAccountsPayableAccountId,
        string? DefaultAccountsPayableAccountCode
    );

    public record UpdateTenantGlSettingsDto(
        Guid? DefaultCashAccountId,
        Guid? DefaultAccountsPayableAccountId
    );
}
```

- [ ] **Step 4: Build**

```bash
dotnet build Prumo.Application
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 5: Commit**

```bash
git add Prumo.Application/DTOs/Finance/
git commit -m "feat: add Finance DTOs (accounts, journal, GL settings)"
```

---

## Task 5: Account Service

**Files:**
- Create: `Prumo.Application/Services/IAccountService.cs`
- Create: `Prumo.Application/Services/AccountService.cs`

- [ ] **Step 1: Create IAccountService.cs**

```csharp
using Prumo.Application.DTOs.Finance;

namespace Prumo.Application.Services
{
    public interface IAccountService
    {
        Task<IReadOnlyList<AccountDto>> ListAccountsAsync(Guid tenantId, bool includeInactive = false, CancellationToken ct = default);
        Task<AccountDto?> GetAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct = default);
        Task<AccountDto> CreateAccountAsync(Guid tenantId, CreateAccountRequestDto request, CancellationToken ct = default);
        Task<AccountDto> UpdateAccountAsync(Guid tenantId, Guid accountId, UpdateAccountRequestDto request, CancellationToken ct = default);
        Task DeactivateAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct = default);
    }
}
```

- [ ] **Step 2: Create AccountService.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Prumo.Application.DTOs.Finance;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data;

namespace Prumo.Application.Services
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
```

- [ ] **Step 3: Build**

```bash
dotnet build Prumo.Application
```

Expected: Build succeeded. (Note: `_db.Accounts` and `_db.JournalLines` will cause errors until Task 8 adds DbSets — that is expected at this point. If you see only those two missing-member errors, continue.)

> **Note:** If the missing-DbSet errors block further builds, skip ahead to Task 9 Step 5 (add DbSets to ApplicationDbContext) and come back to finish here.

- [ ] **Step 4: Commit**

```bash
git add Prumo.Application/Services/IAccountService.cs \
        Prumo.Application/Services/AccountService.cs
git commit -m "feat: add IAccountService and AccountService"
```

---

## Task 6: Journal Service

**Files:**
- Create: `Prumo.Application/Services/IJournalService.cs`
- Create: `Prumo.Application/Services/JournalService.cs`

- [ ] **Step 1: Create IJournalService.cs**

```csharp
using Prumo.Application.DTOs.Finance;
using Prumo.Domain.Common;

namespace Prumo.Application.Services
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
```

- [ ] **Step 2: Create JournalService.cs**

```csharp
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

            if (query.From.HasValue) q = q.Where(e => e.Date >= query.From.Value);
            if (query.To.HasValue)   q = q.Where(e => e.Date <= query.To.Value);
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

            if (from.HasValue) query = query.Where(l => l.JournalEntry.Date >= from.Value);
            if (to.HasValue)   query = query.Where(l => l.JournalEntry.Date <= to.Value);

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
```

- [ ] **Step 3: Commit**

```bash
git add Prumo.Application/Services/IJournalService.cs \
        Prumo.Application/Services/JournalService.cs
git commit -m "feat: add IJournalService and JournalService with balance validation"
```

---

## Task 7: GL Settings Service

**Files:**
- Create: `Prumo.Application/Services/ITenantGlSettingsService.cs`
- Create: `Prumo.Application/Services/TenantGlSettingsService.cs`

- [ ] **Step 1: Create ITenantGlSettingsService.cs**

```csharp
using Prumo.Application.DTOs.Finance;

namespace Prumo.Application.Services
{
    public interface ITenantGlSettingsService
    {
        Task<TenantGlSettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken ct = default);
        Task<TenantGlSettingsDto> UpdateSettingsAsync(Guid tenantId, UpdateTenantGlSettingsDto request, CancellationToken ct = default);
    }
}
```

- [ ] **Step 2: Create TenantGlSettingsService.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Prumo.Application.DTOs.Finance;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;

namespace Prumo.Application.Services
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
```

- [ ] **Step 3: Commit**

```bash
git add Prumo.Application/Services/ITenantGlSettingsService.cs \
        Prumo.Application/Services/TenantGlSettingsService.cs
git commit -m "feat: add ITenantGlSettingsService and TenantGlSettingsService"
```

---

## Task 8: GL Posting Service + Tests

**Files:**
- Create: `Prumo.Application/Services/IGlPostingService.cs`
- Create: `Prumo.Application/Services/GlPostingService.cs`
- Create: `Prumo.Tests/Services/GlPostingServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Prumo.Tests/Services/GlPostingServiceTests.cs`:

```csharp
using NSubstitute;
using Prumo.Application.DTOs.Finance;
using Prumo.Application.Services;
using Prumo.Domain.Enums;

namespace Prumo.Tests.Services;

public class GlPostingServiceTests
{
    private readonly ITenantGlSettingsService _settings = Substitute.For<ITenantGlSettingsService>();
    private readonly IJournalService _journal = Substitute.For<IJournalService>();
    private readonly GlPostingService _sut;

    public GlPostingServiceTests()
    {
        _sut = new GlPostingService(_settings, _journal);
    }

    [Fact]
    public async Task PostApPaymentAsync_WhenCashAccountIsNull_SkipsPosting()
    {
        var tenantId = Guid.NewGuid();
        _settings.GetSettingsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantGlSettingsDto(tenantId, null, null, Guid.NewGuid(), "2.1.1"));

        await _sut.PostApPaymentAsync(tenantId, Guid.NewGuid(), "Invoice #1", 100m, DateTime.UtcNow, Guid.NewGuid());

        await _journal.DidNotReceive().CreateJournalEntryAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CreateJournalEntryRequestDto>(),
            Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostApPaymentAsync_WhenApAccountIsNull_SkipsPosting()
    {
        var tenantId = Guid.NewGuid();
        _settings.GetSettingsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantGlSettingsDto(tenantId, Guid.NewGuid(), "1.1.1", null, null));

        await _sut.PostApPaymentAsync(tenantId, Guid.NewGuid(), "Invoice #1", 100m, DateTime.UtcNow, Guid.NewGuid());

        await _journal.DidNotReceive().CreateJournalEntryAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CreateJournalEntryRequestDto>(),
            Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostApPaymentAsync_WhenSettingsComplete_CreatesBalancedJournalEntry()
    {
        var tenantId  = Guid.NewGuid();
        var cashId    = Guid.NewGuid();
        var apId      = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var apEntryId = Guid.NewGuid();
        var paidAt    = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc);

        _settings.GetSettingsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantGlSettingsDto(tenantId, cashId, "1.1.1", apId, "2.1.1"));

        _journal.CreateJournalEntryAsync(
                default, default, default!, default, default, default)
            .ReturnsForAnyArgs(new JournalEntryDto(
                Guid.NewGuid(), tenantId, paidAt, "AP payment: Invoice #1",
                "AccountsPayable", apEntryId, userId, DateTime.UtcNow,
                Array.Empty<JournalLineDto>()));

        await _sut.PostApPaymentAsync(tenantId, apEntryId, "Invoice #1", 500m, paidAt, userId);

        await _journal.Received(1).CreateJournalEntryAsync(
            tenantId,
            userId,
            Arg.Is<CreateJournalEntryRequestDto>(r =>
                r.Lines.Count == 2 &&
                r.Lines.Any(l =>
                    l.AccountId == apId &&
                    l.EntryType == (int)JournalEntryType.Debit &&
                    l.Amount == 500m) &&
                r.Lines.Any(l =>
                    l.AccountId == cashId &&
                    l.EntryType == (int)JournalEntryType.Credit &&
                    l.Amount == 500m)),
            "AccountsPayable",
            apEntryId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostApPaymentAsync_WhenSettingsComplete_DescriptionContainsApDescription()
    {
        var tenantId = Guid.NewGuid();
        _settings.GetSettingsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantGlSettingsDto(tenantId, Guid.NewGuid(), "1.1.1", Guid.NewGuid(), "2.1.1"));

        _journal.CreateJournalEntryAsync(default, default, default!, default, default, default)
            .ReturnsForAnyArgs(new JournalEntryDto(
                Guid.NewGuid(), tenantId, DateTime.UtcNow, string.Empty,
                null, null, Guid.NewGuid(), DateTime.UtcNow,
                Array.Empty<JournalLineDto>()));

        await _sut.PostApPaymentAsync(tenantId, Guid.NewGuid(), "Rent Q2", 1200m, DateTime.UtcNow, Guid.NewGuid());

        await _journal.Received(1).CreateJournalEntryAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Is<CreateJournalEntryRequestDto>(r => r.Description == "AP payment: Rent Q2"),
            Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail (GlPostingService not yet created)**

```bash
dotnet test --filter "FullyQualifiedName~GlPostingServiceTests"
```

Expected: Build error — `GlPostingService` type not found.

- [ ] **Step 3: Create IGlPostingService.cs**

```csharp
namespace Prumo.Application.Services
{
    public interface IGlPostingService
    {
        Task PostApPaymentAsync(
            Guid tenantId, Guid apEntryId, string description,
            decimal amount, DateTime paidAt, Guid userId,
            CancellationToken ct = default);
    }
}
```

- [ ] **Step 4: Create GlPostingService.cs**

```csharp
using Prumo.Application.DTOs.Finance;
using Prumo.Domain.Enums;

namespace Prumo.Application.Services
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
```

- [ ] **Step 5: Run tests — expect pass**

```bash
dotnet test --filter "FullyQualifiedName~GlPostingServiceTests"
```

Expected: 4 tests passing.

- [ ] **Step 6: Commit**

```bash
git add Prumo.Application/Services/IGlPostingService.cs \
        Prumo.Application/Services/GlPostingService.cs \
        Prumo.Tests/Services/GlPostingServiceTests.cs
git commit -m "feat: add IGlPostingService and GlPostingService with tests"
```

---

## Task 9: EF Core Configurations + DbContext

**Files:**
- Create: `Prumo.Infrastructure/Data/Configurations/AccountConfiguration.cs`
- Create: `Prumo.Infrastructure/Data/Configurations/JournalEntryConfiguration.cs`
- Create: `Prumo.Infrastructure/Data/Configurations/JournalLineConfiguration.cs`
- Create: `Prumo.Infrastructure/Data/Configurations/TenantGlSettingsConfiguration.cs`
- Modify: `Prumo.Infrastructure/Data/ApplicationDbContext.cs`

- [ ] **Step 1: Create AccountConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prumo.Domain.Entities;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            builder.ToTable("Accounts");

            builder.HasKey(a => a.Id);

            builder.Property(a => a.Code).IsRequired().HasMaxLength(20);
            builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
            builder.Property(a => a.Type).HasConversion<int>();

            builder.HasIndex(a => new { a.TenantId, a.Code }).IsUnique();
            builder.HasIndex(a => new { a.TenantId, a.ParentId });

            builder.HasOne(a => a.Parent)
                .WithMany(a => a.Children)
                .HasForeignKey(a => a.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
```

- [ ] **Step 2: Create JournalEntryConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prumo.Domain.Entities;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
    {
        public void Configure(EntityTypeBuilder<JournalEntry> builder)
        {
            builder.ToTable("JournalEntries");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
            builder.Property(e => e.SourceModule).HasMaxLength(50);

            builder.HasIndex(e => new { e.TenantId, e.Date });
            builder.HasIndex(e => new { e.TenantId, e.SourceModule, e.SourceDocumentId });

            builder.HasMany(e => e.Lines)
                .WithOne(l => l.JournalEntry)
                .HasForeignKey(l => l.JournalEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

- [ ] **Step 3: Create JournalLineConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prumo.Domain.Entities;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
    {
        public void Configure(EntityTypeBuilder<JournalLine> builder)
        {
            builder.ToTable("JournalLines");

            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedNever();

            builder.Property(l => l.Amount).HasColumnType("decimal(18,2)");
            builder.Property(l => l.EntryType).HasConversion<int>();

            builder.HasIndex(l => l.JournalEntryId);
            builder.HasIndex(l => l.AccountId);

            builder.HasOne(l => l.Account)
                .WithMany(a => a.JournalLines)
                .HasForeignKey(l => l.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
```

- [ ] **Step 4: Create TenantGlSettingsConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prumo.Domain.Entities;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class TenantGlSettingsConfiguration : IEntityTypeConfiguration<TenantGlSettings>
    {
        public void Configure(EntityTypeBuilder<TenantGlSettings> builder)
        {
            builder.ToTable("TenantGlSettings");

            builder.HasKey(s => s.TenantId);
            builder.Property(s => s.TenantId).ValueGeneratedNever();

            builder.HasOne(s => s.Tenant)
                .WithOne()
                .HasForeignKey<TenantGlSettings>(s => s.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(s => s.DefaultCashAccount)
                .WithMany()
                .HasForeignKey(s => s.DefaultCashAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(s => s.DefaultAccountsPayableAccount)
                .WithMany()
                .HasForeignKey(s => s.DefaultAccountsPayableAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
```

- [ ] **Step 5: Add DbSets to ApplicationDbContext**

Open `Prumo.Infrastructure/Data/ApplicationDbContext.cs` and add these four lines after the existing `AccountsPayableEntries` DbSet:

```csharp
        // General Ledger
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
        public DbSet<JournalLine> JournalLines => Set<JournalLine>();
        public DbSet<TenantGlSettings> TenantGlSettings => Set<TenantGlSettings>();
```

Also add the required using at the top of the file (if not already present via implicit usings):

```csharp
using Prumo.Domain.Entities;
```

- [ ] **Step 6: Build entire solution**

```bash
dotnet build
```

Expected: Build succeeded, 0 error(s). All prior service files that referenced `_db.Accounts` and `_db.JournalLines` will now compile.

- [ ] **Step 7: Run all tests**

```bash
dotnet test
```

Expected: All tests pass.

- [ ] **Step 8: Commit**

```bash
git add Prumo.Infrastructure/Data/Configurations/AccountConfiguration.cs \
        Prumo.Infrastructure/Data/Configurations/JournalEntryConfiguration.cs \
        Prumo.Infrastructure/Data/Configurations/JournalLineConfiguration.cs \
        Prumo.Infrastructure/Data/Configurations/TenantGlSettingsConfiguration.cs \
        Prumo.Infrastructure/Data/ApplicationDbContext.cs
git commit -m "feat: add EF configurations and DbSets for GL entities"
```

---

## Task 10: Chart of Accounts Seeder

**Files:**
- Create: `Prumo.Infrastructure/Data/Seeders/ChartOfAccountsSeeder.cs`
- Modify: `Prumo.Infrastructure/Data/DbInitializer.cs`

- [ ] **Step 1: Create ChartOfAccountsSeeder.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;

namespace Prumo.Infrastructure.Data.Seeders
{
    public static class ChartOfAccountsSeeder
    {
        private record AccountSeed(string Code, string Name, AccountType Type, bool IsAnalytic, string? ParentCode);

        private static readonly AccountSeed[] DefaultAccounts =
        {
            new("1",     "Ativo",                           AccountType.Asset,     false, null),
            new("1.1",   "Ativo Circulante",                AccountType.Asset,     false, "1"),
            new("1.1.1", "Caixa e Equivalentes",            AccountType.Asset,     true,  "1.1"),
            new("1.1.2", "Contas a Receber",                AccountType.Asset,     true,  "1.1"),
            new("1.2",   "Ativo Não Circulante",            AccountType.Asset,     false, "1"),
            new("1.2.1", "Imobilizado",                     AccountType.Asset,     true,  "1.2"),
            new("2",     "Passivo",                         AccountType.Liability, false, null),
            new("2.1",   "Passivo Circulante",              AccountType.Liability, false, "2"),
            new("2.1.1", "Fornecedores / Contas a Pagar",   AccountType.Liability, true,  "2.1"),
            new("2.1.2", "Empréstimos e Financiamentos",    AccountType.Liability, true,  "2.1"),
            new("3",     "Patrimônio Líquido",              AccountType.Equity,    false, null),
            new("3.1",   "Capital Social",                  AccountType.Equity,    true,  "3"),
            new("3.2",   "Lucros/Prejuízos Acumulados",     AccountType.Equity,    true,  "3"),
            new("4",     "Receita",                         AccountType.Revenue,   false, null),
            new("4.1",   "Receita Operacional",             AccountType.Revenue,   true,  "4"),
            new("5",     "Despesas",                        AccountType.Expense,   false, null),
            new("5.1",   "Despesas Operacionais",           AccountType.Expense,   true,  "5"),
            new("5.2",   "Custo dos Produtos/Serviços",     AccountType.Expense,   true,  "5"),
        };

        public static async Task SeedAsync(
            ApplicationDbContext db, Guid tenantId, CancellationToken ct = default)
        {
            var hasAccounts = await db.Accounts
                .IgnoreQueryFilters()
                .AnyAsync(a => a.TenantId == tenantId, ct);
            if (hasAccounts) return;

            var seeded = new Dictionary<string, Guid>();

            foreach (var seed in DefaultAccounts)
            {
                Guid? parentId = seed.ParentCode != null ? seeded[seed.ParentCode] : null;

                var account = new Account
                {
                    TenantId   = tenantId,
                    Code       = seed.Code,
                    Name       = seed.Name,
                    Type       = seed.Type,
                    IsAnalytic = seed.IsAnalytic,
                    ParentId   = parentId
                };

                db.Accounts.Add(account);
                await db.SaveChangesAsync(ct);
                seeded[seed.Code] = account.Id;
            }

            var existingSettings = await db.TenantGlSettings
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

            if (existingSettings == null)
            {
                db.TenantGlSettings.Add(new TenantGlSettings
                {
                    TenantId                        = tenantId,
                    DefaultCashAccountId            = seeded["1.1.1"],
                    DefaultAccountsPayableAccountId = seeded["2.1.1"]
                });
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
```

- [ ] **Step 2: Call the seeder from DbInitializer**

Open `Prumo.Infrastructure/Data/DbInitializer.cs`. Find the line that calls `TenantBootstrapSeeder.SeedAsync` inside `EnsureDefaultTenantAsync` and add the chart of accounts call immediately after it:

```csharp
            await Seeders.TenantBootstrapSeeder.SeedAsync(context, tenant.Id);
            await Seeders.ChartOfAccountsSeeder.SeedAsync(context, tenant.Id);
```

The full `EnsureDefaultTenantAsync` ending should look like:

```csharp
            await Seeders.TenantBootstrapSeeder.SeedAsync(context, tenant.Id);
            await Seeders.ChartOfAccountsSeeder.SeedAsync(context, tenant.Id);
        }
```

- [ ] **Step 3: Build**

```bash
dotnet build Prumo.Infrastructure
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 4: Commit**

```bash
git add Prumo.Infrastructure/Data/Seeders/ChartOfAccountsSeeder.cs \
        Prumo.Infrastructure/Data/DbInitializer.cs
git commit -m "feat: add ChartOfAccountsSeeder, call from DbInitializer"
```

---

## Task 11: EF Core Migration

**Files:** Auto-generated under `Prumo.Infrastructure/Migrations/`

- [ ] **Step 1: Add migration**

```bash
dotnet ef migrations add AddFinancialCorePhase1 -p Prumo.Infrastructure -s Prumo.Api
```

Expected: Migration file created, e.g. `20260428XXXXXX_AddFinancialCorePhase1.cs`.

- [ ] **Step 2: Verify the migration creates the right tables**

Open the generated `..._AddFinancialCorePhase1.cs` file and confirm it contains `CreateTable` calls for:
- `Accounts`
- `JournalEntries`
- `JournalLines`
- `TenantGlSettings`

If any are missing, check that the DbSets were added correctly in Task 9 Step 5.

- [ ] **Step 3: Apply migration to the database**

```bash
dotnet ef database update -p Prumo.Infrastructure -s Prumo.Api
```

Expected: Database updated successfully.

- [ ] **Step 4: Commit the migration**

```bash
git add Prumo.Infrastructure/Migrations/
git commit -m "feat: add EF migration AddFinancialCorePhase1"
```

---

## Task 12: DI Registration + AP Integration

**Files:**
- Modify: `Prumo.Api/Configuration/DependencyInjectionConfiguration.cs`
- Modify: `Prumo.Application/Services/IAccountsPayableService.cs`
- Modify: `Prumo.Application/Services/AccountsPayableService.cs`
- Modify: `Prumo.Api/Controllers/AccountsPayableEntriesController.cs`

- [ ] **Step 1: Register the four new services in DI**

Open `Prumo.Api/Configuration/DependencyInjectionConfiguration.cs`. After the `// Accounts Payable` block, add:

```csharp
        // General Ledger
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IJournalService, JournalService>();
        services.AddScoped<ITenantGlSettingsService, TenantGlSettingsService>();
        services.AddScoped<IGlPostingService, GlPostingService>();
```

Also add the needed using directives if not resolved via implicit usings — all four types are in `Prumo.Application.Services`.

- [ ] **Step 2: Update IAccountsPayableService — add userId to MarkEntryPaidAsync**

Open `Prumo.Application/Services/IAccountsPayableService.cs`. Change the `MarkEntryPaidAsync` signature from:

```csharp
Task<EntryDto> MarkEntryPaidAsync(Guid tenantId, Guid entryId, MarkPaidRequestDto request, CancellationToken cancellationToken = default);
```

to:

```csharp
Task<EntryDto> MarkEntryPaidAsync(Guid tenantId, Guid entryId, Guid paidByUserId, MarkPaidRequestDto request, CancellationToken cancellationToken = default);
```

- [ ] **Step 3: Update AccountsPayableService — inject IGlPostingService and call it**

Open `Prumo.Application/Services/AccountsPayableService.cs`.

**3a.** Add the field and update the constructor:

```csharp
        private readonly ApplicationDbContext _db;
        private readonly IGlPostingService _glPosting;

        public AccountsPayableService(ApplicationDbContext db, IGlPostingService glPosting)
        {
            _db        = db;
            _glPosting = glPosting;
        }
```

**3b.** Update the `MarkEntryPaidAsync` signature and body to add `paidByUserId` and call GL posting:

```csharp
        public async Task<EntryDto> MarkEntryPaidAsync(
            Guid tenantId, Guid entryId, Guid paidByUserId,
            MarkPaidRequestDto request, CancellationToken cancellationToken = default)
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
```

- [ ] **Step 4: Update AccountsPayableEntriesController — pass CurrentUserId**

Open `Prumo.Api/Controllers/AccountsPayableEntriesController.cs`. Find the `MarkPaid` action and update the service call to pass `CurrentUserId`:

```csharp
        [HttpPost("{id:guid}/mark-paid")]
        [ProducesResponseType(typeof(EntryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EntryDto>> MarkPaid(
            Guid tenantId, Guid id,
            [FromBody] MarkPaidRequestDto request,
            CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();

            var entry = await _service.MarkEntryPaidAsync(tenantId, id, CurrentUserId, request, ct);
            return Ok(entry);
        }
```

- [ ] **Step 5: Build the full solution**

```bash
dotnet build
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 6: Run all tests**

```bash
dotnet test
```

Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add Prumo.Api/Configuration/DependencyInjectionConfiguration.cs \
        Prumo.Application/Services/IAccountsPayableService.cs \
        Prumo.Application/Services/AccountsPayableService.cs \
        Prumo.Api/Controllers/AccountsPayableEntriesController.cs
git commit -m "feat: wire DI for GL services, integrate GL posting into AP payment"
```

---

## Task 13: Controllers

**Files:**
- Create: `Prumo.Api/Controllers/ChartOfAccountsController.cs`
- Create: `Prumo.Api/Controllers/GeneralLedgerController.cs`

- [ ] **Step 1: Create ChartOfAccountsController.cs**

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prumo.Application.DTOs.Finance;
using Prumo.Application.Services;
using Prumo.Domain.Enums;
using System.Security.Claims;

namespace Prumo.Api.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/chart-of-accounts")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ChartOfAccountsController : ControllerBase
    {
        private readonly IAccountService _service;
        private readonly ITenantService _tenantService;

        public ChartOfAccountsController(IAccountService service, ITenantService tenantService)
        {
            _service       = service;
            _tenantService = tenantService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private async Task<TenantRole?> GetRoleAsync(Guid tenantId, CancellationToken ct) =>
            await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);

        private static bool CanAccess(TenantRole? role) => role.HasValue;
        private static bool CanManage(TenantRole? role) => role is TenantRole.Owner or TenantRole.Admin;

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<AccountDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<AccountDto>>> List(
            Guid tenantId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.ListAccountsAsync(tenantId, includeInactive, ct));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AccountDto>> GetById(
            Guid tenantId, Guid id, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            var account = await _service.GetAccountAsync(tenantId, id, ct);
            return account == null ? NotFound() : Ok(account);
        }

        [HttpPost]
        [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<AccountDto>> Create(
            Guid tenantId, [FromBody] CreateAccountRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            var account = await _service.CreateAccountAsync(tenantId, request, ct);
            return CreatedAtAction(nameof(GetById), new { tenantId, id = account.Id }, account);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AccountDto>> Update(
            Guid tenantId, Guid id, [FromBody] UpdateAccountRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.UpdateAccountAsync(tenantId, id, request, ct));
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Deactivate(
            Guid tenantId, Guid id, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            await _service.DeactivateAccountAsync(tenantId, id, ct);
            return NoContent();
        }
    }
}
```

- [ ] **Step 2: Create GeneralLedgerController.cs**

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prumo.Application.DTOs.Finance;
using Prumo.Application.Services;
using Prumo.Domain.Common;
using Prumo.Domain.Enums;
using System.Security.Claims;

namespace Prumo.Api.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/general-ledger")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class GeneralLedgerController : ControllerBase
    {
        private readonly IJournalService _journalService;
        private readonly ITenantGlSettingsService _settingsService;
        private readonly ITenantService _tenantService;

        public GeneralLedgerController(
            IJournalService journalService,
            ITenantGlSettingsService settingsService,
            ITenantService tenantService)
        {
            _journalService  = journalService;
            _settingsService = settingsService;
            _tenantService   = tenantService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private async Task<TenantRole?> GetRoleAsync(Guid tenantId, CancellationToken ct) =>
            await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);

        private static bool CanAccess(TenantRole? role) => role.HasValue;
        private static bool CanManage(TenantRole? role) => role is TenantRole.Owner or TenantRole.Admin;

        [HttpGet("entries")]
        [ProducesResponseType(typeof(PagedResult<JournalEntryListItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<PagedResult<JournalEntryListItemDto>>> ListEntries(
            Guid tenantId, [FromQuery] JournalEntryQueryDto query, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _journalService.ListEntriesAsync(tenantId, query, ct));
        }

        [HttpGet("entries/{id:guid}")]
        [ProducesResponseType(typeof(JournalEntryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<JournalEntryDto>> GetEntry(
            Guid tenantId, Guid id, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            var entry = await _journalService.GetEntryAsync(tenantId, id, ct);
            return entry == null ? NotFound() : Ok(entry);
        }

        [HttpPost("entries")]
        [ProducesResponseType(typeof(JournalEntryDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<JournalEntryDto>> CreateEntry(
            Guid tenantId, [FromBody] CreateJournalEntryRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            var entry = await _journalService.CreateJournalEntryAsync(tenantId, CurrentUserId, request, ct: ct);
            return CreatedAtAction(nameof(GetEntry), new { tenantId, id = entry.Id }, entry);
        }

        [HttpGet("accounts/{accountId:guid}/statement")]
        [ProducesResponseType(typeof(AccountStatementDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AccountStatementDto>> GetStatement(
            Guid tenantId, Guid accountId,
            [FromQuery] DateTime? from, [FromQuery] DateTime? to,
            CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _journalService.GetAccountStatementAsync(tenantId, accountId, from, to, ct));
        }

        [HttpGet("settings")]
        [ProducesResponseType(typeof(TenantGlSettingsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<TenantGlSettingsDto>> GetSettings(
            Guid tenantId, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _settingsService.GetSettingsAsync(tenantId, ct));
        }

        [HttpPut("settings")]
        [ProducesResponseType(typeof(TenantGlSettingsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<TenantGlSettingsDto>> UpdateSettings(
            Guid tenantId, [FromBody] UpdateTenantGlSettingsDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _settingsService.UpdateSettingsAsync(tenantId, request, ct));
        }
    }
}
```

- [ ] **Step 3: Build the full solution**

```bash
dotnet build
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 4: Run all tests**

```bash
dotnet test
```

Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add Prumo.Api/Controllers/ChartOfAccountsController.cs \
        Prumo.Api/Controllers/GeneralLedgerController.cs
git commit -m "feat: add ChartOfAccountsController and GeneralLedgerController"
```

---

## Task 14: Final Verification

- [ ] **Step 1: Full clean build**

```bash
dotnet build --configuration Release
```

Expected: Build succeeded, 0 warning(s) that indicate real issues (informational warnings about nullable reference types are fine).

- [ ] **Step 2: Run all tests**

```bash
dotnet test
```

Expected: All tests pass. You should see at least 8 tests: the 4 existing domain/paged-result tests + 4 JournalEntryTests + 4 GlPostingServiceTests.

- [ ] **Step 3: Start the API and verify startup**

```bash
dotnet run --project Prumo.Api
```

Expected: Application starts without errors. Watch for:
- "Banco de dados verificado/criado com sucesso" — migrations applied
- No exceptions during startup (seeder will run for the default tenant)

- [ ] **Step 4: Final commit**

```bash
git add -A
git commit -m "feat: complete Financial Core Phase 1 — Chart of Accounts + General Ledger"
```

---

## Quick Reference: New Endpoints

| Method | URL | Auth | Description |
|--------|-----|------|-------------|
| GET | `api/tenants/{id}/chart-of-accounts` | Any role | List all accounts |
| GET | `api/tenants/{id}/chart-of-accounts/{accountId}` | Any role | Single account |
| POST | `api/tenants/{id}/chart-of-accounts` | Admin/Owner | Create account |
| PUT | `api/tenants/{id}/chart-of-accounts/{accountId}` | Admin/Owner | Update account |
| DELETE | `api/tenants/{id}/chart-of-accounts/{accountId}` | Admin/Owner | Deactivate account |
| GET | `api/tenants/{id}/general-ledger/entries` | Any role | List journal entries |
| GET | `api/tenants/{id}/general-ledger/entries/{entryId}` | Any role | Single journal entry |
| POST | `api/tenants/{id}/general-ledger/entries` | Admin/Owner | Manual journal entry |
| GET | `api/tenants/{id}/general-ledger/accounts/{accountId}/statement` | Any role | Account statement |
| GET | `api/tenants/{id}/general-ledger/settings` | Any role | Get GL defaults |
| PUT | `api/tenants/{id}/general-ledger/settings` | Admin/Owner | Update GL defaults |

# Financial Core Phase 1 — Chart of Accounts + General Ledger

**Date:** 2026-04-28  
**Scope:** Phase 1 of ERP Financial Core expansion  
**Stack:** .NET 10, EF Core, PostgreSQL, Clean Architecture (Domain → Application → Infrastructure → API)

---

## Context

The platform already has a working Accounts Payable (AP) module. Phase 1 adds the foundational accounting layer that AP (and future modules) will integrate with:

1. **Chart of Accounts** — hierarchical account structure, seeded per tenant
2. **General Ledger** — double-entry journal, with automatic posting from AP payments

Phase 2 (AR, Cash Flow, Reports) is out of scope here.

---

## Decisions Made

| Question | Decision |
|---|---|
| GL posting from AP | Automatic on payment (direct service call, Approach A) |
| Default chart of accounts | Seeded on tenant creation (simplified Brazilian Plano de Contas) |
| Fiscal period locking | No — entries can be posted to any date freely |
| Multi-currency | No — single currency per tenant |
| Scope | MVP only — no period closing, no reversals, no audit trail beyond CreatedAt |

---

## Domain Entities

### `Account` — Chart of Accounts entry

Inherits `EntityBase` + implements `ITenantScoped`.

| Field | Type | Constraints |
|---|---|---|
| `TenantId` | Guid | FK → Tenants, Cascade |
| `Code` | string | max 20, required, unique per tenant |
| `Name` | string | max 200, required |
| `Type` | `AccountType` enum | Asset=1, Liability=2, Equity=3, Revenue=4, Expense=5 |
| `IsAnalytic` | bool | true = leaf (receives journal lines); false = synthetic/grouping |
| `ParentId` | Guid? | self-referencing FK → Accounts |

**DB indexes:** `(TenantId, Code)` UNIQUE; `(TenantId, ParentId)`.  
**Constraint:** A synthetic account cannot be directly debited or credited (enforced in service layer).  
**Deactivation constraint:** An analytic account with posted journal lines cannot be deactivated.

---

### `JournalEntry` — GL Journal Header

Inherits `EntityBase` + implements `ITenantScoped`.

| Field | Type | Constraints |
|---|---|---|
| `TenantId` | Guid | FK → Tenants, Cascade |
| `Date` | DateTime | competence/accrual date, UTC |
| `Description` | string | max 500, required |
| `SourceModule` | string? | max 50, e.g. `"AccountsPayable"` |
| `SourceDocumentId` | Guid? | e.g. AP entry Id |
| `CreatedByUserId` | Guid | required |

---

### `JournalLine` — GL Journal Line

Not an `EntityBase` — owned by `JournalEntry`. No independent identity outside the entry.

| Field | Type | Constraints |
|---|---|---|
| `Id` | Guid | PK |
| `JournalEntryId` | Guid | FK → JournalEntries, Cascade |
| `AccountId` | Guid | FK → Accounts, Restrict |
| `EntryType` | `JournalEntryType` enum | Debit=1, Credit=2 |
| `Amount` | decimal(18,2) | > 0 |

**DB indexes:** `(JournalEntryId)`; `(AccountId)`.

---

### `TenantGlSettings` — GL defaults per tenant

Not an `EntityBase`. 1:1 with `Tenant`. Seeded alongside the chart of accounts.

| Field | Type | Constraints |
|---|---|---|
| `TenantId` | Guid | PK + FK → Tenants, Cascade |
| `DefaultCashAccountId` | Guid? | FK → Accounts |
| `DefaultAccountsPayableAccountId` | Guid? | FK → Accounts |

---

### New Enums

```
AccountType:      Asset=1, Liability=2, Equity=3, Revenue=4, Expense=5
JournalEntryType: Debit=1, Credit=2
```

---

## Seeded Chart of Accounts

Applied by `ChartOfAccountsSeeder`, called from `DbInitializer` after a tenant is first created. The seeder also writes `TenantGlSettings` pointing to the two default accounts.

```
Code    Name                              Type       IsAnalytic  GL Default
1       Ativo                             Asset      false
1.1     Ativo Circulante                  Asset      false
1.1.1   Caixa e Equivalentes              Asset      true        ← DefaultCashAccountId
1.1.2   Contas a Receber                  Asset      true
1.2     Ativo Não Circulante              Asset      false
1.2.1   Imobilizado                       Asset      true
2       Passivo                           Liability  false
2.1     Passivo Circulante                Liability  false
2.1.1   Fornecedores / Contas a Pagar     Liability  true        ← DefaultAccountsPayableAccountId
2.1.2   Empréstimos e Financiamentos      Liability  true
3       Patrimônio Líquido                Equity     false
3.1     Capital Social                    Equity     true
3.2     Lucros/Prejuízos Acumulados       Equity     true
4       Receita                           Revenue    false
4.1     Receita Operacional               Revenue    true
5       Despesas                          Expense    false
5.1     Despesas Operacionais             Expense    true
5.2     Custo dos Produtos/Serviços       Expense    true
```

---

## Application Services

### `IAccountService` / `AccountService`

```
ListAccountsAsync(tenantId, includeInactive)     → IReadOnlyList<AccountDto>
GetAccountAsync(tenantId, accountId)             → AccountDto?
CreateAccountAsync(tenantId, dto)                → AccountDto
UpdateAccountAsync(tenantId, accountId, dto)     → AccountDto
DeactivateAccountAsync(tenantId, accountId)      → Task
```

**Rules:**
- `Code` must be unique per tenant (case-insensitive).
- `ParentId` must reference an account in the same tenant that is synthetic (`IsAnalytic = false`).
- `IsAnalytic = false` accounts cannot be directly posted to (enforced in journal service, not account service).
- Deactivation blocked if the account has any `JournalLine` entries.

---

### `IJournalService` / `JournalService`

```
ListEntriesAsync(tenantId, query)                                  → PagedResult<JournalEntryDto>
GetEntryAsync(tenantId, entryId)                                   → JournalEntryDto?
CreateManualEntryAsync(tenantId, userId, dto)                      → JournalEntryDto
GetAccountStatementAsync(tenantId, accountId, from?, to?, page)    → AccountStatementDto
```

**Double-entry validation rule (enforced on every save):**
```
SUM(lines where EntryType == Debit) == SUM(lines where EntryType == Credit)
```
If unbalanced, throw a domain exception before persisting.

**Account statement:** returns `JournalLine` rows for the given account ordered by `JournalEntry.Date ASC`, with a `RunningBalance` column computed cumulatively. Debit increases the balance for Asset/Expense accounts; Credit increases for Liability/Equity/Revenue (normal balance convention).

---

### `IGlPostingService` / `GlPostingService`

Internal service — not exposed via API. Called by `AccountsPayableService`.

```
PostApPaymentAsync(tenantId, apEntryId, description, amount, paidAt, userId) → Task
```

**Flow:**
1. Load `TenantGlSettings` for tenant.
2. If `DefaultCashAccountId` or `DefaultAccountsPayableAccountId` is null → log a warning and return (graceful skip).
3. Build a `JournalEntry`:
   - `Date = paidAt`
   - `Description = "AP payment: {description}"`
   - `SourceModule = "AccountsPayable"`
   - `SourceDocumentId = apEntryId`
   - Line 1: **Debit** `DefaultAccountsPayableAccountId`, `amount` (reduces liability)
   - Line 2: **Credit** `DefaultCashAccountId`, `amount` (reduces cash asset)
4. Persist via `JournalService.CreateManualEntryAsync` (reuses balanced-entry validation).

**AP integration change:** `AccountsPayableService.MarkEntryPaidAsync` calls `IGlPostingService.PostApPaymentAsync` after marking the entry paid, within the same `SaveChangesAsync` transaction boundary.

---

### `ITenantGlSettingsService` / `TenantGlSettingsService`

```
GetSettingsAsync(tenantId)              → TenantGlSettingsDto
UpdateSettingsAsync(tenantId, dto)      → TenantGlSettingsDto
```

Allows Admin/Owner to reassign the default GL accounts (e.g., use a bank account instead of Cash).

---

## DTOs

### Chart of Accounts

```
AccountDto:              Id, TenantId, Code, Name, Type, TypeName, IsAnalytic, ParentId, IsActive, CreatedAt, UpdatedAt?
CreateAccountRequestDto: Code, Name, Type, IsAnalytic, ParentId?
UpdateAccountRequestDto: Code, Name, Type, IsAnalytic, ParentId?, IsActive
```

### General Ledger

```
JournalLineDto:              Id, AccountId, AccountCode, AccountName, EntryType, Amount
JournalEntryDto:             Id, TenantId, Date, Description, SourceModule?, SourceDocumentId?, CreatedByUserId, CreatedAt, Lines: JournalLineDto[]
JournalEntryListItemDto:     Id, Date, Description, SourceModule?, TotalAmount, LineCount, CreatedAt
CreateJournalLineDto:        AccountId, EntryType, Amount
CreateJournalEntryRequestDto: Date, Description, Lines: CreateJournalLineDto[] (min 2)
JournalEntryQueryDto:        From?, To?, SourceModule?, Page, PageSize
AccountStatementLineDto:     JournalEntryId, Date, Description, EntryType, Amount, RunningBalance
AccountStatementDto:         AccountId, AccountCode, AccountName, From?, To?, OpeningBalance, Lines: AccountStatementLineDto[], ClosingBalance
TenantGlSettingsDto:         TenantId, DefaultCashAccountId?, DefaultCashAccountCode?, DefaultAccountsPayableAccountId?, DefaultAccountsPayableAccountCode?
UpdateTenantGlSettingsDto:   DefaultCashAccountId?, DefaultAccountsPayableAccountId?
```

---

## API Endpoints

### Chart of Accounts — `api/tenants/{tenantId}/chart-of-accounts`

| Method | Path | Description | Auth |
|---|---|---|---|
| GET | `/` | List all accounts (flat, with parentId) | Any tenant role |
| GET | `/{id}` | Get single account | Any tenant role |
| POST | `/` | Create account | Admin/Owner |
| PUT | `/{id}` | Update account | Admin/Owner |
| DELETE | `/{id}` | Deactivate account | Admin/Owner |

### General Ledger — `api/tenants/{tenantId}/general-ledger`

| Method | Path | Description | Auth |
|---|---|---|---|
| GET | `/entries` | Paged list of journal entries | Any tenant role |
| GET | `/entries/{id}` | Single entry with lines | Any tenant role |
| POST | `/entries` | Create manual journal entry | Admin/Owner |
| GET | `/accounts/{accountId}/statement` | Account statement with running balance | Any tenant role |
| GET | `/settings` | Get GL default account settings | Any tenant role |
| PUT | `/settings` | Update GL default accounts | Admin/Owner |

---

## Infrastructure

### EF Configurations

- `AccountConfiguration` — table `"Accounts"`, column lengths, self-referencing FK with `DeleteBehavior.Restrict`, indexes
- `JournalEntryConfiguration` — table `"JournalEntries"`, indexes on `(TenantId, Date)`, `(TenantId, SourceModule, SourceDocumentId)`
- `JournalLineConfiguration` — table `"JournalLines"`, indexes on `(JournalEntryId)`, `(AccountId)`
- `TenantGlSettingsConfiguration` — table `"TenantGlSettings"`, PK = TenantId, nullable FKs to Accounts

### Migrations

Single migration: `AddFinancialCorePhase1`
- Creates `Accounts`, `JournalEntries`, `JournalLines`, `TenantGlSettings` tables
- Adds all FK constraints and indexes described above

### DbContext changes

Add to `ApplicationDbContext`:
```csharp
public DbSet<Account> Accounts => Set<Account>();
public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
public DbSet<JournalLine> JournalLines => Set<JournalLine>();
public DbSet<TenantGlSettings> TenantGlSettings => Set<TenantGlSettings>();
```

`JournalLine` is automatically tenant-scoped through `JournalEntry` — it does not implement `ITenantScoped` itself.

### Seeders

New: `ChartOfAccountsSeeder`
- Called from `DbInitializer` when a new tenant row is created
- Seeds all 18 accounts from the table above
- Creates `TenantGlSettings` pointing to accounts `1.1.1` and `2.1.1`

---

## DI Registration

Add to `DependencyInjectionConfiguration.AddApplicationServices`:
```
services.AddScoped<IAccountService, AccountService>();
services.AddScoped<IJournalService, JournalService>();
services.AddScoped<IGlPostingService, GlPostingService>();
services.AddScoped<ITenantGlSettingsService, TenantGlSettingsService>();
```

---

## Authorization

Two new resource codes seeded in the `Resources` table:

| Code | Module | Admin/Owner Level | Member Level |
|---|---|---|---|
| `ChartOfAccounts.Management` | Finance | Full | Read |
| `GeneralLedger.Management` | Finance | Full | Read |

Controller authorization follows the existing AP pattern: `CanAccess()` for reads, Admin/Owner check for mutations.

---

## AP Module Changes

Only one change to existing code: `AccountsPayableService.MarkEntryPaidAsync` gains a call to `IGlPostingService.PostApPaymentAsync`. No entity changes, no DTO changes, no controller changes.

---

## Out of Scope (Phase 1)

- Journal entry reversal
- Fiscal period locking / closing
- Multi-currency
- Trial Balance, P&L, GL detail reports (Phase 2)
- AR module (Phase 2)
- Cash Flow module (Phase 2)
- Cost centers
- Attachments / documents on journal entries

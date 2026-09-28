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
            new("1",     "Assets",                          AccountType.Asset,     false, null),
            new("1.1",   "Current Assets",                  AccountType.Asset,     false, "1"),
            new("1.1.1", "Cash and Cash Equivalents",       AccountType.Asset,     true,  "1.1"),
            new("1.1.2", "Accounts Receivable",             AccountType.Asset,     true,  "1.1"),
            new("1.2",   "Non-current Assets",              AccountType.Asset,     false, "1"),
            new("1.2.1", "Property, Plant and Equipment",   AccountType.Asset,     true,  "1.2"),
            new("2",     "Liabilities",                     AccountType.Liability, false, null),
            new("2.1",   "Current Liabilities",             AccountType.Liability, false, "2"),
            new("2.1.1", "Suppliers / Accounts Payable",    AccountType.Liability, true,  "2.1"),
            new("2.1.2", "Loans and Borrowings",            AccountType.Liability, true,  "2.1"),
            new("3",     "Equity",                          AccountType.Equity,    false, null),
            new("3.1",   "Share Capital",                   AccountType.Equity,    true,  "3"),
            new("3.2",   "Retained Earnings",               AccountType.Equity,    true,  "3"),
            new("4",     "Revenue",                         AccountType.Revenue,   false, null),
            new("4.1",   "Operating Revenue",               AccountType.Revenue,   true,  "4"),
            new("5",     "Expenses",                        AccountType.Expense,   false, null),
            new("5.1",   "Operating Expenses",              AccountType.Expense,   true,  "5"),
            new("5.2",   "Cost of Goods and Services",      AccountType.Expense,   true,  "5"),
            new("2.1.3", "Salaries Payable",          AccountType.Liability, true,  "2.1"),
            new("2.1.4", "Payroll Taxes Payable",     AccountType.Liability, true,  "2.1"),
            new("5.1.1", "Personnel Expenses",        AccountType.Expense,   true,  "5.1"),
            new("5.1.2", "Administrative Expenses",   AccountType.Expense,   true,  "5.1"),
            new("5.1.3", "Other Operating Expenses",  AccountType.Expense,   true,  "5.1"),
            new("5.1.4", "Supplier Expenses",         AccountType.Expense,   true,  "5.1"),
        };

        public static async Task SeedAsync(
            ApplicationDbContext db, Guid tenantId, CancellationToken ct = default)
        {
            var hasAccounts = await db.Accounts
                // Cross-tenant on purpose: the seeder runs at startup, with no TenantContext.
                // The tenantId comes from the parameter and is filtered right below. Without
                // the bypass the fail-closed filter would return nothing and seeding would
                // silently break.
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
                    DefaultAccountsPayableAccountId = seeded["2.1.1"],
                    DefaultExpenseAccountId         = seeded["5.1.4"]
                });
                await db.SaveChangesAsync(ct);
            }
        }
    }
}

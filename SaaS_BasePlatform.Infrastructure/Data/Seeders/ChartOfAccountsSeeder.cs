using Microsoft.EntityFrameworkCore;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Infrastructure.Data.Seeders
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
            new("2.1.3", "Salários a Pagar",          AccountType.Liability, true,  "2.1"),
            new("2.1.4", "Encargos Sociais a Pagar",  AccountType.Liability, true,  "2.1"),
            new("5.1.1", "Despesas com Pessoal",      AccountType.Expense,   true,  "5.1"),
            new("5.1.2", "Despesas Administrativas",  AccountType.Expense,   true,  "5.1"),
            new("5.1.3", "Despesas Operacionais",     AccountType.Expense,   true,  "5.1"),
            new("5.1.4", "Despesas com Fornecedores", AccountType.Expense,   true,  "5.1"),
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
                    DefaultAccountsPayableAccountId = seeded["2.1.1"],
                    DefaultExpenseAccountId         = seeded["5.1.4"]
                });
                await db.SaveChangesAsync(ct);
            }
        }
    }
}

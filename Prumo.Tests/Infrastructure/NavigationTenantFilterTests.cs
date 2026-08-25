using Microsoft.EntityFrameworkCore;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;

namespace Prumo.Tests.Infrastructure
{
    /// <summary>
    /// WorkLog, Payment, PaymentPeriod e JournalLine não têm coluna TenantId — são
    /// escopados pelo pai. Sem filtro por navegação, o isolamento deles depende
    /// inteiramente de o service lembrar do join, que é a disciplina que a fase 2
    /// troca por estrutura.
    /// </summary>
    public class NavigationTenantFilterTests
    {
        private static ApplicationDbContext NewDb(ITenantContext ctx, string dbName) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName).Options, ctx);

        [Fact]
        public async Task WorkLogs_of_another_tenant_are_invisible()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();

            var seedCtx = new TenantContext();
            seedCtx.SetTenant(tenantA);
            await using (var seed = NewDb(seedCtx, dbName))
            {
                var empA = new Employee { TenantId = tenantA, FullName = "A", CPF = "1" };
                var empB = new Employee { TenantId = tenantB, FullName = "B", CPF = "2" };
                seed.Employees.AddRange(empA, empB);
                seed.WorkLogs.AddRange(
                    new WorkLog { EmployeeId = empA.Id, WorkDate = DateTime.UtcNow.Date },
                    new WorkLog { EmployeeId = empB.Id, WorkDate = DateTime.UtcNow.Date });
                await seed.SaveChangesAsync();
            }

            var ctx = new TenantContext();
            ctx.SetTenant(tenantA);
            await using var db = NewDb(ctx, dbName);

            // Sem nenhum Where de tenant e sem guard: só o filtro pode proteger.
            var visible = await db.WorkLogs.ToListAsync();

            Assert.Single(visible);
        }

        [Fact]
        public async Task WorkLogs_are_invisible_when_no_tenant_is_resolved()
        {
            var dbName = Guid.NewGuid().ToString();
            var tenantA = Guid.NewGuid();

            var seedCtx = new TenantContext();
            seedCtx.SetTenant(tenantA);
            await using (var seed = NewDb(seedCtx, dbName))
            {
                var emp = new Employee { TenantId = tenantA, FullName = "A", CPF = "1" };
                seed.Employees.Add(emp);
                seed.WorkLogs.Add(new WorkLog { EmployeeId = emp.Id, WorkDate = DateTime.UtcNow.Date });
                await seed.SaveChangesAsync();
            }

            // Fail-closed: sem tenant resolvido, nada volta.
            await using var db = NewDb(new TenantContext(), dbName);

            Assert.Empty(await db.WorkLogs.ToListAsync());
        }
    }
}

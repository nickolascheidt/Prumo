using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prumo.Infrastructure.Migrations
{
    /// <summary>
    /// Dá a cada role o acesso ao dashboard que ela já enxergava pelo recurso do módulo.
    /// </summary>
    /// <remarks>
    /// As abas do dashboard passaram a ter recurso próprio (<c>Dashboard.HR</c> e afins)
    /// em vez de emprestar o do módulo vizinho. Sem este backfill, todo mundo perderia as
    /// abas na troca.
    ///
    /// <b>Por que migration e não uma rotina de startup:</b> a primeira versão disto
    /// rodava a cada boot, e por isso <b>desfazia revogações</b> — tirar
    /// <c>Dashboard.HR</c> de uma role que ainda tivesse <c>HR.Employees</c> durava até o
    /// próximo restart. É a mesma forma do bug 4203a15, em que re-semear ressuscitava
    /// grants revogados. Migration roda uma vez, por definição, e o problema desaparece.
    ///
    /// O <c>WHERE NOT EXISTS</c> mantém a operação idempotente mesmo assim.
    /// </remarks>
    public partial class BackfillDashboardResourceGrants : Migration
    {
        /// <summary>Aba do dashboard → recurso que ela gateava antes.</summary>
        private static readonly (string Dashboard, string Source)[] Map =
        {
            ("Dashboard.Accounting", "GeneralLedger.Management"),
            ("Dashboard.Finance",    "AccountsPayable.Entries"),
            ("Dashboard.HR",         "HR.Employees"),
            ("Dashboard.Admin",      "User.Management")
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (dashboard, source) in Map)
            {
                // Nível fixo em 1 (Read): dashboard só mostra números, então Write e Full
                // não significariam nada ali.
                // A chave é composta (TenantId, RoleId, ResourceId) — a tabela não tem Id
                // próprio nem IsActive.
                migrationBuilder.Sql($@"
                    INSERT INTO ""ResourcePermissions""
                        (""TenantId"", ""RoleId"", ""ResourceId"", ""Level"", ""CreatedAt"")
                    SELECT src.""TenantId"", src.""RoleId"", dash.""Id"", 1, now()
                    FROM ""ResourcePermissions"" src
                    JOIN ""Resources"" srcRes
                      ON srcRes.""Id"" = src.""ResourceId"" AND srcRes.""Code"" = '{source}'
                    JOIN ""Resources"" dash
                      ON dash.""TenantId"" = src.""TenantId"" AND dash.""Code"" = '{dashboard}'
                    WHERE NOT EXISTS (
                        SELECT 1 FROM ""ResourcePermissions"" existing
                        WHERE existing.""RoleId"" = src.""RoleId""
                          AND existing.""ResourceId"" = dash.""Id""
                          AND existing.""TenantId"" = src.""TenantId""
                    );");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove só os grants de dashboard. Não dá para distinguir os que vieram
            // deste backfill dos concedidos à mão depois — e reverter esta migration
            // significa voltar ao mundo em que as abas nem tinham recurso próprio.
            var codes = string.Join(", ", Map.Select(m => $"'{m.Dashboard}'"));

            migrationBuilder.Sql($@"
                DELETE FROM ""ResourcePermissions"" rp
                USING ""Resources"" r
                WHERE r.""Id"" = rp.""ResourceId"" AND r.""Code"" IN ({codes});");
        }
    }
}

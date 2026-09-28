using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prumo.Infrastructure.Migrations
{
    /// <summary>
    /// Gives each role access to the dashboard it already saw through the module resource.
    /// </summary>
    /// <remarks>
    /// Dashboard tabs got their own resource (<c>Dashboard.HR</c> and so on) instead of
    /// borrowing the neighboring module's. Without this backfill, everyone would lose the
    /// tabs in the switch.
    ///
    /// <b>Why a migration and not a startup routine:</b> the first version of this ran on
    /// every boot, and so it <b>undid revocations</b> — removing <c>Dashboard.HR</c> from a
    /// role that still had <c>HR.Employees</c> lasted until the next restart. A migration
    /// runs once by definition, and the problem goes away.
    ///
    /// The <c>WHERE NOT EXISTS</c> keeps it idempotent anyway.
    /// </remarks>
    public partial class BackfillDashboardResourceGrants : Migration
    {
        /// <summary>Dashboard tab → the resource that gated it before.</summary>
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
                // Level fixed at 1 (Read): a dashboard only shows numbers, so Write and Full
                // would mean nothing there.
                // The key is composite (TenantId, RoleId, ResourceId) — the table has no Id
                // of its own and no IsActive.
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
            // Removes only the dashboard grants. There is no way to tell the ones from this
            // backfill from those granted by hand later — and reverting this migration means
            // going back to a world where the tabs did not even have their own resource.
            var codes = string.Join(", ", Map.Select(m => $"'{m.Dashboard}'"));

            migrationBuilder.Sql($@"
                DELETE FROM ""ResourcePermissions"" rp
                USING ""Resources"" r
                WHERE r.""Id"" = rp.""ResourceId"" AND r.""Code"" IN ({codes});");
        }
    }
}

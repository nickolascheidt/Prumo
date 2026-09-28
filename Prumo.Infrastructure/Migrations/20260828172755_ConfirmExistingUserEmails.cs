using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prumo.Infrastructure.Migrations
{
    /// <summary>
    /// Marks everyone who already existed as having a confirmed e-mail.
    ///
    /// From here on login requires `EmailConfirmed`. Whoever was created before — the
    /// seeded admin and every member the admin registered with a password — never went
    /// through any confirmation, and without this backfill would be **locked out**
    /// overnight, without having done anything.
    ///
    /// It is a migration and not a startup routine on purpose: it must run **once**.
    /// Repeated on every boot, it would re-confirm accounts someone had unconfirmed.
    /// </summary>
    public partial class ConfirmExistingUserEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "AspNetUsers" SET "EmailConfirmed" = true WHERE "EmailConfirmed" = false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No revert: there is no way to know which accounts were unconfirmed before,
            // and unconfirming all of them would lock everyone out.
        }
    }
}

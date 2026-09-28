using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prumo.Infrastructure.Migrations
{
    /// <summary>
    /// Brings Serilog's `logs` table into the versioned schema.
    ///
    /// It used to be created by the sink itself (`needAutoCreateTable: true`), which meant
    /// **DDL over the application connection** on every startup. With `prumo_app` limited
    /// to DML that would start failing; and even before that, it was schema outside the
    /// migrations' control.
    ///
    /// The columns mirror the `ColumnWriter`s in
    /// `Prumo.Api/Configuration/LoggingConfiguration.cs` exactly — changing one means
    /// changing the other.
    /// </summary>
    public partial class CreateLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS because on any database that already ran the app the table
            // exists, created by the sink. This migration adopts the one that is there.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS logs (
                    message          text,
                    message_template text,
                    level            character varying(50),
                    raise_date       timestamp with time zone,
                    exception        text,
                    properties       jsonb,
                    props_test       jsonb,
                    user_name        character varying(50),
                    client_ip        character varying(50)
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No DROP on purpose. On most databases this table predates the migration and
            // holds log history; reverting the schema is no reason to delete it. Whoever
            // wants it gone drops it by hand, deliberately.
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prumo.Infrastructure.Migrations
{
    /// <summary>
    /// Traz a tabela `logs` do Serilog para o schema versionado.
    ///
    /// Ela era criada pelo próprio sink (`needAutoCreateTable: true`), o que significava
    /// **DDL na conexão da aplicação** a cada startup. Com `prumo_app` restrito a DML
    /// (item 11), isso passaria a falhar; e mesmo antes disso, era schema fora do
    /// controle das migrations.
    ///
    /// As colunas espelham exatamente os `ColumnWriter`s de
    /// `Prumo.Api/Configuration/LoggingConfiguration.cs` — mudar um exige mudar o outro.
    /// </summary>
    public partial class CreateLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS porque em qualquer banco que já rodou a aplicação a tabela
            // existe, criada pelo sink. Esta migration adota a que está lá.
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
            // Sem DROP de propósito. Na maioria dos bancos esta tabela é anterior à
            // migration e guarda histórico de log; reverter o schema não é motivo para
            // apagá-lo. Quem quiser removê-la faz na mão, conscientemente.
        }
    }
}

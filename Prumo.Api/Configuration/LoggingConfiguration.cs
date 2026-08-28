using NpgsqlTypes;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;

namespace Prumo.Api.Configuration;

public static class LoggingConfiguration
{
    public static Serilog.ILogger CreateBootstrapLogger()
    {
        // Falha de sink é silenciosa por design no Serilog: a exceção morre dentro do
        // batch periódico. Foi assim que o sink do Postgres passou meses sem escrever
        // uma linha sem ninguém notar. O SelfLog só fala quando algo quebra.
        Serilog.Debugging.SelfLog.Enable(message => Console.Error.WriteLine($"[serilog] {message}"));

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .WriteTo.Console()
            .CreateBootstrapLogger();
    }

    public static IHostBuilder ConfigureSerilog(this IHostBuilder host)
    {
        return host.UseSerilog((context, services, configuration) =>
        {
            var connectionString = context.Configuration.GetConnectionString("DefaultConnection");

            configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .WriteTo.Console();

            if (!string.IsNullOrEmpty(connectionString) && !context.HostingEnvironment.IsEnvironment("Demo"))
            {
                var columnWriters = new Dictionary<string, ColumnWriterBase>
                {
                    { "message", new RenderedMessageColumnWriter(NpgsqlDbType.Text) },
                    { "message_template", new MessageTemplateColumnWriter(NpgsqlDbType.Text) },
                    { "level", new LevelColumnWriter(true, NpgsqlDbType.Varchar) },
                    { "raise_date", new UtcTimestampColumnWriter() },
                    { "exception", new ExceptionColumnWriter(NpgsqlDbType.Text) },
                    { "properties", new LogEventSerializedColumnWriter(NpgsqlDbType.Jsonb) },
                    { "props_test", new PropertiesColumnWriter(NpgsqlDbType.Jsonb) },
                    { "user_name", new SinglePropertyColumnWriter("UserName", PropertyWriteMethod.ToString, NpgsqlDbType.Varchar, "l") },
                    { "client_ip", new SinglePropertyColumnWriter("ClientIp", PropertyWriteMethod.ToString, NpgsqlDbType.Varchar, "l") }
                };

                // `needAutoCreateTable: false` porque a aplicação conecta como `prumo_app`,
                // que não faz DDL (ver `db/roles.sql`). A tabela `logs` é criada pela
                // migration `CreateLogsTable`, junto do resto do schema — se ela faltar, o
                // sink falha aqui em vez de o Postgres recusar um CREATE TABLE silencioso.
                configuration.WriteTo.PostgreSQL(
                    connectionString: connectionString,
                    tableName: "logs",
                    columnOptions: columnWriters,
                    needAutoCreateTable: false,
                    restrictedToMinimumLevel: LogEventLevel.Information);
            }
        });
    }
}

using NpgsqlTypes;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;

namespace Prumo.Api.Configuration;

public static class LoggingConfiguration
{
    public static Serilog.ILogger CreateBootstrapLogger()
    {
        // Sink failures are silent by design in Serilog: the exception dies inside the
        // periodic batch. That is how the Postgres sink once went months without writing a
        // row with nobody noticing. SelfLog only speaks when something breaks.
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

                // `needAutoCreateTable: false` because the app connects as `prumo_app`,
                // which does not run DDL (see `db/roles.sql`). The `logs` table is created by
                // the `CreateLogsTable` migration, with the rest of the schema — if it is
                // missing, the sink fails here instead of Postgres silently refusing a
                // CREATE TABLE.
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

using NpgsqlTypes;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;

namespace SaaS_BasePlatform.Api.Configuration;

public static class LoggingConfiguration
{
    public static Serilog.ILogger CreateBootstrapLogger()
    {
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
                    { "raise_date", new TimestampColumnWriter(NpgsqlDbType.TimestampTz) },
                    { "exception", new ExceptionColumnWriter(NpgsqlDbType.Text) },
                    { "properties", new LogEventSerializedColumnWriter(NpgsqlDbType.Jsonb) },
                    { "props_test", new PropertiesColumnWriter(NpgsqlDbType.Jsonb) },
                    { "user_name", new SinglePropertyColumnWriter("UserName", PropertyWriteMethod.ToString, NpgsqlDbType.Varchar, "l") },
                    { "client_ip", new SinglePropertyColumnWriter("ClientIp", PropertyWriteMethod.ToString, NpgsqlDbType.Varchar, "l") }
                };

                configuration.WriteTo.PostgreSQL(
                    connectionString: connectionString,
                    tableName: "logs",
                    columnOptions: columnWriters,
                    needAutoCreateTable: true,
                    restrictedToMinimumLevel: LogEventLevel.Information);
            }
        });
    }
}

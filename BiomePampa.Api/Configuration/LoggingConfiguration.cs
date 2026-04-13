using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;

namespace BiomePampa.Api.Configuration;

public static class LoggingConfiguration
{
    public static Serilog.ILogger CreateBootstrapLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
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
                var sinkOpts = new MSSqlServerSinkOptions
                {
                    TableName = "Logs",
                    SchemaName = "dbo",
                    AutoCreateSqlTable = true
                };

                var columnOpts = new ColumnOptions();
                columnOpts.Store.Remove(StandardColumn.Properties);
                columnOpts.Store.Add(StandardColumn.LogEvent);
                columnOpts.AdditionalColumns = new[]
                {
                    new SqlColumn { ColumnName = "UserName", DataType = System.Data.SqlDbType.NVarChar, DataLength = 256, AllowNull = true },
                    new SqlColumn { ColumnName = "ClientIp", DataType = System.Data.SqlDbType.NVarChar, DataLength = 50, AllowNull = true }
                };

                configuration.WriteTo.MSSqlServer(
                    connectionString: connectionString,
                    sinkOptions: sinkOpts,
                    columnOptions: columnOpts,
                    restrictedToMinimumLevel: LogEventLevel.Information);
            }
        });
    }
}

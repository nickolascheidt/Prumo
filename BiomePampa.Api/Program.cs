using BiomePampa.Api.Configuration;
using Serilog;

Log.Logger = LoggingConfiguration.CreateBootstrapLogger();

try
{
    Log.Information("Iniciando aplicação BiomePampa");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.ConfigureSerilog();

    builder.Services.AddControllers();
    builder.Services.AddDatabaseConfiguration(builder.Configuration);
    builder.Services.AddAuthenticationConfiguration(builder.Configuration);
    builder.Services.AddAuthorizationConfiguration();
    builder.Services.AddApplicationServices();
    builder.Services.AddHealthChecksConfiguration();
    builder.Services.AddRateLimitingConfiguration(builder.Configuration);
    builder.Services.AddCorsConfiguration(builder.Configuration);
    builder.Services.AddOpenApi();

    var app = builder.Build();

    await app.InitializeDatabaseAsync();

    app.ConfigureMiddleware();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aplicação encerrada inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}

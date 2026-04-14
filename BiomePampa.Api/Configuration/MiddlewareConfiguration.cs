using Scalar.AspNetCore;
using Serilog;

namespace BiomePampa.Api.Configuration;

public static class MiddlewareConfiguration
{
    public static WebApplication ConfigureMiddleware(this WebApplication app)
    {
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Demo"))
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            });
        }

        app.UseCors("AppCorsPolicy");

        if (!app.Environment.IsEnvironment("Demo"))
        {
            app.UseHttpsRedirection();
        }

        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("UserName", httpContext.User?.Identity?.Name);
                diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
            };
        });
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthCheckEndpoints();
        app.MapControllers();

        return app;
    }
}

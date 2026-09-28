using Prumo.Api.Middleware;
using Scalar.AspNetCore;
using Serilog;

namespace Prumo.Api.Configuration;

public static class MiddlewareConfiguration
{
    public static WebApplication ConfigureMiddleware(this WebApplication app)
    {
        // First of all: everything below (logging, per-IP rate limit, CORS) must see the
        // client's IP and scheme, not nginx's. See ForwardedHeadersConfiguration.
        app.UseForwardedHeaders();

        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Demo"))
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            });
        }

        // No UseHttpsRedirection: TLS terminates at Caddy, which already redirects
        // 80 → 443. In here there was no HTTPS port to point to and it only logged a warning.

        app.UseCors("AppCorsPolicy");

        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                // An anonymous request has no name, and RemoteIpAddress is null on transports
                // without an IP. Set does not accept null; the column stays NULL because the
                // property is absent, which is what SinglePropertyColumnWriter already does.
                var userName = httpContext.User?.Identity?.Name;
                if (userName is not null)
                {
                    diagnosticContext.Set("UserName", userName);
                }

                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString();
                if (clientIp is not null)
                {
                    diagnosticContext.Set("ClientIp", clientIp);
                }
            };
        });
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.UseAuthorization();

        app.MapHealthCheckEndpoints();
        app.MapControllers();

        return app;
    }
}

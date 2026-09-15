using Prumo.Api.Middleware;
using Scalar.AspNetCore;
using Serilog;

namespace Prumo.Api.Configuration;

public static class MiddlewareConfiguration
{
    public static WebApplication ConfigureMiddleware(this WebApplication app)
    {
        // Primeiro de todos: tudo abaixo (log, rate limit por IP, CORS) precisa ver o
        // IP e o scheme do cliente, não os do nginx. Ver ForwardedHeadersConfiguration.
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

        // Sem UseHttpsRedirection: TLS termina no Caddy, que já redireciona 80 → 443.
        // Aqui dentro ele não tinha porta HTTPS para apontar e só avisava no log.

        app.UseCors("AppCorsPolicy");

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
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.UseAuthorization();

        app.MapHealthCheckEndpoints();
        app.MapControllers();

        return app;
    }
}

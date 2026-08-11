using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Prumo.Api.Configuration;

public static class RateLimitingConfiguration
{
    public static IServiceCollection AddRateLimitingConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Política para usuários autenticados
            options.AddPolicy("authenticated", context =>
            {
                var username = context.User?.Identity?.Name;
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: username ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = configuration.GetValue<int>("RateLimiting:PermitLimit", 100),
                        Window = TimeSpan.FromSeconds(configuration.GetValue<int>("RateLimiting:Window", 60)),
                        QueueLimit = configuration.GetValue<int>("RateLimiting:QueueLimit", 10)
                    });
            });

            // Política para endpoints públicos (mais restritiva)
            options.AddFixedWindowLimiter("public", options =>
            {
                options.PermitLimit = 10;
                options.Window = TimeSpan.FromMinutes(1);
                options.QueueLimit = 2;
            });
        });

        return services;
    }
}

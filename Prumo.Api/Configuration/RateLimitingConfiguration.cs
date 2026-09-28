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

            // Policy for authenticated users
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

            // Public endpoints (login, sign-up, forgot password): 10 per minute PER IP.
            // A plain fixed window would be one window for the whole site — 10 logins per
            // minute across every client, so any anonymous caller could lock everyone out
            // with 10 requests. The IP is the real one only because ForwardedHeaders runs
            // first (ForwardedHeadersConfiguration).
            options.AddPolicy("public", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: PublicPartitionKey(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 2
                    }));
        });

        return services;
    }

    /// <summary>
    /// Partition key for the public limit: the source IP. Without an IP (unix socket,
    /// tests) it falls into a fixed partition instead of throwing.
    /// </summary>
    public static string PublicPartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

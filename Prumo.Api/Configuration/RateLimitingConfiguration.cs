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

            // Endpoints públicos (login, cadastro, esqueci-senha): 10 por minuto POR IP.
            // Era AddFixedWindowLimiter, que é uma janela única para o site inteiro —
            // 10 logins por minuto somando todos os clientes, e qualquer anônimo
            // derrubava o login de todo mundo com 10 requisições. O IP é o real só
            // porque o ForwardedHeaders roda antes (ForwardedHeadersConfiguration).
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
    /// Chave da partição do limite público: o IP de origem. Sem IP (socket unix, teste)
    /// cai numa partição fixa em vez de estourar.
    /// </summary>
    public static string PublicPartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

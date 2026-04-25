using SaaS_BasePlatform.Infrastructure.Services;
using StackExchange.Redis;

namespace SaaS_BasePlatform.Api.Configuration;

public static class CacheConfiguration
{
    public static IServiceCollection AddCacheConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("Redis");

        if (string.IsNullOrEmpty(redisConnection))
        {
            // Fallback para cache em memória se Redis não estiver configurado
            services.AddDistributedMemoryCache();
        }
        else
        {
            // Configurar Redis
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = configuration["Redis:InstanceName"] ?? "SaaS_BasePlatform_";
            });

            // Registrar ConnectionMultiplexer para uso direto se necessário
            services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(redisConnection));
        }

        services.AddScoped<ICacheService, CacheService>();

        return services;
    }
}

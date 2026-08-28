using Azure.Messaging.ServiceBus;
using Prumo.Infrastructure.Services;

namespace Prumo.Api.Configuration;

public static class NotificationConfiguration
{
    /// <summary>
    /// Liga a API à fila de notificações. Sem `ServiceBus:ConnectionString` a aplicação
    /// **sobe assim mesmo**, com um publisher que descarta e avisa — rodar a API para mexer
    /// numa tela não deve exigir o emulador do Service Bus, que puxa dois containers.
    /// </summary>
    public static IServiceCollection AddNotificationConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration["ServiceBus:ConnectionString"];
        var queueName = configuration["ServiceBus:QueueName"] ?? "notifications";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<INotificationPublisher>(sp => new LoggingNotificationPublisher(
                sp.GetRequiredService<ILogger<LoggingNotificationPublisher>>(),
                includePayload: environment.IsDevelopment()));

            return services;
        }

        // Singleton: o cliente é seguro para concorrência e caro de criar. Um por
        // requisição abriria uma conexão AMQP por chamada.
        services.AddSingleton(_ => new ServiceBusClient(connectionString));
        services.AddSingleton<INotificationPublisher>(sp => new ServiceBusNotificationPublisher(
            sp.GetRequiredService<ServiceBusClient>(),
            queueName,
            sp.GetRequiredService<ILogger<ServiceBusNotificationPublisher>>()));

        return services;
    }
}

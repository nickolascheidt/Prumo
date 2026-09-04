using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Azure.Messaging.ServiceBus;
using Prumo.Infrastructure.Services;

namespace Prumo.Api.Configuration;

public static class NotificationConfiguration
{
    /// <summary>
    /// Liga a API à fila de notificações, escolhendo o provedor por
    /// `Notifications:Provider`. Sem provedor — ou com o provedor escolhido sem fila
    /// configurada — a aplicação **sobe assim mesmo**, com um publisher que descarta e
    /// avisa: rodar a API para mexer numa tela não deve exigir fila nenhuma.
    /// </summary>
    public static IServiceCollection AddNotificationConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var provider = configuration["Notifications:Provider"];

        if (string.Equals(provider, "Sqs", StringComparison.OrdinalIgnoreCase))
        {
            var queueUrl = configuration["Sqs:QueueUrl"];

            if (!string.IsNullOrWhiteSpace(queueUrl))
            {
                // Singleton: o cliente é seguro para concorrência e caro de criar.
                services.AddSingleton(_ => CreateSqsClient(configuration));
                services.AddSingleton<INotificationPublisher>(sp => new SqsNotificationPublisher(
                    sp.GetRequiredService<IAmazonSQS>(),
                    queueUrl,
                    sp.GetRequiredService<ILogger<SqsNotificationPublisher>>()));

                return services;
            }
        }
        else if (string.Equals(provider, "ServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = configuration["ServiceBus:ConnectionString"];
            var queueName = configuration["ServiceBus:QueueName"] ?? "notifications";

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                // Singleton pelo mesmo motivo: um cliente por requisição abriria uma
                // conexão AMQP por chamada.
                services.AddSingleton(_ => new ServiceBusClient(connectionString));
                services.AddSingleton<INotificationPublisher>(sp => new ServiceBusNotificationPublisher(
                    sp.GetRequiredService<ServiceBusClient>(),
                    queueName,
                    sp.GetRequiredService<ILogger<ServiceBusNotificationPublisher>>()));

                return services;
            }
        }

        services.AddSingleton<INotificationPublisher>(sp => new LoggingNotificationPublisher(
            sp.GetRequiredService<ILogger<LoggingNotificationPublisher>>(),
            includePayload: environment.IsDevelopment()));

        return services;
    }

    /// <summary>
    /// Duplicada, de propósito, em `Prumo.Notifications/Program.cs`. Ver o mapa de
    /// arquivos do plano `2026-09-03-fila-e-email-portaveis.md`: são composition roots de
    /// processos diferentes, e o único projeto comum aos dois é o de contratos, que não
    /// deve carregar o SDK da AWS.
    /// </summary>
    private static IAmazonSQS CreateSqsClient(IConfiguration configuration)
    {
        var serviceUrl = configuration["Sqs:ServiceUrl"];
        var region = configuration["Sqs:Region"] ?? "us-east-1";
        var config = new AmazonSQSConfig();

        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
            return new AmazonSQSClient(config);
        }

        // ServiceURL e RegionEndpoint são mutuamente exclusivos: atribuir um anula o outro.
        config.ServiceURL = serviceUrl;
        config.AuthenticationRegion = region;

        // O ElasticMQ não valida credencial, mas o SDK recusa assinar sem uma.
        return new AmazonSQSClient(new BasicAWSCredentials("local", "local"), config);
    }
}

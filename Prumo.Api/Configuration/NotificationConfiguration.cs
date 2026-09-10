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
    /// `Notifications:Provider`.
    ///
    /// **Em desenvolvimento**, sem provedor — ou com o provedor escolhido sem fila
    /// configurada — a aplicação sobe assim mesmo, com um publisher que descarta e avisa:
    /// rodar a API para mexer numa tela não deve exigir fila nenhuma.
    ///
    /// **Fora de desenvolvimento isso vira erro de startup.** Descartar notificação em
    /// produção é confirmação de cadastro e reset de senha sumindo com uma linha de log —
    /// a mesma classe de falha silenciosa que o sink do Serilog já custou a este projeto.
    /// </summary>
    public static IServiceCollection AddNotificationConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var provider = configuration["Notifications:Provider"];
        var isDevelopment = environment.IsDevelopment();

        // Só para a mensagem de erro saber dizer qual chave preencher.
        var missingKey = "Notifications:Provider";

        if (string.Equals(provider, "Sqs", StringComparison.OrdinalIgnoreCase))
        {
            var queueUrl = configuration["Sqs:QueueUrl"];
            var serviceUrl = configuration["Sqs:ServiceUrl"];

            // `Sqs:ServiceUrl` não é só um endpoint alternativo: preenchida, ela também
            // troca a cadeia de credenciais do SDK — em produção, a role da task — pelas
            // duas strings fixas que o ElasticMQ aceita. Herdada do `appsettings.json`
            // base, produção publicaria para localhost assinando com credencial de
            // mentira, e a falha apareceria em cada publish, longe do startup.
            if (!isDevelopment && !string.IsNullOrWhiteSpace(serviceUrl))
            {
                throw new InvalidOperationException(
                    $"Sqs:ServiceUrl está preenchida ('{serviceUrl}') em {environment.EnvironmentName}. "
                    + "Ela existe para apontar o SDK ao ElasticMQ local e, preenchida, ainda "
                    + "substitui a credencial padrão (role da task) por uma de emulador. "
                    + "Deixe-a vazia para falar com o SQS real.");
            }

            missingKey = "Sqs:QueueUrl";

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

            missingKey = "ServiceBus:ConnectionString";

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

        // O publisher que descarta é uma comodidade de desenvolvimento. Fora dele, chegar
        // aqui é configuração errada, e falhar no startup é melhor que descobrir pelo
        // usuário que não recebeu o e-mail de reset.
        if (!isDevelopment)
        {
            throw new InvalidOperationException(
                $"Nenhuma fila de notificação configurada ({missingKey}) em {environment.EnvironmentName}. "
                + "A API não sobe descartando notificação fora de desenvolvimento: confirmação "
                + "de cadastro e reset de senha sumiriam deixando só um aviso no log.");
        }

        services.AddSingleton<INotificationPublisher>(sp => new LoggingNotificationPublisher(
            sp.GetRequiredService<ILogger<LoggingNotificationPublisher>>(),
            includePayload: true));

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
        var region = configuration["Sqs:Region"] ?? "sa-east-1";
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

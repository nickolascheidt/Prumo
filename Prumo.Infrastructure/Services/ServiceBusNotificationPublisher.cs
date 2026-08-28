using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Prumo.Notifications.Contracts;

namespace Prumo.Infrastructure.Services
{
    /// <summary>
    /// Publica na fila do Service Bus. O <see cref="ServiceBusClient"/> é caro de criar e
    /// seguro para concorrência, então vive como singleton no DI — criar um por mensagem
    /// abriria uma conexão AMQP por requisição.
    /// </summary>
    public sealed class ServiceBusNotificationPublisher : INotificationPublisher, IAsyncDisposable
    {
        private readonly ServiceBusSender _sender;
        private readonly ILogger<ServiceBusNotificationPublisher> _logger;

        public ServiceBusNotificationPublisher(
            ServiceBusClient client,
            string queueName,
            ILogger<ServiceBusNotificationPublisher> logger)
        {
            _sender = client.CreateSender(queueName);
            _logger = logger;
        }

        public async Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            var payload = JsonSerializer.Serialize(message);

            var serviceBusMessage = new ServiceBusMessage(payload)
            {
                ContentType = "application/json",
                // Sobe o tipo e a correlação para propriedades da própria mensagem: assim
                // dá para inspecionar uma fila parada sem desserializar o corpo.
                Subject = message.Type,
                CorrelationId = message.CorrelationId.ToString()
            };

            await _sender.SendMessageAsync(serviceBusMessage, cancellationToken);

            _logger.LogInformation(
                "Notificação {Type} publicada ({CorrelationId})", message.Type, message.CorrelationId);
        }

        public async ValueTask DisposeAsync() => await _sender.DisposeAsync();
    }
}

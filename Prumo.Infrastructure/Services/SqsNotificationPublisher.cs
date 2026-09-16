using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Prumo.Notifications.Contracts;

namespace Prumo.Infrastructure.Services
{
    /// <summary>
    /// Publica na fila SQS. Teve um espelho para Service Bus enquanto a nuvem era a Azure;
    /// desde 2026-09-16 é o único publisher, e `Notifications:Provider` sobrevive só para
    /// recusar valor herdado em vez de subir calado com o provedor errado.
    ///
    /// O <see cref="IAmazonSQS"/> é singleton no DI: é seguro para concorrência e caro de
    /// criar.
    /// </summary>
    public sealed class SqsNotificationPublisher : INotificationPublisher
    {
        private readonly IAmazonSQS _sqs;
        private readonly string _queueUrl;
        private readonly ILogger<SqsNotificationPublisher> _logger;

        public SqsNotificationPublisher(
            IAmazonSQS sqs,
            string queueUrl,
            ILogger<SqsNotificationPublisher> logger)
        {
            _sqs = sqs;
            _queueUrl = queueUrl;
            _logger = logger;
        }

        public async Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            var payload = JsonSerializer.Serialize(message);

            await _sqs.SendMessageAsync(
                new SendMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MessageBody = payload,
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        // Sobe o tipo e a correlação para atributos da própria mensagem: assim
                        // dá para inspecionar uma fila parada sem desserializar o corpo.
                        ["Type"] = new() { DataType = "String", StringValue = message.Type },
                        ["CorrelationId"] = new() { DataType = "String", StringValue = message.CorrelationId.ToString() }
                    }
                },
                cancellationToken);

            _logger.LogInformation(
                "Notificação {Type} publicada ({CorrelationId})", message.Type, message.CorrelationId);
        }
    }
}

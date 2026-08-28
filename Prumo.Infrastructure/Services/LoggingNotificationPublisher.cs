using Microsoft.Extensions.Logging;
using Prumo.Notifications.Contracts;

namespace Prumo.Infrastructure.Services
{
    /// <summary>
    /// Registrado quando **não há fila configurada**, para que a API suba sem o emulador do
    /// Service Bus — que puxa dois containers e nem sempre vale o custo para mexer numa tela.
    ///
    /// Ele **descarta** a notificação, e por isso grita: cada mensagem sai como
    /// <see cref="LogLevel.Warning"/>. Um publisher silencioso aqui significaria "cadastrei
    /// e não recebi o e-mail" sem nada no log explicando — e silêncio em caminho de
    /// notificação já custou meses neste projeto.
    /// </summary>
    public sealed class LoggingNotificationPublisher : INotificationPublisher
    {
        private readonly ILogger<LoggingNotificationPublisher> _logger;
        private readonly bool _includePayload;

        /// <param name="includePayload">
        /// Só em desenvolvimento. O <c>Data</c> carrega o token de confirmação e o de reset
        /// de senha; num log eles são caminho de tomada de conta, e o Serilog deste projeto
        /// tem sink para tabela. Em dev, é o que permite seguir o fluxo sem subir o worker.
        /// </param>
        public LoggingNotificationPublisher(
            ILogger<LoggingNotificationPublisher> logger,
            bool includePayload)
        {
            _logger = logger;
            _includePayload = includePayload;
        }

        public Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            if (_includePayload)
            {
                _logger.LogWarning(
                    "[DEV] Notificação {Type} para {To} DESCARTADA (sem fila configurada). Dados: {Data}",
                    message.Type,
                    message.To,
                    string.Join(", ", message.Data.Select(kv => $"{kv.Key}={kv.Value}")));
            }
            else
            {
                _logger.LogWarning(
                    "Notificação {Type} para {To} DESCARTADA ({CorrelationId}): "
                    + "ServiceBus:ConnectionString não está configurada.",
                    message.Type,
                    message.To,
                    message.CorrelationId);
            }

            return Task.CompletedTask;
        }
    }
}

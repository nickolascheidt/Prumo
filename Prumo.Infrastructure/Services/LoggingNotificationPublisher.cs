using Microsoft.Extensions.Logging;

namespace Prumo.Infrastructure.Services
{
    /// <summary>
    /// The only publisher: it writes the notification to the log instead of sending an
    /// e-mail. In development the log line carries the confirmation / reset / invitation
    /// data, which is what lets you follow those flows locally.
    /// </summary>
    public sealed class LoggingNotificationPublisher : INotificationPublisher
    {
        private readonly ILogger<LoggingNotificationPublisher> _logger;
        private readonly bool _includePayload;

        /// <param name="includePayload">
        /// Development only. <c>Data</c> carries the confirmation and password-reset tokens;
        /// in a log they are an account-takeover path, and the Serilog setup here has a
        /// database sink.
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
                    "[DEV] Notification {Type} to {To}. Data: {Data}",
                    message.Type,
                    message.To,
                    string.Join(", ", message.Data.Select(kv => $"{kv.Key}={kv.Value}")));
            }
            else
            {
                _logger.LogWarning(
                    "Notification {Type} to {To} ({CorrelationId}) was not delivered: "
                    + "no e-mail provider is configured.",
                    message.Type,
                    message.To,
                    message.CorrelationId);
            }

            return Task.CompletedTask;
        }
    }
}

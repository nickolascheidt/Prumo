using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Prumo.Notifications.Contracts;
using Prumo.Notifications.Email;

namespace Prumo.Notifications;

/// <summary>
/// Toda a lógica de notificação, sem uma linha de SDK de fila. Recebe o corpo bruto da
/// mensagem e devolve o que aconteceu; quem traduz isso para a fila é o worker.
/// </summary>
public sealed class NotificationHandler
{
    /// <summary>
    /// A fila entrega ao menos uma vez. Para e-mail, duplicar é irritante e não perigoso,
    /// então uma janela em memória basta — tabela nova custaria mais que o problema que
    /// resolve. Reinício do serviço zera a janela, e é aceitável.
    /// </summary>
    public static readonly TimeSpan DefaultDuplicateWindow = TimeSpan.FromMinutes(10);

    private readonly NotificationRenderer _renderer;
    private readonly IEmailSender _sender;

    /// <summary>
    /// O intervalo entre `TryGetValue` e `Set` abaixo não é atômico, e é aceito assim: com
    /// `MaxConcurrentCalls = 4` no processor (`Program.cs`), duas reentregas do mesmo
    /// `CorrelationId` podem passar juntas pelo `TryGetValue` e as duas enviarem. Duplicar
    /// e-mail é chato, não perigoso — não "conserte" isto com lock em volta do `SendAsync`,
    /// que serializaria todo envio.
    /// </summary>
    private readonly IMemoryCache _seen;
    private readonly ILogger<NotificationHandler> _logger;
    private readonly TimeSpan _duplicateWindow;

    public NotificationHandler(
        NotificationRenderer renderer,
        IEmailSender sender,
        IMemoryCache seen,
        ILogger<NotificationHandler> logger,
        TimeSpan? duplicateWindow = null)
    {
        _renderer = renderer;
        _sender = sender;
        _seen = seen;
        _logger = logger;
        _duplicateWindow = duplicateWindow ?? DefaultDuplicateWindow;
    }

    public async Task<NotificationOutcome> HandleAsync(string body, CancellationToken cancellationToken = default)
    {
        NotificationMessage? message;

        try
        {
            message = JsonSerializer.Deserialize<NotificationMessage>(body);
        }
        catch (JsonException ex)
        {
            // Corpo que não desserializa não melhora com retry. Vai direto para a
            // dead-letter, com o motivo, em vez de girar até estourar a contagem de entrega.
            _logger.LogError(ex, "Corpo de mensagem não é JSON válido.");
            return new NotificationOutcome.Poison("InvalidJson", ex.Message);
        }

        if (message is null)
            return new NotificationOutcome.Poison("EmptyBody", "O corpo desserializou para null.");

        if (_seen.TryGetValue(message.CorrelationId, out _))
        {
            _logger.LogInformation(
                "Notificação {CorrelationId} já enviada nesta janela; ignorando reentrega.",
                message.CorrelationId);
            return new NotificationOutcome.Duplicate();
        }

        OutboundEmail email;

        try
        {
            email = _renderer.Render(message);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            _logger.LogError(ex, "Notificação {CorrelationId} não pôde ser renderizada.", message.CorrelationId);
            return new NotificationOutcome.Poison("RenderFailed", ex.Message);
        }

        // Falha aqui **sobe**: provedor fora do ar é retry, não veneno. Ver Task 4.
        await _sender.SendAsync(email, cancellationToken);

        _seen.Set(message.CorrelationId, true, _duplicateWindow);
        return new NotificationOutcome.Handled();
    }
}

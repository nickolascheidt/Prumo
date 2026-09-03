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
        var email = _renderer.Render(JsonSerializer.Deserialize<NotificationMessage>(body)!);
        await _sender.SendAsync(email, cancellationToken);
        return new NotificationOutcome.Handled();
    }
}

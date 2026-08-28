using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Caching.Memory;
using Prumo.Notifications.Contracts;
using Prumo.Notifications.Email;

namespace Prumo.Notifications;

/// <summary>
/// Consome a fila e envia. É o serviço inteiro.
/// </summary>
public sealed class NotificationWorker : BackgroundService
{
    private readonly ServiceBusProcessor _processor;
    private readonly NotificationRenderer _renderer;
    private readonly IEmailSender _sender;
    private readonly IMemoryCache _seen;
    private readonly ILogger<NotificationWorker> _logger;

    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(10);

    public NotificationWorker(
        ServiceBusProcessor processor,
        NotificationRenderer renderer,
        IEmailSender sender,
        IMemoryCache seen,
        ILogger<NotificationWorker> logger)
    {
        _processor = processor;
        _renderer = renderer;
        _sender = sender;
        _seen = seen;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);
        _logger.LogInformation("Consumindo a fila de notificações.");

        // O processor tem thread própria; aqui é só esperar o desligamento.
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);

        await _processor.StopProcessingAsync(CancellationToken.None);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        NotificationMessage? message;

        try
        {
            message = JsonSerializer.Deserialize<NotificationMessage>(args.Message.Body.ToString());
        }
        catch (JsonException ex)
        {
            // Corpo que não desserializa não melhora com retry. Vai direto para a
            // dead-letter, com o motivo, em vez de girar até estourar MaxDeliveryCount.
            _logger.LogError(ex, "Mensagem {MessageId} não é JSON válido.", args.Message.MessageId);
            await args.DeadLetterMessageAsync(args.Message, "InvalidJson", ex.Message);
            return;
        }

        if (message is null)
        {
            await args.DeadLetterMessageAsync(args.Message, "EmptyBody", "O corpo desserializou para null.");
            return;
        }

        // O Service Bus entrega ao menos uma vez. Para e-mail, duplicar é irritante e não
        // perigoso, então uma janela em memória basta — tabela nova custaria mais do que o
        // problema que resolve. Reinício do serviço zera a janela, e é aceitável.
        if (_seen.TryGetValue(message.CorrelationId, out _))
        {
            _logger.LogInformation(
                "Notificação {CorrelationId} já enviada nesta janela; ignorando reentrega.",
                message.CorrelationId);
            await args.CompleteMessageAsync(args.Message);
            return;
        }

        try
        {
            var email = _renderer.Render(message);
            await _sender.SendAsync(email, args.CancellationToken);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            // Tipo desconhecido ou template sem valor: defeito de código ou de contrato.
            // Retry não conserta nenhum dos dois.
            _logger.LogError(ex, "Notificação {CorrelationId} não pôde ser renderizada.", message.CorrelationId);
            await args.DeadLetterMessageAsync(args.Message, "RenderFailed", ex.Message);
            return;
        }

        _seen.Set(message.CorrelationId, true, DuplicateWindow);
        await args.CompleteMessageAsync(args.Message);
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Falha no processador da fila ({Source}).", args.ErrorSource);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _processor.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}

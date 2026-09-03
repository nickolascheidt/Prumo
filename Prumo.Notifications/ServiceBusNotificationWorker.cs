using Azure.Messaging.ServiceBus;

namespace Prumo.Notifications;

/// <summary>
/// Consome a fila do Service Bus e traduz o <see cref="NotificationOutcome"/> para a API
/// dela. A lógica de notificação está no <see cref="NotificationHandler"/> — aqui não há
/// nada específico de e-mail.
/// </summary>
public sealed class ServiceBusNotificationWorker : BackgroundService
{
    // A dead-letter do Service Bus limita motivo a 256 caracteres e descrição a 4096;
    // passar disso é exceção na hora de dead-letterar, o que trocaria uma mensagem
    // ruim por um worker travado.
    private const int MaxReasonLength = 256;
    private const int MaxDetailLength = 4096;

    private readonly ServiceBusProcessor _processor;
    private readonly NotificationHandler _handler;
    private readonly ILogger<ServiceBusNotificationWorker> _logger;

    public ServiceBusNotificationWorker(
        ServiceBusProcessor processor,
        NotificationHandler handler,
        ILogger<ServiceBusNotificationWorker> logger)
    {
        _processor = processor;
        _handler = handler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);
        _logger.LogInformation("Consumindo a fila de notificações (Service Bus).");

        // O processor tem thread própria; aqui é só esperar o desligamento.
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);

        await _processor.StopProcessingAsync(CancellationToken.None);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        // Exceção que sobe daqui não completa a mensagem, e o Service Bus reentrega.
        // É assim que falha de provedor de e-mail vira retry.
        var outcome = await _handler.HandleAsync(args.Message.Body.ToString(), args.CancellationToken);

        switch (outcome)
        {
            case NotificationOutcome.Poison poison:
                await args.DeadLetterMessageAsync(
                    args.Message,
                    Truncate(poison.Reason, MaxReasonLength),
                    Truncate(poison.Detail, MaxDetailLength));
                break;

            default:
                await args.CompleteMessageAsync(args.Message);
                break;
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Falha no processador da fila ({Source}).", args.ErrorSource);
        return Task.CompletedTask;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    public override void Dispose()
    {
        _processor.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}

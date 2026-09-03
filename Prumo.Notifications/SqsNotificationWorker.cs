using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;

namespace Prumo.Notifications;

public sealed class SqsNotificationWorkerOptions
{
    public string QueueUrl { get; set; } = string.Empty;
    public string DeadLetterQueueUrl { get; set; } = string.Empty;
}

/// <summary>
/// Consome a fila SQS por long polling e traduz o <see cref="NotificationOutcome"/> para a
/// API dela. Contraparte do <see cref="ServiceBusNotificationWorker"/>; a lógica de
/// notificação é a mesma <see cref="NotificationHandler"/>.
/// </summary>
public sealed class SqsNotificationWorker : BackgroundService
{
    // O atributo de mensagem do SQS não aceita valor vazio, e mensagem de exceção pode ser
    // enorme. 1024 é folgado para diagnosticar e cabe sobrando no limite de 256 KB.
    private const int MaxAttributeLength = 1024;

    private readonly IAmazonSQS _sqs;
    private readonly NotificationHandler _handler;
    private readonly SqsNotificationWorkerOptions _options;
    private readonly ILogger<SqsNotificationWorker> _logger;

    public SqsNotificationWorker(
        IAmazonSQS sqs,
        NotificationHandler handler,
        IOptions<SqsNotificationWorkerOptions> options,
        ILogger<SqsNotificationWorker> logger)
    {
        _sqs = sqs;
        _handler = handler;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Consumindo a fila de notificações (SQS: {QueueUrl}).", _options.QueueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            ReceiveMessageResponse response;

            try
            {
                response = await _sqs.ReceiveMessageAsync(
                    new ReceiveMessageRequest
                    {
                        QueueUrl = _options.QueueUrl,
                        // Long polling: uma chamada esperando 20s em vez de vinte chamadas
                        // vazias por minuto.
                        WaitTimeSeconds = 20,
                        MaxNumberOfMessages = 10
                    },
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao ler a fila SQS; nova tentativa em 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            // No SDK v4 as coleções de resposta chegam nulas quando vazias, e não como
            // lista vazia. Sem o `?? []` isto é NullReferenceException no primeiro poll
            // sem mensagem — que é a esmagadora maioria deles.
            foreach (var message in response.Messages ?? [])
                await ProcessAsync(message, stoppingToken);
        }
    }

    private async Task ProcessAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await _handler.HandleAsync(message.Body, cancellationToken);

            switch (outcome)
            {
                case NotificationOutcome.Poison poison:
                    await DeadLetterAsync(message, poison, cancellationToken);
                    break;

                case NotificationOutcome.Handled:
                case NotificationOutcome.Duplicate:
                    await _sqs.DeleteMessageAsync(_options.QueueUrl, message.ReceiptHandle, cancellationToken);
                    break;

                // Mesma razão do worker do Service Bus: um resultado que ninguém ensinou
                // este worker a traduzir não pode ser apagado em silêncio. A exceção cai
                // no catch abaixo, a mensagem não é apagada e a redrive policy acaba
                // levando para a DLQ — alto, e sem perder a notificação.
                default:
                    throw new NotSupportedException(
                        $"NotificationOutcome não tratado: {outcome.GetType().Name}.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Desligamento no meio do processamento. A mensagem volta pelo visibility
            // timeout; não é erro e não merece log de erro.
        }
        catch (Exception ex)
        {
            // Falha de provedor é retry: não apaga. O visibility timeout devolve a
            // mensagem e a redrive policy leva para a DLQ depois de maxReceiveCount.
            // Vale também para falha da própria chamada de fila — engolir aqui mantém o
            // laço vivo em vez de derrubar o worker por uma mensagem.
            _logger.LogError(ex, "Falha ao processar {MessageId}; deixando reentregar.", message.MessageId);
        }
    }

    /// <summary>
    /// O SQS não tem dead-letter nativa como o Service Bus: é publicar na outra fila e
    /// apagar da origem. A ordem importa — publica **antes** de apagar. Se o envio para a
    /// DLQ falhar, a mensagem não é apagada, volta pelo visibility timeout e a redrive
    /// policy acaba levando para a DLQ de qualquer forma. A ordem torna a duplicata
    /// possível e a perda não.
    /// </summary>
    private async Task DeadLetterAsync(Message message, NotificationOutcome.Poison poison, CancellationToken cancellationToken)
    {
        await _sqs.SendMessageAsync(
            new SendMessageRequest
            {
                QueueUrl = _options.DeadLetterQueueUrl,
                MessageBody = message.Body,
                // O mais perto que o SQS chega da dead-letter descrita do Service Bus.
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["PoisonReason"] = new() { DataType = "String", StringValue = Sanitize(poison.Reason) },
                    ["PoisonDetail"] = new() { DataType = "String", StringValue = Sanitize(poison.Detail) }
                }
            },
            cancellationToken);

        await _sqs.DeleteMessageAsync(_options.QueueUrl, message.ReceiptHandle, cancellationToken);

        _logger.LogError(
            "Mensagem {MessageId} enviada para a DLQ: {Reason} — {Detail}",
            message.MessageId, poison.Reason, poison.Detail);
    }

    /// <summary>
    /// Atributo de mensagem do SQS não aceita valor vazio, recusa caracteres de controle
    /// e tem limite de tamanho. O <c>Detail</c> vem de <c>ex.Message</c> — no caso de
    /// template sem valor, ele embute as chaves do payload —, então nada garante que
    /// chegue limpo aqui. Um atributo recusado no publish seria pior que um truncado: a
    /// mensagem envenenada não chegaria à DLQ.
    /// </summary>
    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "-";

        var cleaned = new string([.. value.Select(c => char.IsControl(c) && c is not ('\t' or '\n' or '\r') ? ' ' : c)]);

        return cleaned.Length <= MaxAttributeLength ? cleaned : cleaned[..MaxAttributeLength];
    }
}

using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Prumo.Notifications.Email;

public sealed class AcsEmailSenderOptions
{
    /// <summary>Connection string do recurso de Communication Services.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Remetente. Em teste é o `DoNotReply@&lt;guid&gt;.azurecomm.net` do subdomínio
    /// gerenciado; em produção, um endereço do domínio verificado.
    /// </summary>
    public string SenderAddress { get; set; } = string.Empty;
}

/// <summary>
/// Envia por Azure Communication Services.
///
/// Usa <see cref="WaitUntil.Started"/>: o serviço já roda fora do caminho da requisição, e
/// esperar a confirmação de entrega prenderia o consumidor por dezenas de segundos, com o
/// lock da mensagem correndo. Falha de aceite ainda vem como exceção e cai no retry do
/// Service Bus.
/// </summary>
public sealed class AcsEmailSender : IEmailSender
{
    private readonly EmailClient _client;
    private readonly string _sender;
    private readonly ILogger<AcsEmailSender> _logger;

    public AcsEmailSender(IOptions<AcsEmailSenderOptions> options, ILogger<AcsEmailSender> logger)
    {
        var value = options.Value;

        if (string.IsNullOrWhiteSpace(value.ConnectionString))
            throw new InvalidOperationException("Email:Acs:ConnectionString não configurada.");
        if (string.IsNullOrWhiteSpace(value.SenderAddress))
            throw new InvalidOperationException("Email:Acs:SenderAddress não configurado.");

        _client = new EmailClient(value.ConnectionString);
        _sender = value.SenderAddress;
        _logger = logger;
    }

    public async Task SendAsync(OutboundEmail email, CancellationToken cancellationToken = default)
    {
        var operation = await _client.SendAsync(
            WaitUntil.Started,
            senderAddress: _sender,
            recipientAddress: email.To,
            subject: email.Subject,
            htmlContent: email.HtmlBody,
            cancellationToken: cancellationToken);

        _logger.LogInformation("E-mail aceito pelo ACS para {To} ({OperationId})", email.To, operation.Id);
    }
}

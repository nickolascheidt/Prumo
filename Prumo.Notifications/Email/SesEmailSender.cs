using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Prumo.Notifications.Email;

public sealed class SesEmailSenderOptions
{
    /// <summary>
    /// Remetente. No sandbox do SES, um endereço verificado; fora dele, um do domínio
    /// verificado.
    /// </summary>
    public string SenderAddress { get; set; } = string.Empty;
}

/// <summary>
/// Envia por Amazon SES v2. Terceiro <see cref="IEmailSender"/>, ao lado do
/// <see cref="AcsEmailSender"/> e do <see cref="FileEmailSender"/> — o default de
/// desenvolvimento continua sendo `File`.
///
/// Sem equivalente ao <c>WaitUntil.Started</c> do ACS: o SES já é aceite-e-devolve, e o
/// <c>MessageId</c> da resposta entra no log no lugar do <c>operation.Id</c>.
///
/// Credencial vem da cadeia padrão do SDK — variável de ambiente, perfil, role da task.
/// Esta classe não lê chave de configuração nenhuma, de propósito: em produção é role de
/// IAM, e localmente ela nem é usada.
/// </summary>
public sealed class SesEmailSender : IEmailSender
{
    private readonly IAmazonSimpleEmailServiceV2 _client;
    private readonly string _sender;
    private readonly ILogger<SesEmailSender> _logger;

    public SesEmailSender(
        IAmazonSimpleEmailServiceV2 client,
        IOptions<SesEmailSenderOptions> options,
        ILogger<SesEmailSender> logger)
    {
        if (string.IsNullOrWhiteSpace(options.Value.SenderAddress))
            throw new InvalidOperationException("Email:Ses:SenderAddress não configurado.");

        _client = client;
        _sender = options.Value.SenderAddress;
        _logger = logger;
    }

    public async Task SendAsync(OutboundEmail email, CancellationToken cancellationToken = default)
    {
        var response = await _client.SendEmailAsync(
            new SendEmailRequest
            {
                FromEmailAddress = _sender,
                Destination = new Destination { ToAddresses = [email.To] },
                Content = new EmailContent
                {
                    Simple = new Message
                    {
                        Subject = new Content { Data = email.Subject, Charset = "UTF-8" },
                        Body = new Body { Html = new Content { Data = email.HtmlBody, Charset = "UTF-8" } }
                    }
                }
            },
            cancellationToken);

        _logger.LogInformation("E-mail aceito pelo SES para {To} ({MessageId})", email.To, response.MessageId);
    }
}

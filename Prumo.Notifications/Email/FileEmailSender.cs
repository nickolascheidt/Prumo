using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Prumo.Notifications.Email;

public sealed class FileEmailSenderOptions
{
    /// <summary>Onde os e-mails são gravados. Relativo ao diretório de trabalho do serviço.</summary>
    public string Directory { get; set; } = "sent-emails";
}

/// <summary>
/// Grava o e-mail renderizado em disco em vez de enviar.
///
/// É o que permite verificar o item 8 inteiro — cadastro, confirmação, reset de senha,
/// convite — sem provedor, sem domínio verificado e sem custo. O arquivo é o e-mail: abrir
/// no navegador mostra exatamente o que a pessoa receberia, links inclusive.
/// </summary>
public sealed class FileEmailSender : IEmailSender
{
    private readonly string _directory;
    private readonly ILogger<FileEmailSender> _logger;

    public FileEmailSender(IOptions<FileEmailSenderOptions> options, ILogger<FileEmailSender> logger)
    {
        _directory = Path.GetFullPath(options.Value.Directory);
        _logger = logger;
    }

    public async Task SendAsync(OutboundEmail email, CancellationToken cancellationToken = default)
    {
        System.IO.Directory.CreateDirectory(_directory);

        // Ordenável por nome, e o destinatário no nome para achar o e-mail certo quando há
        // vários. Caracteres inválidos de caminho saem — "@" e "." são válidos no Windows.
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var safeRecipient = string.Join("_", email.To.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(_directory, $"{stamp}_{safeRecipient}.html");

        var document = $"""
            <!doctype html>
            <meta charset="utf-8">
            <title>{email.Subject}</title>
            <p style="font:12px monospace;color:#666;border-bottom:1px solid #ddd;padding-bottom:8px">
              Para: {email.To}<br>Assunto: {email.Subject}
            </p>
            {email.HtmlBody}
            """;

        await File.WriteAllTextAsync(path, document, cancellationToken);

        _logger.LogInformation("E-mail para {To} gravado em {Path}", email.To, path);
    }
}

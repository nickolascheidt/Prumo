namespace Prumo.Notifications.Email;

/// <summary>O e-mail já renderizado, pronto para sair.</summary>
public sealed record OutboundEmail(string To, string Subject, string HtmlBody);

/// <summary>
/// A única coisa que o serviço precisa saber sobre "enviar e-mail". Duas implementações:
/// <c>FileEmailSender</c> em desenvolvimento e <c>AcsEmailSender</c> em produção.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(OutboundEmail email, CancellationToken cancellationToken = default);
}

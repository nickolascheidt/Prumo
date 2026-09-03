namespace Prumo.Notifications;

/// <summary>
/// O que aconteceu com a notificação — e a única coisa que Service Bus e SQS têm em comum.
///
/// A alternativa seria um `IMessageContext` com `CompleteAsync()`/`DeadLetterAsync()`, e
/// ela foi recusada na spec porque vaza: o Service Bus tem dead-letter nativa, com motivo
/// e descrição numa chamada; no SQS, mandar para a DLQ é publicar na outra fila e apagar
/// da origem, duas operações que podem falhar entre si. Um contrato que apresentasse as
/// duas como o mesmo método estaria mentindo. Aqui cada adapter decide o que o resultado
/// significa para a sua fila.
///
/// Construtor privado + tipos aninhados = hierarquia fechada: ninguém de fora acrescenta
/// um quarto caso sem que os dois workers precisem tratá-lo.
/// </summary>
public abstract record NotificationOutcome
{
    private NotificationOutcome() { }

    /// <summary>Renderizou e saiu pelo <c>IEmailSender</c>.</summary>
    public sealed record Handled : NotificationOutcome;

    /// <summary>Reentrega dentro da janela de duplicata. Nada foi enviado, e está certo.</summary>
    public sealed record Duplicate : NotificationOutcome;

    /// <summary>
    /// Não melhora com retry: JSON inválido, corpo nulo, tipo desconhecido ou template
    /// sem valor. Vai para a dead-letter com o motivo.
    /// </summary>
    public sealed record Poison(string Reason, string Detail) : NotificationOutcome;
}

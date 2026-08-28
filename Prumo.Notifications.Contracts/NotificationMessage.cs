using System.Text.Json.Serialization;

namespace Prumo.Notifications.Contracts;

/// <summary>
/// O envelope que atravessa a fila entre a API e o serviço de notificação.
///
/// É um envelope só, com o tipo dentro, em vez de uma fila por assunto: o Service Bus
/// Basic não tem tópicos, e três mensagens não justificam três filas para manter.
///
/// **Nada de credencial aqui.** Os tokens que viajam no <see cref="Data"/> são os do
/// ASP.NET Identity — uso único e expiráveis. Senha, hash e token de sessão nunca entram.
/// </summary>
public sealed record NotificationMessage
{
    /// <summary>Discrimina o conteúdo de <see cref="Data"/>. Ver <see cref="NotificationTypes"/>.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Destinatário. Um por mensagem — em lote, a falha de um esconderia a do outro.</summary>
    [JsonPropertyName("to")]
    public required string To { get; init; }

    /// <summary>
    /// Acompanha a mensagem do publicador ao envio. O Service Bus entrega **ao menos uma
    /// vez**, então é por este campo que o consumidor reconhece uma reentrega.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public required Guid CorrelationId { get; init; }

    /// <summary>Campos específicos do tipo, resolvidos pelo template correspondente.</summary>
    [JsonPropertyName("data")]
    public required IReadOnlyDictionary<string, string> Data { get; init; }
}

/// <summary>
/// Os tipos que existem. Constante em vez de enum porque atravessa JSON, e enum em JSON
/// já mordeu este projeto quatro vezes.
/// </summary>
public static class NotificationTypes
{
    public const string EmailConfirmation = "email.confirmation";
    public const string PasswordReset = "email.password-reset";
    public const string TenantInvitation = "email.tenant-invitation";

    public static readonly IReadOnlyList<string> All =
        [EmailConfirmation, PasswordReset, TenantInvitation];
}

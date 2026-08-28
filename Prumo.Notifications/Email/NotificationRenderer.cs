using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Prumo.Notifications.Contracts;

namespace Prumo.Notifications.Email;

/// <summary>
/// Transforma a mensagem da fila no e-mail que vai sair.
///
/// Substituição de <c>{{chave}}</c> e nada mais — são três e-mails, não vale uma engine de
/// template. **Placeholder que sobra é exceção**, não texto: um e-mail dizendo
/// "clique em {{link}}" é pior que e-mail nenhum, porque parece que funcionou.
/// </summary>
public sealed partial class NotificationRenderer
{
    private static readonly Dictionary<string, (string Template, string Subject)> Templates = new()
    {
        [NotificationTypes.EmailConfirmation] = ("email-confirmation.html", "Confirme seu e-mail — Prumo"),
        [NotificationTypes.PasswordReset] = ("password-reset.html", "Redefinir sua senha — Prumo"),
        [NotificationTypes.TenantInvitation] = ("tenant-invitation.html", "Você foi adicionado a uma empresa no Prumo")
    };

    private readonly Dictionary<string, string> _cache = [];

    public OutboundEmail Render(NotificationMessage message)
    {
        if (!Templates.TryGetValue(message.Type, out var template))
        {
            throw new NotSupportedException(
                $"Tipo de notificação desconhecido: '{message.Type}'. Conhecidos: {string.Join(", ", NotificationTypes.All)}.");
        }

        var body = LoadTemplate(template.Template);

        foreach (var (key, value) in message.Data)
        {
            // HtmlEncode nos valores: nome de usuário é dado de entrada, e um "<" solto
            // quebraria o layout na melhor das hipóteses.
            body = body.Replace($"{{{{{key}}}}}", WebUtility.HtmlEncode(value));
        }

        var leftover = PlaceholderPattern().Match(body);
        if (leftover.Success)
        {
            throw new InvalidOperationException(
                $"O template '{template.Template}' ficou com o placeholder {leftover.Value} sem valor. "
                + $"A mensagem {message.Type} trouxe: {string.Join(", ", message.Data.Keys)}.");
        }

        return new OutboundEmail(message.To, template.Subject, body);
    }

    private string LoadTemplate(string fileName)
    {
        if (_cache.TryGetValue(fileName, out var cached))
            return cached;

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"Prumo.Notifications.Templates.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Template '{resourceName}' não está embutido no assembly. "
                + "Confira o ItemGroup de EmbeddedResource no csproj.");

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        _cache[fileName] = content;
        return content;
    }

    [GeneratedRegex(@"\{\{[a-zA-Z0-9_]+\}\}")]
    private static partial Regex PlaceholderPattern();
}

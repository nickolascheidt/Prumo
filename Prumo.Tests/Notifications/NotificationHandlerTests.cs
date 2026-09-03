using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Prumo.Notifications;
using Prumo.Notifications.Contracts;
using Prumo.Notifications.Email;

namespace Prumo.Tests.Notifications;

/// <summary>
/// A lógica de notificação vivia dentro dos handlers do `ServiceBusProcessor` e por isso
/// nunca teve teste. Extraída para o `NotificationHandler`, é o que este arquivo cobre —
/// os workers em si ficam sem teste de propósito: depois da extração, o que sobra neles é
/// tradução de `NotificationOutcome` para a API da fila, e testar isso seria testar o SDK.
/// </summary>
public class NotificationHandlerTests
{
    private static NotificationHandler MakeSut(
        IEmailSender sender,
        IMemoryCache? cache = null,
        TimeSpan? duplicateWindow = null) =>
        new(new NotificationRenderer(),
            sender,
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            NullLogger<NotificationHandler>.Instance,
            duplicateWindow);

    /// <summary>
    /// Cada template tem placeholders próprios, e placeholder que sobra é exceção no
    /// `NotificationRenderer`. Este helper devolve o `Data` que cada tipo exige.
    /// </summary>
    private static string MakeBody(string type, Guid? correlationId = null)
    {
        IReadOnlyDictionary<string, string> data = type switch
        {
            NotificationTypes.EmailConfirmation =>
                new Dictionary<string, string> { ["name"] = "Ana", ["link"] = "https://prumo.test/confirmar" },
            NotificationTypes.PasswordReset =>
                new Dictionary<string, string> { ["link"] = "https://prumo.test/redefinir" },
            NotificationTypes.TenantInvitation =>
                new Dictionary<string, string>
                {
                    ["tenantName"] = "Acme",
                    ["invitedBy"] = "Nickolas",
                    ["link"] = "https://prumo.test/convite"
                },
            _ => new Dictionary<string, string>()
        };

        return JsonSerializer.Serialize(new NotificationMessage
        {
            Type = type,
            To = "alguem@exemplo.com",
            CorrelationId = correlationId ?? Guid.NewGuid(),
            Data = data
        });
    }

    [Theory]
    [InlineData(NotificationTypes.EmailConfirmation)]
    [InlineData(NotificationTypes.PasswordReset)]
    [InlineData(NotificationTypes.TenantInvitation)]
    public async Task Os_tres_tipos_renderizam_e_saem_pelo_sender(string type)
    {
        var sender = Substitute.For<IEmailSender>();
        var sut = MakeSut(sender);

        var outcome = await sut.HandleAsync(MakeBody(type));

        Assert.IsType<NotificationOutcome.Handled>(outcome);
        await sender.Received(1).SendAsync(
            Arg.Is<OutboundEmail>(e => e.To == "alguem@exemplo.com" && e.HtmlBody.Length > 0),
            Arg.Any<CancellationToken>());
    }
}

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
    /// `NotificationRenderer`. Este helper devolve o `Data` que cada tipo exige — e é a
    /// única fonte dos valores, para não duplicar as URLs de teste na asserção.
    /// </summary>
    private static IReadOnlyDictionary<string, string> DataFor(string type) => type switch
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

    private static string MakeBody(string type, Guid? correlationId = null) =>
        JsonSerializer.Serialize(new NotificationMessage
        {
            Type = type,
            To = "alguem@exemplo.com",
            CorrelationId = correlationId ?? Guid.NewGuid(),
            Data = DataFor(type)
        });

    [Theory]
    [InlineData(NotificationTypes.EmailConfirmation, "Confirme seu e-mail — Prumo")]
    [InlineData(NotificationTypes.PasswordReset, "Redefinir sua senha — Prumo")]
    [InlineData(NotificationTypes.TenantInvitation, "Você foi adicionado a uma empresa no Prumo")]
    public async Task Os_tres_tipos_renderizam_e_saem_pelo_sender(string type, string expectedSubject)
    {
        var sender = Substitute.For<IEmailSender>();
        var sut = MakeSut(sender);
        var expectedLink = DataFor(type)["link"];

        var outcome = await sut.HandleAsync(MakeBody(type));

        Assert.IsType<NotificationOutcome.Handled>(outcome);
        await sender.Received(1).SendAsync(
            Arg.Is<OutboundEmail>(e =>
                e.To == "alguem@exemplo.com" &&
                e.Subject == expectedSubject &&
                e.HtmlBody.Contains(expectedLink)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reentrega_do_mesmo_correlation_id_na_janela_nao_envia_de_novo()
    {
        var sender = Substitute.For<IEmailSender>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = MakeSut(sender, cache);
        var correlationId = Guid.NewGuid();
        var body = MakeBody(NotificationTypes.EmailConfirmation, correlationId);

        var first = await sut.HandleAsync(body);
        var second = await sut.HandleAsync(body);

        Assert.IsType<NotificationOutcome.Handled>(first);
        Assert.IsType<NotificationOutcome.Duplicate>(second);
        await sender.Received(1).SendAsync(Arg.Any<OutboundEmail>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Fora da janela, a mesma mensagem sai de novo — a janela é anti-duplicata de
    /// reentrega, não idempotência eterna. A janela é injetável exatamente para este
    /// teste; em produção vale o `DefaultDuplicateWindow`.
    /// </summary>
    [Fact]
    public async Task Mesmo_correlation_id_fora_da_janela_envia_de_novo()
    {
        var sender = Substitute.For<IEmailSender>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = MakeSut(sender, cache, TimeSpan.FromMilliseconds(50));
        var body = MakeBody(NotificationTypes.EmailConfirmation, Guid.NewGuid());

        await sut.HandleAsync(body);
        await Task.Delay(200);
        var second = await sut.HandleAsync(body);

        Assert.IsType<NotificationOutcome.Handled>(second);
        await sender.Received(2).SendAsync(Arg.Any<OutboundEmail>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Corpo_que_nao_e_json_e_veneno()
    {
        var sender = Substitute.For<IEmailSender>();
        var sut = MakeSut(sender);

        var outcome = await sut.HandleAsync("isto não é json");

        var poison = Assert.IsType<NotificationOutcome.Poison>(outcome);
        Assert.Equal("InvalidJson", poison.Reason);
        await sender.DidNotReceive().SendAsync(Arg.Any<OutboundEmail>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Corpo_que_desserializa_para_null_e_veneno()
    {
        var sender = Substitute.For<IEmailSender>();
        var sut = MakeSut(sender);

        var outcome = await sut.HandleAsync("null");

        var poison = Assert.IsType<NotificationOutcome.Poison>(outcome);
        Assert.Equal("EmptyBody", poison.Reason);
    }

    /// <summary>
    /// Tipo desconhecido é `NotSupportedException` no renderer, e template com placeholder
    /// sem valor é `InvalidOperationException`. Os dois são defeito de código ou de
    /// contrato: retry não conserta nenhum, então vão para a dead-letter.
    /// </summary>
    [Fact]
    public async Task Tipo_desconhecido_e_veneno()
    {
        var sender = Substitute.For<IEmailSender>();
        var sut = MakeSut(sender);

        var outcome = await sut.HandleAsync(MakeBody("email.tipo-que-nao-existe"));

        var poison = Assert.IsType<NotificationOutcome.Poison>(outcome);
        Assert.Equal("RenderFailed", poison.Reason);
        await sender.DidNotReceive().SendAsync(Arg.Any<OutboundEmail>(), Arg.Any<CancellationToken>());
    }
}

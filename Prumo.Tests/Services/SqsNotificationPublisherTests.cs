using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Prumo.Infrastructure.Services;
using Prumo.Notifications.Contracts;

namespace Prumo.Tests.Services;

public class SqsNotificationPublisherTests
{
    private const string QueueUrl = "http://localhost:9324/000000000000/notifications";

    private static NotificationMessage MakeMessage(Guid correlationId) => new()
    {
        Type = NotificationTypes.TenantInvitation,
        To = "alguem@exemplo.com",
        CorrelationId = correlationId,
        Data = new Dictionary<string, string> { ["tenantName"] = "Acme" }
    };

    /// <summary>
    /// Mesma intenção do `ServiceBusNotificationPublisher`: tipo e correlação sobem para
    /// atributos da mensagem, para dar pra inspecionar uma fila parada sem desserializar
    /// o corpo.
    /// </summary>
    [Fact]
    public async Task PublishAsync_serializa_o_envelope_e_poe_tipo_e_correlacao_nos_atributos()
    {
        SendMessageRequest? captured = null;
        var sqs = Substitute.For<IAmazonSQS>();
        sqs.SendMessageAsync(Arg.Do<SendMessageRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new SendMessageResponse());

        var correlationId = Guid.NewGuid();
        var sut = new SqsNotificationPublisher(sqs, QueueUrl, NullLogger<SqsNotificationPublisher>.Instance);

        await sut.PublishAsync(MakeMessage(correlationId));

        Assert.NotNull(captured);
        Assert.Equal(QueueUrl, captured!.QueueUrl);

        var roundTrip = JsonSerializer.Deserialize<NotificationMessage>(captured.MessageBody);
        Assert.NotNull(roundTrip);
        Assert.Equal(NotificationTypes.TenantInvitation, roundTrip!.Type);
        Assert.Equal(correlationId, roundTrip.CorrelationId);
        Assert.Equal("alguem@exemplo.com", roundTrip.To);

        Assert.True(captured.MessageAttributes.TryGetValue("Type", out var typeAttr));
        Assert.Equal(NotificationTypes.TenantInvitation, typeAttr.StringValue);
        Assert.Equal("String", typeAttr.DataType);

        Assert.True(captured.MessageAttributes.TryGetValue("CorrelationId", out var correlationAttr));
        Assert.Equal(correlationId.ToString(), correlationAttr.StringValue);
    }
}

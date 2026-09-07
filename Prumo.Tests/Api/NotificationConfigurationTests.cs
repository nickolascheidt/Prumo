using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Prumo.Api.Configuration;
using Prumo.Infrastructure.Services;

namespace Prumo.Tests.Api;

/// <summary>
/// O `LoggingNotificationPublisher` **descarta** a notificação. Isso é uma comodidade de
/// desenvolvimento — rodar a API para mexer numa tela sem subir fila nenhuma —, e fora de
/// desenvolvimento é perda silenciosa de confirmação de cadastro e de reset de senha, que
/// é a mesma classe de falha que já custou meses neste projeto no sink do Serilog.
///
/// Estes testes fixam a fronteira: em Development degrada, fora dele estoura no startup.
/// </summary>
public class NotificationConfigurationTests
{
    private const string RealQueue = "https://sqs.us-east-1.amazonaws.com/123456789012/notifications";
    private const string EmulatorQueue = "http://localhost:9324/000000000000/notifications";

    private static IHostEnvironment Environment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return environment;
    }

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static INotificationPublisher Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotificationConfiguration(configuration, environment);

        return services.BuildServiceProvider().GetRequiredService<INotificationPublisher>();
    }

    [Fact]
    public void Development_sem_fila_configurada_degrada_para_o_publisher_que_descarta()
    {
        var publisher = Resolve(
            Config(("Notifications:Provider", "Sqs")),
            Environment("Development"));

        Assert.IsType<LoggingNotificationPublisher>(publisher);
    }

    [Fact]
    public void Development_aceita_o_emulador_local()
    {
        var publisher = Resolve(
            Config(
                ("Notifications:Provider", "Sqs"),
                ("Sqs:QueueUrl", EmulatorQueue),
                ("Sqs:ServiceUrl", "http://localhost:9324")),
            Environment("Development"));

        Assert.IsType<SqsNotificationPublisher>(publisher);
    }

    [Fact]
    public void Production_sem_fila_configurada_estoura_em_vez_de_descartar()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Resolve(
            Config(("Notifications:Provider", "Sqs")),
            Environment("Production")));

        Assert.Contains("Sqs:QueueUrl", exception.Message);
    }

    /// <summary>
    /// A armadilha que motivou estes testes: `Sqs:ServiceUrl` preenchido aponta o SDK para
    /// o ElasticMQ **e** troca a cadeia de credenciais padrão — a role da task — por duas
    /// strings fixas. Herdado do `appsettings.json` base, produção publicaria para
    /// localhost com credencial de mentira, e cada publish falharia longe do startup.
    /// </summary>
    [Fact]
    public void Production_com_ServiceUrl_do_emulador_estoura_no_startup()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Resolve(
            Config(
                ("Notifications:Provider", "Sqs"),
                ("Sqs:QueueUrl", RealQueue),
                ("Sqs:ServiceUrl", "http://localhost:9324")),
            Environment("Production")));

        Assert.Contains("Sqs:ServiceUrl", exception.Message);
    }

    [Fact]
    public void Production_com_a_fila_real_registra_o_publisher_do_Sqs()
    {
        var publisher = Resolve(
            Config(
                ("Notifications:Provider", "Sqs"),
                ("Sqs:QueueUrl", RealQueue),
                ("Sqs:Region", "us-east-1")),
            Environment("Production"));

        Assert.IsType<SqsNotificationPublisher>(publisher);
    }

    /// <summary>
    /// A guarda é sobre descartar notificação em produção, não sobre SQS: o provedor
    /// antigo cai na mesma regra enquanto existir.
    /// </summary>
    [Fact]
    public void Production_com_ServiceBus_sem_connection_string_tambem_estoura()
    {
        Assert.Throws<InvalidOperationException>(() => Resolve(
            Config(("Notifications:Provider", "ServiceBus")),
            Environment("Production")));
    }
}

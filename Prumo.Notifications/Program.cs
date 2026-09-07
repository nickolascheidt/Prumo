using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmailV2;
using Amazon.SQS;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Caching.Memory;
using Prumo.Notifications;
using Prumo.Notifications.Email;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<NotificationRenderer>();

// Fábrica explícita: o container embutido não honra valor default de parâmetro de
// construtor, e o `duplicateWindow` do handler é opcional (só os testes o passam).
builder.Services.AddSingleton(sp => new NotificationHandler(
    sp.GetRequiredService<NotificationRenderer>(),
    sp.GetRequiredService<IEmailSender>(),
    sp.GetRequiredService<IMemoryCache>(),
    sp.GetRequiredService<ILogger<NotificationHandler>>()));

// --- e-mail ---
// Em desenvolvimento vai para disco, o que permite verificar o fluxo inteiro sem provedor,
// sem domínio e sem custo. Em produção, ACS ou SES conforme a nuvem.
var emailProvider = builder.Configuration["Email:Provider"] ?? "File";

// O sender de arquivo grava o e-mail num diretório e não avisa ninguém. Em desenvolvimento
// é o ponto; fora dele é o mesmo descarte silencioso que a API se recusa a fazer.
if (!builder.Environment.IsDevelopment()
    && string.Equals(emailProvider, "File", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"Email:Provider=File em {builder.Environment.EnvironmentName}. O sender de arquivo "
        + "escreve em disco e ninguém recebe o e-mail. Configure Ses (ou Acs).");
}

if (string.Equals(emailProvider, "Acs", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<AcsEmailSenderOptions>(builder.Configuration.GetSection("Email:Acs"));
    builder.Services.AddSingleton<IEmailSender, AcsEmailSender>();
}
else if (string.Equals(emailProvider, "Ses", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<SesEmailSenderOptions>(builder.Configuration.GetSection("Email:Ses"));
    builder.Services.AddSingleton<IAmazonSimpleEmailServiceV2>(_ => new AmazonSimpleEmailServiceV2Client());
    builder.Services.AddSingleton<IEmailSender, SesEmailSender>();
}
else
{
    builder.Services.Configure<FileEmailSenderOptions>(builder.Configuration.GetSection("Email:File"));
    builder.Services.AddSingleton<IEmailSender, FileEmailSender>();
}

// --- fila ---
// A chave de decisão é `Notifications:Provider`, e não a connection string de um provedor
// específico: com duas filas possíveis, "tem connection string do Service Bus?" deixa de
// ser pergunta suficiente.
var queueProvider = builder.Configuration["Notifications:Provider"] ?? "ServiceBus";

if (string.Equals(queueProvider, "Sqs", StringComparison.OrdinalIgnoreCase))
{
    var queueUrl = builder.Configuration["Sqs:QueueUrl"];

    if (string.IsNullOrWhiteSpace(queueUrl))
    {
        throw new InvalidOperationException(
            "Notifications:Provider=Sqs mas Sqs:QueueUrl não está configurada. O serviço de "
            + "notificação não tem o que fazer sem fila — suba o ElasticMQ "
            + "(docker compose --profile notifications up -d) ou aponte para a fila real.");
    }

    if (string.IsNullOrWhiteSpace(builder.Configuration["Sqs:DeadLetterQueueUrl"]))
    {
        throw new InvalidOperationException(
            "Notifications:Provider=Sqs mas Sqs:DeadLetterQueueUrl não está configurada. "
            + "Sem ela a mensagem envenenada não tem para onde ir: o publish na DLQ falharia "
            + "e ela voltaria para a fila a cada reentrega, para sempre.");
    }

    // Mesma armadilha da API: `Sqs:ServiceUrl` preenchida aponta o SDK para o ElasticMQ e
    // ainda troca a credencial padrão — a role da task — por uma fixa de emulador.
    var serviceUrl = builder.Configuration["Sqs:ServiceUrl"];

    if (!builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(serviceUrl))
    {
        throw new InvalidOperationException(
            $"Sqs:ServiceUrl está preenchida ('{serviceUrl}') em {builder.Environment.EnvironmentName}. "
            + "Ela existe para apontar o SDK ao ElasticMQ local. Deixe-a vazia para falar "
            + "com o SQS real.");
    }

    builder.Services.Configure<SqsNotificationWorkerOptions>(builder.Configuration.GetSection("Sqs"));
    builder.Services.AddSingleton(_ => CreateSqsClient(builder.Configuration));
    builder.Services.AddHostedService<SqsNotificationWorker>();
}
else
{
    var connectionString = builder.Configuration["ServiceBus:ConnectionString"]
        ?? throw new InvalidOperationException(
            "ServiceBus:ConnectionString não configurada. O serviço de notificação não tem o que "
            + "fazer sem fila — aponte para o recurso real ou troque Notifications:Provider para Sqs.");

    var queueName = builder.Configuration["ServiceBus:QueueName"] ?? "notifications";

    builder.Services.AddSingleton(_ => new ServiceBusClient(connectionString));
    builder.Services.AddSingleton(sp => sp.GetRequiredService<ServiceBusClient>().CreateProcessor(
        queueName,
        new ServiceBusProcessorOptions
        {
            // Nós completamos a mensagem depois do envio. Com autocomplete, uma falha de
            // envio ainda assim removeria a mensagem da fila.
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 4
        }));

    builder.Services.AddHostedService<ServiceBusNotificationWorker>();
}

var host = builder.Build();
host.Run();

// Duplicada, de propósito, em `Prumo.Api/Configuration/NotificationConfiguration.cs`:
// são composition roots de processos diferentes, e o único projeto comum aos dois é o de
// contratos, que não deve carregar o SDK da AWS. Ver o mapa de arquivos do plano
// `docs/superpowers/plans/2026-09-03-fila-e-email-portaveis.md`.
static IAmazonSQS CreateSqsClient(IConfiguration configuration)
{
    var serviceUrl = configuration["Sqs:ServiceUrl"];
    var region = configuration["Sqs:Region"] ?? "us-east-1";
    var config = new AmazonSQSConfig();

    if (string.IsNullOrWhiteSpace(serviceUrl))
    {
        config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
        // Credencial pela cadeia padrão do SDK: variável de ambiente, perfil, role da task.
        return new AmazonSQSClient(config);
    }

    // ServiceURL e RegionEndpoint são **mutuamente exclusivos** no SDK: atribuir um anula
    // o outro. Por isso a região local vai em AuthenticationRegion, que só participa da
    // assinatura e não do roteamento.
    config.ServiceURL = serviceUrl;
    config.AuthenticationRegion = region;

    // O ElasticMQ não valida credencial, mas o SDK recusa assinar sem uma. Estas duas
    // strings existem só para isso, e nunca saem da máquina de desenvolvimento.
    return new AmazonSQSClient(new BasicAWSCredentials("local", "local"), config);
}

using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Caching.Memory;
using Prumo.Notifications;
using Prumo.Notifications.Email;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration["ServiceBus:ConnectionString"]
    ?? throw new InvalidOperationException(
        "ServiceBus:ConnectionString não configurada. O serviço de notificação não tem o que "
        + "fazer sem fila — suba o emulador (docker compose up -d) ou aponte para o recurso real.");

var queueName = builder.Configuration["ServiceBus:QueueName"] ?? "notifications";

builder.Services.AddSingleton(_ => new ServiceBusClient(connectionString));
builder.Services.AddSingleton(sp => sp.GetRequiredService<ServiceBusClient>().CreateProcessor(
    queueName,
    new ServiceBusProcessorOptions
    {
        // Nós completamos a mensagem depois do envio. Com autocomplete, uma falha de envio
        // ainda assim removeria a mensagem da fila.
        AutoCompleteMessages = false,
        MaxConcurrentCalls = 4
    }));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<NotificationRenderer>();
// Fábrica explícita: o container embutido não honra valor default de parâmetro de
// construtor, e o `duplicateWindow` do handler é opcional (só os testes o passam).
builder.Services.AddSingleton(sp => new NotificationHandler(
    sp.GetRequiredService<NotificationRenderer>(),
    sp.GetRequiredService<IEmailSender>(),
    sp.GetRequiredService<IMemoryCache>(),
    sp.GetRequiredService<ILogger<NotificationHandler>>()));

// Em produção sai por Azure Communication Services; em desenvolvimento vai para disco, o
// que permite verificar todo o item 8 sem provedor, sem domínio e sem custo.
var provider = builder.Configuration["Email:Provider"] ?? "File";

if (string.Equals(provider, "Acs", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<AcsEmailSenderOptions>(builder.Configuration.GetSection("Email:Acs"));
    builder.Services.AddSingleton<IEmailSender, AcsEmailSender>();
}
else
{
    builder.Services.Configure<FileEmailSenderOptions>(builder.Configuration.GetSection("Email:File"));
    builder.Services.AddSingleton<IEmailSender, FileEmailSender>();
}

builder.Services.AddHostedService<ServiceBusNotificationWorker>();

var host = builder.Build();
host.Run();

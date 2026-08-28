using Azure.Messaging.ServiceBus;
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

builder.Services.AddHostedService<NotificationWorker>();

var host = builder.Build();
host.Run();

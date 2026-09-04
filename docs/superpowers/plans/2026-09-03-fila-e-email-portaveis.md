# Fila e e-mail portáveis — plano de implementação (fase 1)

> **Para quem executa com agente:** SUB-SKILL OBRIGATÓRIA — use
> `superpowers:subagent-driven-development` (recomendado) ou `superpowers:executing-plans`
> para executar tarefa a tarefa. Os passos usam checkbox (`- [ ]`) para acompanhamento.

**Objetivo:** tirar do código a dependência dura de Azure no caminho de notificação, fazendo
a nuvem virar configuração (`Notifications:Provider`), com SQS/SES convivendo com Service
Bus/ACS — sem Terraform, sem conta AWS, verificável offline no `docker compose`.

**Arquitetura:** a lógica de notificação sai dos handlers do `ServiceBusProcessor` para um
`NotificationHandler` testável, que devolve `NotificationOutcome` (`Handled`/`Duplicate`/
`Poison`). Dois workers finos traduzem esse resultado para a API da sua fila. Do lado do
publicador, `SqsNotificationPublisher` espelha o `ServiceBusNotificationPublisher` atrás do
`INotificationPublisher` que já existe. Localmente, ElasticMQ (~20 MB) substitui o emulador
do Service Bus + SQL Server (~2 GB).

**Tech stack:** .NET 10, xUnit + NSubstitute, AWSSDK.SQS 4.0.100.11,
AWSSDK.SimpleEmailV2 4.0.104, ElasticMQ (`softwaremill/elasticmq-native`).

**Spec:** `docs/superpowers/specs/2026-09-02-fila-e-email-portaveis-design.md`. Onde este
plano se afasta dela, há uma nota explícita dizendo por quê.

**Baseline verificado em 2026-09-03:** `dotnet test` → 119 testes, 0 falhas. Toda tarefa
termina com a suíte verde e sem regressão nesse número (só crescendo).

**Branch:** `feature/portable-queue-and-email`, que já contém o commit da spec.

---

## Mapa de arquivos

**Criar**
- `Prumo.Notifications/NotificationOutcome.cs` — o resultado que as duas filas têm em comum.
- `Prumo.Notifications/NotificationHandler.cs` — toda a lógica de notificação, testável.
- `Prumo.Notifications/SqsNotificationWorker.cs` — laço de long polling + tradução do outcome.
- `Prumo.Notifications/Email/SesEmailSender.cs` — terceiro `IEmailSender`.
- `Prumo.Infrastructure/Services/SqsNotificationPublisher.cs` — publicador SQS.
- `Prumo.Tests/Notifications/NotificationHandlerTests.cs` — o TDD desta fase.
- `Prumo.Tests/Services/SqsNotificationPublisherTests.cs`.
- `elasticmq/elasticmq.conf` — declara `notifications` e `notifications-dlq` na subida.

**Modificar**
- `Prumo.Notifications/NotificationWorker.cs` → renomeado para
  `ServiceBusNotificationWorker.cs`, com o miolo removido.
- `Prumo.Notifications/Program.cs` — escolha de provedor de fila e de e-mail.
- `Prumo.Notifications/Prumo.Notifications.csproj` — pacotes AWS.
- `Prumo.Notifications/appsettings.json` — seções `Notifications`, `Sqs`, `Email:Ses`.
- `Prumo.Infrastructure/Prumo.Infrastructure.csproj` — AWSSDK.SQS.
- `Prumo.Api/Configuration/NotificationConfiguration.cs` — escolha por `Notifications:Provider`.
- `Prumo.Api/appsettings.json` — seções `Notifications` e `Sqs`.
- `Prumo.Tests/Prumo.Tests.csproj` — `ProjectReference` para `Prumo.Notifications`.
- `docker-compose.yml` — sai `servicebus` + `mssql`, entra `elasticmq`.

**Apagar**
- `servicebus/config.json` (e o diretório `servicebus/`).

### Duas decisões de estrutura, para não serem "consertadas" por engano

1. **A construção do `IAmazonSQS` aparece duas vezes** — em `Prumo.Api/Configuration/
   NotificationConfiguration.cs` e em `Prumo.Notifications/Program.cs`. Não é descuido.
   Os dois são composition roots de **processos diferentes**, e o único projeto que ambos
   enxergam é `Prumo.Notifications.Contracts`, que é o envelope puro — pôr o SDK da AWS
   dentro dele arrastaria a dependência para todo consumidor do contrato. Dez linhas
   duplicadas entre dois executáveis custam menos que isso.

2. **`Prumo.Infrastructure` continua referenciando `Azure.Messaging.ServiceBus`.** É a
   decisão 1 da spec: conviver, não substituir. O pacote só sai quando o Service Bus sair.

---

## Task 1: `NotificationOutcome` e o caminho feliz do `NotificationHandler`

**Arquivos:**
- Criar: `Prumo.Notifications/NotificationOutcome.cs`
- Criar: `Prumo.Notifications/NotificationHandler.cs`
- Criar: `Prumo.Tests/Notifications/NotificationHandlerTests.cs`
- Modificar: `Prumo.Tests/Prumo.Tests.csproj`

- [ ] **Passo 1: escrever o teste que falha**

Criar `Prumo.Tests/Notifications/NotificationHandlerTests.cs`:

```csharp
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
```

Adicionar a referência de projeto em `Prumo.Tests/Prumo.Tests.csproj`, no `ItemGroup` que
já tem os outros `ProjectReference` (o projeto de testes ainda não enxerga
`Prumo.Notifications` — hoje só chega a `Prumo.Notifications.Contracts`, por transitividade
de `Prumo.Infrastructure`):

```xml
    <ProjectReference Include="..\Prumo.Notifications\Prumo.Notifications.csproj" />
```

- [ ] **Passo 2: rodar e ver falhar**

Rodar: `dotnet test --filter "FullyQualifiedName~NotificationHandlerTests"`
Esperado: **falha de compilação** — `CS0246: O tipo ou nome do namespace
"NotificationHandler" não foi encontrado`. Compilar é o vermelho aqui.

- [ ] **Passo 3: implementação mínima**

Criar `Prumo.Notifications/NotificationOutcome.cs`:

```csharp
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
```

Criar `Prumo.Notifications/NotificationHandler.cs`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Prumo.Notifications.Contracts;
using Prumo.Notifications.Email;

namespace Prumo.Notifications;

/// <summary>
/// Toda a lógica de notificação, sem uma linha de SDK de fila. Recebe o corpo bruto da
/// mensagem e devolve o que aconteceu; quem traduz isso para a fila é o worker.
/// </summary>
public sealed class NotificationHandler
{
    /// <summary>
    /// A fila entrega ao menos uma vez. Para e-mail, duplicar é irritante e não perigoso,
    /// então uma janela em memória basta — tabela nova custaria mais que o problema que
    /// resolve. Reinício do serviço zera a janela, e é aceitável.
    /// </summary>
    public static readonly TimeSpan DefaultDuplicateWindow = TimeSpan.FromMinutes(10);

    private readonly NotificationRenderer _renderer;
    private readonly IEmailSender _sender;
    private readonly IMemoryCache _seen;
    private readonly ILogger<NotificationHandler> _logger;
    private readonly TimeSpan _duplicateWindow;

    public NotificationHandler(
        NotificationRenderer renderer,
        IEmailSender sender,
        IMemoryCache seen,
        ILogger<NotificationHandler> logger,
        TimeSpan? duplicateWindow = null)
    {
        _renderer = renderer;
        _sender = sender;
        _seen = seen;
        _logger = logger;
        _duplicateWindow = duplicateWindow ?? DefaultDuplicateWindow;
    }

    public async Task<NotificationOutcome> HandleAsync(string body, CancellationToken cancellationToken = default)
    {
        var email = _renderer.Render(JsonSerializer.Deserialize<NotificationMessage>(body)!);
        await _sender.SendAsync(email, cancellationToken);
        return new NotificationOutcome.Handled();
    }
}
```

> O `HandleAsync` acima é deliberadamente ingênuo — é o mínimo que faz o teste do caminho
> feliz passar. Duplicata, veneno e propagação de exceção entram nas tarefas 2, 3 e 4,
> cada uma puxada pelo seu próprio teste.

- [ ] **Passo 4: rodar e ver passar**

Rodar: `dotnet test --filter "FullyQualifiedName~NotificationHandlerTests"`
Esperado: **3 aprovados** (um por tipo), 0 falhas.

- [ ] **Passo 5: commit**

```bash
git add Prumo.Notifications/NotificationOutcome.cs Prumo.Notifications/NotificationHandler.cs Prumo.Tests/Notifications/NotificationHandlerTests.cs Prumo.Tests/Prumo.Tests.csproj
git commit -m "feat(notifications): extract NotificationHandler with the happy path"
```

---

## Task 2: janela de duplicata

**Arquivos:**
- Modificar: `Prumo.Notifications/NotificationHandler.cs`
- Modificar: `Prumo.Tests/Notifications/NotificationHandlerTests.cs`

- [ ] **Passo 1: escrever os testes que falham**

Acrescentar a `NotificationHandlerTests`:

```csharp
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
```

- [ ] **Passo 2: rodar e ver falhar**

Rodar: `dotnet test --filter "FullyQualifiedName~NotificationHandlerTests"`
Esperado: `Reentrega_do_mesmo_correlation_id_na_janela_nao_envia_de_novo` **falha** com
`Assert.IsType() Failure: Value is not the expected type` — veio `Handled`, esperava
`Duplicate`. O outro teste passa por vacuidade (ainda não há janela nenhuma).

- [ ] **Passo 3: implementar**

Substituir o corpo de `HandleAsync` em `Prumo.Notifications/NotificationHandler.cs`:

```csharp
    public async Task<NotificationOutcome> HandleAsync(string body, CancellationToken cancellationToken = default)
    {
        var message = JsonSerializer.Deserialize<NotificationMessage>(body)!;

        if (_seen.TryGetValue(message.CorrelationId, out _))
        {
            _logger.LogInformation(
                "Notificação {CorrelationId} já enviada nesta janela; ignorando reentrega.",
                message.CorrelationId);
            return new NotificationOutcome.Duplicate();
        }

        var email = _renderer.Render(message);
        await _sender.SendAsync(email, cancellationToken);

        _seen.Set(message.CorrelationId, true, _duplicateWindow);
        return new NotificationOutcome.Handled();
    }
```

- [ ] **Passo 4: rodar e ver passar**

Rodar: `dotnet test --filter "FullyQualifiedName~NotificationHandlerTests"`
Esperado: **5 aprovados**, 0 falhas.

- [ ] **Passo 5: commit**

```bash
git add Prumo.Notifications/NotificationHandler.cs Prumo.Tests/Notifications/NotificationHandlerTests.cs
git commit -m "feat(notifications): honour the duplicate window in the handler"
```

---

## Task 3: os três casos de veneno

**Arquivos:**
- Modificar: `Prumo.Notifications/NotificationHandler.cs`
- Modificar: `Prumo.Tests/Notifications/NotificationHandlerTests.cs`

- [ ] **Passo 1: escrever os testes que falham**

Acrescentar a `NotificationHandlerTests`:

```csharp
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
```

- [ ] **Passo 2: rodar e ver falhar**

Rodar: `dotnet test --filter "FullyQualifiedName~NotificationHandlerTests"`
Esperado: os três novos falham com exceção não tratada — `JsonException` no primeiro,
`NullReferenceException` no segundo (o `!` do `Deserialize`) e `NotSupportedException` no
terceiro.

- [ ] **Passo 3: implementar**

Substituir o corpo de `HandleAsync` em `Prumo.Notifications/NotificationHandler.cs`:

```csharp
    public async Task<NotificationOutcome> HandleAsync(string body, CancellationToken cancellationToken = default)
    {
        NotificationMessage? message;

        try
        {
            message = JsonSerializer.Deserialize<NotificationMessage>(body);
        }
        catch (JsonException ex)
        {
            // Corpo que não desserializa não melhora com retry. Vai direto para a
            // dead-letter, com o motivo, em vez de girar até estourar a contagem de entrega.
            _logger.LogError(ex, "Corpo de mensagem não é JSON válido.");
            return new NotificationOutcome.Poison("InvalidJson", ex.Message);
        }

        if (message is null)
            return new NotificationOutcome.Poison("EmptyBody", "O corpo desserializou para null.");

        if (_seen.TryGetValue(message.CorrelationId, out _))
        {
            _logger.LogInformation(
                "Notificação {CorrelationId} já enviada nesta janela; ignorando reentrega.",
                message.CorrelationId);
            return new NotificationOutcome.Duplicate();
        }

        OutboundEmail email;

        try
        {
            email = _renderer.Render(message);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            _logger.LogError(ex, "Notificação {CorrelationId} não pôde ser renderizada.", message.CorrelationId);
            return new NotificationOutcome.Poison("RenderFailed", ex.Message);
        }

        // Falha aqui **sobe**: provedor fora do ar é retry, não veneno. Ver Task 4.
        await _sender.SendAsync(email, cancellationToken);

        _seen.Set(message.CorrelationId, true, _duplicateWindow);
        return new NotificationOutcome.Handled();
    }
```

> **Mudança de comportamento consciente.** O `NotificationWorker` de hoje envolve render
> **e** envio no mesmo `try` com o filtro `NotSupportedException or
> InvalidOperationException` — então um `InvalidOperationException` vindo do *sender* era
> dead-lettered como `RenderFailed`. Aqui só o render está no `try`, e uma exceção do
> sender sobe. É o passo 4 da spec, e é a leitura correta: provedor fora do ar não é
> veneno.

- [ ] **Passo 4: rodar e ver passar**

Rodar: `dotnet test --filter "FullyQualifiedName~NotificationHandlerTests"`
Esperado: **8 aprovados**, 0 falhas.

- [ ] **Passo 5: commit**

```bash
git add Prumo.Notifications/NotificationHandler.cs Prumo.Tests/Notifications/NotificationHandlerTests.cs
git commit -m "feat(notifications): classify poison messages in the handler"
```

---

## Task 4: falha de provedor sobe, e não vira veneno

**Arquivos:**
- Modificar: `Prumo.Tests/Notifications/NotificationHandlerTests.cs`

- [ ] **Passo 1: escrever o teste**

Acrescentar a `NotificationHandlerTests`:

```csharp
    /// <summary>
    /// A distinção de que a fase inteira depende: veneno é `Poison` (dead-letter agora),
    /// provedor fora do ar é exceção (reentrega depois). Se esta linha inverter, um SES
    /// intermitente passa a queimar notificação na DLQ.
    /// </summary>
    [Fact]
    public async Task Falha_do_provedor_de_email_sobe_como_excecao()
    {
        var sender = Substitute.For<IEmailSender>();
        // Task falhada em vez de `throw` dentro do lambda: com throw, o compilador não
        // infere o tipo de retorno e o `Returns` fica ambíguo. Await de Task falhada
        // levanta a mesma exceção.
        sender.SendAsync(Arg.Any<OutboundEmail>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new HttpRequestException("provedor fora do ar")));
        var sut = MakeSut(sender);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => sut.HandleAsync(MakeBody(NotificationTypes.PasswordReset)));
    }

    /// <summary>
    /// E a mensagem que estourou no envio **não** entra na janela de duplicata: se
    /// entrasse, a reentrega seria descartada como duplicata e o e-mail nunca sairia.
    /// </summary>
    [Fact]
    public async Task Mensagem_que_falhou_no_envio_nao_entra_na_janela()
    {
        var sender = Substitute.For<IEmailSender>();
        var falhar = true;
        sender.SendAsync(Arg.Any<OutboundEmail>(), Arg.Any<CancellationToken>())
            .Returns(_ => falhar
                ? Task.FromException(new HttpRequestException("provedor fora do ar"))
                : Task.CompletedTask);
        var sut = MakeSut(sender);
        var body = MakeBody(NotificationTypes.PasswordReset, Guid.NewGuid());

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.HandleAsync(body));
        falhar = false;
        var retry = await sut.HandleAsync(body);

        Assert.IsType<NotificationOutcome.Handled>(retry);
    }
```

- [ ] **Passo 2: rodar**

Rodar: `dotnet test --filter "FullyQualifiedName~NotificationHandlerTests"`
Esperado: **10 aprovados**, 0 falhas — os dois passam sem mudança de produção, porque a
Task 3 já pôs o `_seen.Set` **depois** do envio. Este é um teste de caracterização: ele
existe para quebrar se alguém mover aquela linha para cima.

> Se algum dos dois falhar, **não** é para adaptar o teste: é sinal de que o `_seen.Set`
> ficou antes do `SendAsync` ou que o envio foi envolvido em `try/catch`. Conserte a
> implementação.

- [ ] **Passo 3: commit**

```bash
git add Prumo.Tests/Notifications/NotificationHandlerTests.cs
git commit -m "test(notifications): pin provider failures as retry, not poison"
```

---

## Task 5: `SqsNotificationPublisher`

**Arquivos:**
- Criar: `Prumo.Infrastructure/Services/SqsNotificationPublisher.cs`
- Criar: `Prumo.Tests/Services/SqsNotificationPublisherTests.cs`
- Modificar: `Prumo.Infrastructure/Prumo.Infrastructure.csproj`

- [ ] **Passo 1: escrever o teste que falha**

Criar `Prumo.Tests/Services/SqsNotificationPublisherTests.cs`:

```csharp
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

        Assert.Equal(NotificationTypes.TenantInvitation, captured.MessageAttributes["Type"].StringValue);
        Assert.Equal("String", captured.MessageAttributes["Type"].DataType);
        Assert.Equal(correlationId.ToString(), captured.MessageAttributes["CorrelationId"].StringValue);
    }
}
```

- [ ] **Passo 2: rodar e ver falhar**

Rodar: `dotnet test --filter "FullyQualifiedName~SqsNotificationPublisherTests"`
Esperado: falha de compilação — `CS0246` em `Amazon.SQS` e em `SqsNotificationPublisher`.

- [ ] **Passo 3: implementar**

Em `Prumo.Infrastructure/Prumo.Infrastructure.csproj`, acrescentar ao `ItemGroup` de
`PackageReference`:

```xml
    <PackageReference Include="AWSSDK.SQS" Version="4.0.100.11" />
```

Criar `Prumo.Infrastructure/Services/SqsNotificationPublisher.cs`:

```csharp
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Prumo.Notifications.Contracts;

namespace Prumo.Infrastructure.Services
{
    /// <summary>
    /// Espelho do <see cref="ServiceBusNotificationPublisher"/> para SQS. Os dois existem
    /// ao mesmo tempo de propósito: a escolha de nuvem é `Notifications:Provider`, não
    /// recompilação.
    ///
    /// O <see cref="IAmazonSQS"/> é singleton no DI pelo mesmo motivo que o
    /// <c>ServiceBusClient</c>: é seguro para concorrência e caro de criar.
    /// </summary>
    public sealed class SqsNotificationPublisher : INotificationPublisher
    {
        private readonly IAmazonSQS _sqs;
        private readonly string _queueUrl;
        private readonly ILogger<SqsNotificationPublisher> _logger;

        public SqsNotificationPublisher(
            IAmazonSQS sqs,
            string queueUrl,
            ILogger<SqsNotificationPublisher> logger)
        {
            _sqs = sqs;
            _queueUrl = queueUrl;
            _logger = logger;
        }

        public async Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            var payload = JsonSerializer.Serialize(message);

            await _sqs.SendMessageAsync(
                new SendMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MessageBody = payload,
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        ["Type"] = new() { DataType = "String", StringValue = message.Type },
                        ["CorrelationId"] = new() { DataType = "String", StringValue = message.CorrelationId.ToString() }
                    }
                },
                cancellationToken);

            _logger.LogInformation(
                "Notificação {Type} publicada ({CorrelationId})", message.Type, message.CorrelationId);
        }
    }
}
```

- [ ] **Passo 4: rodar e ver passar**

Rodar: `dotnet test --filter "FullyQualifiedName~SqsNotificationPublisherTests"`
Esperado: **1 aprovado**, 0 falhas.

Depois, a suíte inteira: `dotnet test`
Esperado: **130 aprovados** (119 do baseline + 10 do handler + 1 deste), 0 falhas.

- [ ] **Passo 5: commit**

```bash
git add Prumo.Infrastructure/Services/SqsNotificationPublisher.cs Prumo.Infrastructure/Prumo.Infrastructure.csproj Prumo.Tests/Services/SqsNotificationPublisherTests.cs
git commit -m "feat(notifications): add the SQS publisher next to the Service Bus one"
```

---

## Task 6: afinar o worker do Service Bus

**Arquivos:**
- Renomear: `Prumo.Notifications/NotificationWorker.cs` → `Prumo.Notifications/ServiceBusNotificationWorker.cs`
- Modificar: `Prumo.Notifications/Program.cs`

Sem teste novo: depois da extração, o que sobra no worker é tradução de
`NotificationOutcome` para a API da fila. A garantia aqui é compilar e a suíte seguir verde.

- [ ] **Passo 1: renomear preservando o histórico**

```bash
git mv Prumo.Notifications/NotificationWorker.cs Prumo.Notifications/ServiceBusNotificationWorker.cs
```

- [ ] **Passo 2: reescrever o arquivo**

Conteúdo completo de `Prumo.Notifications/ServiceBusNotificationWorker.cs`:

```csharp
using Azure.Messaging.ServiceBus;

namespace Prumo.Notifications;

/// <summary>
/// Consome a fila do Service Bus e traduz o <see cref="NotificationOutcome"/> para a API
/// dela. A lógica de notificação está no <see cref="NotificationHandler"/> — aqui não há
/// nada específico de e-mail.
/// </summary>
public sealed class ServiceBusNotificationWorker : BackgroundService
{
    // A dead-letter do Service Bus limita motivo a 256 caracteres e descrição a 4096;
    // passar disso é exceção na hora de dead-letterar, o que trocaria uma mensagem
    // ruim por um worker travado.
    private const int MaxReasonLength = 256;
    private const int MaxDetailLength = 4096;

    private readonly ServiceBusProcessor _processor;
    private readonly NotificationHandler _handler;
    private readonly ILogger<ServiceBusNotificationWorker> _logger;

    public ServiceBusNotificationWorker(
        ServiceBusProcessor processor,
        NotificationHandler handler,
        ILogger<ServiceBusNotificationWorker> logger)
    {
        _processor = processor;
        _handler = handler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);
        _logger.LogInformation("Consumindo a fila de notificações (Service Bus).");

        // O processor tem thread própria; aqui é só esperar o desligamento.
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);

        await _processor.StopProcessingAsync(CancellationToken.None);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        // Exceção que sobe daqui não completa a mensagem, e o Service Bus reentrega.
        // É assim que falha de provedor de e-mail vira retry.
        var outcome = await _handler.HandleAsync(args.Message.Body.ToString(), args.CancellationToken);

        switch (outcome)
        {
            case NotificationOutcome.Poison poison:
                await args.DeadLetterMessageAsync(
                    args.Message,
                    Truncate(poison.Reason, MaxReasonLength),
                    Truncate(poison.Detail, MaxDetailLength));
                break;

            default:
                await args.CompleteMessageAsync(args.Message);
                break;
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Falha no processador da fila ({Source}).", args.ErrorSource);
        return Task.CompletedTask;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    public override void Dispose()
    {
        _processor.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
```

- [ ] **Passo 3: atualizar o registro no `Program.cs` do worker**

Em `Prumo.Notifications/Program.cs`, acrescentar ao topo o using que o `IMemoryCache` exige:

```csharp
using Microsoft.Extensions.Caching.Memory;
```

Trocar a linha:

```csharp
builder.Services.AddSingleton<NotificationRenderer>();
```

por:

```csharp
builder.Services.AddSingleton<NotificationRenderer>();
// Fábrica explícita: o container embutido não honra valor default de parâmetro de
// construtor, e o `duplicateWindow` do handler é opcional (só os testes o passam).
builder.Services.AddSingleton(sp => new NotificationHandler(
    sp.GetRequiredService<NotificationRenderer>(),
    sp.GetRequiredService<IEmailSender>(),
    sp.GetRequiredService<IMemoryCache>(),
    sp.GetRequiredService<ILogger<NotificationHandler>>()));
```

E a linha final:

```csharp
builder.Services.AddHostedService<NotificationWorker>();
```

por:

```csharp
builder.Services.AddHostedService<ServiceBusNotificationWorker>();
```

- [ ] **Passo 4: compilar e rodar a suíte**

Rodar: `dotnet build`
Esperado: **compilação bem-sucedida**, sem erro.

Rodar: `dotnet test`
Esperado: **130 aprovados**, 0 falhas.

- [ ] **Passo 5: commit**

```bash
git add Prumo.Notifications/ServiceBusNotificationWorker.cs Prumo.Notifications/Program.cs
git commit -m "refactor(notifications): thin the Service Bus worker down to queue translation"
```

---

## Task 7: `SqsNotificationWorker`

**Arquivos:**
- Criar: `Prumo.Notifications/SqsNotificationWorker.cs`
- Modificar: `Prumo.Notifications/Prumo.Notifications.csproj`

- [ ] **Passo 1: adicionar o pacote**

Em `Prumo.Notifications/Prumo.Notifications.csproj`, no `ItemGroup` de `PackageReference`:

```xml
    <PackageReference Include="AWSSDK.SQS" Version="4.0.100.11" />
```

- [ ] **Passo 2: escrever o worker**

Criar `Prumo.Notifications/SqsNotificationWorker.cs`:

```csharp
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;

namespace Prumo.Notifications;

public sealed class SqsNotificationWorkerOptions
{
    public string QueueUrl { get; set; } = string.Empty;
    public string DeadLetterQueueUrl { get; set; } = string.Empty;
}

/// <summary>
/// Consome a fila SQS por long polling e traduz o <see cref="NotificationOutcome"/> para a
/// API dela. Contraparte do <see cref="ServiceBusNotificationWorker"/>; a lógica de
/// notificação é a mesma <see cref="NotificationHandler"/>.
/// </summary>
public sealed class SqsNotificationWorker : BackgroundService
{
    // O atributo de mensagem do SQS não aceita valor vazio, e mensagem de exceção pode ser
    // enorme. 1024 é folgado para diagnosticar e cabe sobrando no limite de 256 KB.
    private const int MaxAttributeLength = 1024;

    private readonly IAmazonSQS _sqs;
    private readonly NotificationHandler _handler;
    private readonly SqsNotificationWorkerOptions _options;
    private readonly ILogger<SqsNotificationWorker> _logger;

    public SqsNotificationWorker(
        IAmazonSQS sqs,
        NotificationHandler handler,
        IOptions<SqsNotificationWorkerOptions> options,
        ILogger<SqsNotificationWorker> logger)
    {
        _sqs = sqs;
        _handler = handler;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Consumindo a fila de notificações (SQS: {QueueUrl}).", _options.QueueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            ReceiveMessageResponse response;

            try
            {
                response = await _sqs.ReceiveMessageAsync(
                    new ReceiveMessageRequest
                    {
                        QueueUrl = _options.QueueUrl,
                        // Long polling: uma chamada esperando 20s em vez de vinte chamadas
                        // vazias por minuto.
                        WaitTimeSeconds = 20,
                        MaxNumberOfMessages = 10
                    },
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao ler a fila SQS; nova tentativa em 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            // No SDK v4 as coleções de resposta chegam nulas quando vazias, e não como
            // lista vazia. Sem o `?? []` isto é NullReferenceException no primeiro poll
            // sem mensagem — que é a esmagadora maioria deles.
            foreach (var message in response.Messages ?? [])
                await ProcessAsync(message, stoppingToken);
        }
    }

    private async Task ProcessAsync(Message message, CancellationToken cancellationToken)
    {
        NotificationOutcome outcome;

        try
        {
            outcome = await _handler.HandleAsync(message.Body, cancellationToken);
        }
        catch (Exception ex)
        {
            // Falha de provedor é retry: não apaga. O visibility timeout devolve a
            // mensagem e a redrive policy leva para a DLQ depois de maxReceiveCount.
            _logger.LogError(ex, "Falha ao processar {MessageId}; deixando reentregar.", message.MessageId);
            return;
        }

        if (outcome is NotificationOutcome.Poison poison)
        {
            await DeadLetterAsync(message, poison, cancellationToken);
            return;
        }

        await _sqs.DeleteMessageAsync(_options.QueueUrl, message.ReceiptHandle, cancellationToken);
    }

    /// <summary>
    /// O SQS não tem dead-letter nativa como o Service Bus: é publicar na outra fila e
    /// apagar da origem. A ordem importa — publica **antes** de apagar. Se o envio para a
    /// DLQ falhar, a mensagem não é apagada, volta pelo visibility timeout e a redrive
    /// policy acaba levando para a DLQ de qualquer forma. A ordem torna a duplicata
    /// possível e a perda não.
    /// </summary>
    private async Task DeadLetterAsync(Message message, NotificationOutcome.Poison poison, CancellationToken cancellationToken)
    {
        await _sqs.SendMessageAsync(
            new SendMessageRequest
            {
                QueueUrl = _options.DeadLetterQueueUrl,
                MessageBody = message.Body,
                // O mais perto que o SQS chega da dead-letter descrita do Service Bus.
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["PoisonReason"] = new() { DataType = "String", StringValue = Sanitize(poison.Reason) },
                    ["PoisonDetail"] = new() { DataType = "String", StringValue = Sanitize(poison.Detail) }
                }
            },
            cancellationToken);

        await _sqs.DeleteMessageAsync(_options.QueueUrl, message.ReceiptHandle, cancellationToken);

        _logger.LogError(
            "Mensagem {MessageId} enviada para a DLQ: {Reason} — {Detail}",
            message.MessageId, poison.Reason, poison.Detail);
    }

    private static string Sanitize(string value) =>
        string.IsNullOrWhiteSpace(value) ? "-"
        : value.Length <= MaxAttributeLength ? value
        : value[..MaxAttributeLength];
}
```

- [ ] **Passo 3: compilar**

Rodar: `dotnet build`
Esperado: **compilação bem-sucedida**. O worker ainda não está registrado no DI — isso é a
Task 9.

- [ ] **Passo 4: commit**

```bash
git add Prumo.Notifications/SqsNotificationWorker.cs Prumo.Notifications/Prumo.Notifications.csproj
git commit -m "feat(notifications): add the SQS worker with explicit dead-lettering"
```

---

## Task 8: `SesEmailSender`

**Arquivos:**
- Criar: `Prumo.Notifications/Email/SesEmailSender.cs`
- Modificar: `Prumo.Notifications/Prumo.Notifications.csproj`

- [ ] **Passo 1: adicionar o pacote**

Em `Prumo.Notifications/Prumo.Notifications.csproj`, no `ItemGroup` de `PackageReference`:

```xml
    <PackageReference Include="AWSSDK.SimpleEmailV2" Version="4.0.104" />
```

- [ ] **Passo 2: escrever o sender**

Criar `Prumo.Notifications/Email/SesEmailSender.cs`:

```csharp
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Prumo.Notifications.Email;

public sealed class SesEmailSenderOptions
{
    /// <summary>
    /// Remetente. No sandbox do SES, um endereço verificado; fora dele, um do domínio
    /// verificado.
    /// </summary>
    public string SenderAddress { get; set; } = string.Empty;
}

/// <summary>
/// Envia por Amazon SES v2. Terceiro <see cref="IEmailSender"/>, ao lado do
/// <see cref="AcsEmailSender"/> e do <see cref="FileEmailSender"/> — o default de
/// desenvolvimento continua sendo `File`.
///
/// Sem equivalente ao <c>WaitUntil.Started</c> do ACS: o SES já é aceite-e-devolve, e o
/// <c>MessageId</c> da resposta entra no log no lugar do <c>operation.Id</c>.
///
/// Credencial vem da cadeia padrão do SDK — variável de ambiente, perfil, role da task.
/// Esta classe não lê chave de configuração nenhuma, de propósito: em produção é role de
/// IAM, e localmente ela nem é usada.
/// </summary>
public sealed class SesEmailSender : IEmailSender
{
    private readonly IAmazonSimpleEmailServiceV2 _client;
    private readonly string _sender;
    private readonly ILogger<SesEmailSender> _logger;

    public SesEmailSender(
        IAmazonSimpleEmailServiceV2 client,
        IOptions<SesEmailSenderOptions> options,
        ILogger<SesEmailSender> logger)
    {
        if (string.IsNullOrWhiteSpace(options.Value.SenderAddress))
            throw new InvalidOperationException("Email:Ses:SenderAddress não configurado.");

        _client = client;
        _sender = options.Value.SenderAddress;
        _logger = logger;
    }

    public async Task SendAsync(OutboundEmail email, CancellationToken cancellationToken = default)
    {
        var response = await _client.SendEmailAsync(
            new SendEmailRequest
            {
                FromEmailAddress = _sender,
                Destination = new Destination { ToAddresses = [email.To] },
                Content = new EmailContent
                {
                    Simple = new Message
                    {
                        Subject = new Content { Data = email.Subject, Charset = "UTF-8" },
                        Body = new Body { Html = new Content { Data = email.HtmlBody, Charset = "UTF-8" } }
                    }
                }
            },
            cancellationToken);

        _logger.LogInformation("E-mail aceito pelo SES para {To} ({MessageId})", email.To, response.MessageId);
    }
}
```

- [ ] **Passo 3: compilar**

Rodar: `dotnet build`
Esperado: **compilação bem-sucedida**.

> Se aparecer ambiguidade em `Message`, é colisão entre
> `Amazon.SimpleEmailV2.Model.Message` e `Amazon.SQS.Model.Message`: os dois pacotes agora
> vivem no mesmo projeto. Este arquivo só importa o namespace do SES, então não deveria
> acontecer — se acontecer, qualifique como `Amazon.SimpleEmailV2.Model.Message` em vez de
> mexer nos usings do worker.

- [ ] **Passo 4: commit**

```bash
git add Prumo.Notifications/Email/SesEmailSender.cs Prumo.Notifications/Prumo.Notifications.csproj
git commit -m "feat(notifications): add the SES email sender"
```

---

## Task 9: escolha de provedor no worker

**Arquivos:**
- Modificar: `Prumo.Notifications/Program.cs`
- Modificar: `Prumo.Notifications/appsettings.json`

- [ ] **Passo 1: reescrever o `Program.cs`**

> Isto reescreve por cima do que a Task 6 editou, e não é retrabalho: lá o `Program.cs`
> mudou o mínimo para o build seguir verde depois do rename; aqui ele ganha a escolha de
> provedor. Uma tarefa, um build verde.

Conteúdo completo de `Prumo.Notifications/Program.cs`:

```csharp
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
// contratos, que não deve carregar o SDK da AWS. Ver o mapa de arquivos do plano.
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
```

- [ ] **Passo 2: atualizar o `appsettings.json` do worker**

Conteúdo completo de `Prumo.Notifications/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Notifications": {
    "Provider": "Sqs"
  },
  "Sqs": {
    "QueueUrl": "http://localhost:9324/000000000000/notifications",
    "DeadLetterQueueUrl": "http://localhost:9324/000000000000/notifications-dlq",
    "ServiceUrl": "http://localhost:9324",
    "Region": "us-east-1"
  },
  "ServiceBus": {
    "ConnectionString": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
    "QueueName": "notifications"
  },
  "Email": {
    "Provider": "File",
    "File": {
      "Directory": "sent-emails"
    },
    "Acs": {
      "ConnectionString": "",
      "SenderAddress": ""
    },
    "Ses": {
      "SenderAddress": ""
    }
  }
}
```

> `ServiceUrl` preenchido aponta o SDK para o ElasticMQ; vazio, para a AWS real. A seção
> `ServiceBus` **permanece** — trocar de nuvem é mudar `Notifications:Provider` para
> `ServiceBus`, e nada mais.

- [ ] **Passo 3: compilar**

Rodar: `dotnet build`
Esperado: **compilação bem-sucedida**.

- [ ] **Passo 4: commit**

```bash
git add Prumo.Notifications/Program.cs Prumo.Notifications/appsettings.json
git commit -m "feat(notifications): pick the queue and email provider from configuration"
```

---

## Task 10: escolha de provedor na API

**Arquivos:**
- Modificar: `Prumo.Api/Configuration/NotificationConfiguration.cs`
- Modificar: `Prumo.Api/appsettings.json`

- [ ] **Passo 1: reescrever a configuração**

Conteúdo completo de `Prumo.Api/Configuration/NotificationConfiguration.cs`:

```csharp
using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Azure.Messaging.ServiceBus;
using Prumo.Infrastructure.Services;

namespace Prumo.Api.Configuration;

public static class NotificationConfiguration
{
    /// <summary>
    /// Liga a API à fila de notificações, escolhendo o provedor por
    /// `Notifications:Provider`. Sem provedor — ou com o provedor escolhido sem fila
    /// configurada — a aplicação **sobe assim mesmo**, com um publisher que descarta e
    /// avisa: rodar a API para mexer numa tela não deve exigir fila nenhuma.
    /// </summary>
    public static IServiceCollection AddNotificationConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var provider = configuration["Notifications:Provider"];

        if (string.Equals(provider, "Sqs", StringComparison.OrdinalIgnoreCase))
        {
            var queueUrl = configuration["Sqs:QueueUrl"];

            if (!string.IsNullOrWhiteSpace(queueUrl))
            {
                // Singleton: o cliente é seguro para concorrência e caro de criar.
                services.AddSingleton(_ => CreateSqsClient(configuration));
                services.AddSingleton<INotificationPublisher>(sp => new SqsNotificationPublisher(
                    sp.GetRequiredService<IAmazonSQS>(),
                    queueUrl,
                    sp.GetRequiredService<ILogger<SqsNotificationPublisher>>()));

                return services;
            }
        }
        else if (string.Equals(provider, "ServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = configuration["ServiceBus:ConnectionString"];
            var queueName = configuration["ServiceBus:QueueName"] ?? "notifications";

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                // Singleton pelo mesmo motivo: um cliente por requisição abriria uma
                // conexão AMQP por chamada.
                services.AddSingleton(_ => new ServiceBusClient(connectionString));
                services.AddSingleton<INotificationPublisher>(sp => new ServiceBusNotificationPublisher(
                    sp.GetRequiredService<ServiceBusClient>(),
                    queueName,
                    sp.GetRequiredService<ILogger<ServiceBusNotificationPublisher>>()));

                return services;
            }
        }

        services.AddSingleton<INotificationPublisher>(sp => new LoggingNotificationPublisher(
            sp.GetRequiredService<ILogger<LoggingNotificationPublisher>>(),
            includePayload: environment.IsDevelopment()));

        return services;
    }

    /// <summary>
    /// Duplicada, de propósito, em `Prumo.Notifications/Program.cs`. Ver o mapa de
    /// arquivos do plano `2026-09-03-fila-e-email-portaveis.md`.
    /// </summary>
    private static IAmazonSQS CreateSqsClient(IConfiguration configuration)
    {
        var serviceUrl = configuration["Sqs:ServiceUrl"];
        var region = configuration["Sqs:Region"] ?? "us-east-1";
        var config = new AmazonSQSConfig();

        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
            return new AmazonSQSClient(config);
        }

        // ServiceURL e RegionEndpoint são mutuamente exclusivos: atribuir um anula o outro.
        config.ServiceURL = serviceUrl;
        config.AuthenticationRegion = region;

        // O ElasticMQ não valida credencial, mas o SDK recusa assinar sem uma.
        return new AmazonSQSClient(new BasicAWSCredentials("local", "local"), config);
    }
}
```

> **Nota sobre a mensagem do `LoggingNotificationPublisher`.** O texto dele hoje diz
> "ServiceBus:ConnectionString não está configurada", que passou a ser meia verdade. Não é
> desta tarefa mudar — se incomodar, é um commit separado de uma linha.

- [ ] **Passo 2: atualizar o `appsettings.json` da API**

Em `Prumo.Api/appsettings.json`, **antes** do bloco `"ServiceBus"` (linha 12), inserir:

```json
  "Notifications": {
    "Provider": "Sqs"
  },
  "Sqs": {
    "QueueUrl": "http://localhost:9324/000000000000/notifications",
    "ServiceUrl": "http://localhost:9324",
    "Region": "us-east-1"
  },
```

O bloco `"ServiceBus"` fica como está.

- [ ] **Passo 3: compilar e rodar a suíte**

Rodar: `dotnet build`
Esperado: **compilação bem-sucedida**.

Rodar: `dotnet test`
Esperado: **130 aprovados**, 0 falhas.

- [ ] **Passo 4: commit**

```bash
git add Prumo.Api/Configuration/NotificationConfiguration.cs Prumo.Api/appsettings.json
git commit -m "feat(api): choose the notification publisher from configuration"
```

---

## Task 11: trocar o emulador do Service Bus por ElasticMQ

**Arquivos:**
- Criar: `elasticmq/elasticmq.conf`
- Modificar: `docker-compose.yml`
- Apagar: `servicebus/config.json`

- [ ] **Passo 1: criar a configuração do ElasticMQ**

Criar `elasticmq/elasticmq.conf`:

```hocon
include classpath("application.conf")

// As filas nascem com o container: nada de script de bootstrap, e o worker sobe
// encontrando fila e DLQ prontas.
node-address {
    protocol = http
    host = "localhost"
    port = 9324
    context-path = ""
}

rest-sqs {
    enabled = true
    bind-port = 9324
    bind-hostname = "0.0.0.0"
    // strict: rejeita o que o SQS real rejeitaria, para o local não ser mais permissivo
    // que a produção.
    sqs-limits = strict
}

rest-stats {
    enabled = true
    bind-port = 9325
    bind-hostname = "0.0.0.0"
}

queues {
    notifications {
        defaultVisibilityTimeout = 60 seconds
        receiveMessageWait = 20 seconds
        deadLettersQueue {
            name = "notifications-dlq"
            maxReceiveCount = 5
        }
    }
    notifications-dlq { }
}
```

- [ ] **Passo 2: trocar os serviços no `docker-compose.yml`**

Remover os serviços `servicebus` e `mssql` inteiros (com os comentários acima deles) e pôr
no lugar:

```yaml
  # Fila local, no perfil "notifications" — `docker compose up -d` sozinho não a sobe.
  # A API roda sem ela, com um publisher que descarta e avisa no log.
  #   docker compose --profile notifications up -d
  #
  # Substituiu o emulador do Service Bus, que puxava um SQL Server junto e passava de 2 GB
  # para servir um projeto que roda em um container. Efeito colateral bem-vindo: some o
  # ACCEPT_EULA chumbado no compose.
  elasticmq:
    image: softwaremill/elasticmq-native
    container_name: saasbase-elasticmq
    profiles: ["notifications"]
    restart: unless-stopped
    volumes:
      - ./elasticmq/elasticmq.conf:/opt/elasticmq.conf:ro
    ports:
      - "9324:9324"
      - "9325:9325"
```

- [ ] **Passo 3: apagar o que não serve mais**

```bash
git rm servicebus/config.json
```

- [ ] **Passo 4: verificar que a fila sobe**

```bash
docker compose --profile notifications up -d
docker compose ps
```
Esperado: `saasbase-postgres` e `saasbase-elasticmq` em `running`.

```bash
curl -s "http://localhost:9324/?Action=ListQueues"
```
Esperado: XML com `notifications` **e** `notifications-dlq`.

- [ ] **Passo 5: commit**

```bash
git add docker-compose.yml elasticmq/elasticmq.conf
git commit -m "chore(dev): swap the Service Bus emulator for ElasticMQ"
```

---

## Task 12: verificação de ponta a ponta

Nenhum arquivo de produção muda. Esta tarefa existe porque o caminho de notificação já
passou meses falhando em silêncio neste projeto — a suíte verde não substitui ver o
e-mail no disco.

- [ ] **Passo 1: subir a infraestrutura local**

```bash
docker compose --profile notifications up -d
```

- [ ] **Passo 2: subir os dois processos, em terminais separados**

```bash
dotnet run --project Prumo.Api
```

```bash
dotnet run --project Prumo.Notifications
```

Esperado no log do worker:
`Consumindo a fila de notificações (SQS: http://localhost:9324/000000000000/notifications).`

- [ ] **Passo 3: disparar uma notificação real**

Pelo endpoint de esqueci-a-senha, que não exige sessão. Trocar o e-mail pelo de um usuário
que exista no banco local:

```bash
curl -i -X POST http://localhost:5201/api/auth/forgot-password \
  -H "Content-Type: application/json" \
  -d '{"email":"nickolas.scheidt@gmail.com"}'
```

Esperado: **200**, e no log da API `Notificação email.password-reset publicada`.

- [ ] **Passo 4: conferir que o e-mail saiu**

```bash
ls -t Prumo.Notifications/sent-emails | head -3
```
Esperado: um arquivo `.html` novo, com a data de agora. Abrir no navegador e confirmar que
**não há** `{{link}}` cru no corpo — placeholder sobrando significa render quebrado.

- [ ] **Passo 5: conferir o caminho do veneno**

Publicar uma mensagem inválida direto na fila:

```bash
curl -s "http://localhost:9324/?Action=SendMessage&QueueUrl=http://localhost:9324/000000000000/notifications&MessageBody=isto-nao-e-json"
```

Esperado, no log do worker: `Mensagem ... enviada para a DLQ: InvalidJson`. E:

```bash
curl -s "http://localhost:9324/?Action=ReceiveMessage&QueueUrl=http://localhost:9324/000000000000/notifications-dlq"
```
Esperado: a mensagem na DLQ, com os atributos `PoisonReason` e `PoisonDetail`.

- [ ] **Passo 6: suíte inteira, uma última vez**

Rodar: `dotnet test`
Esperado: **130 aprovados**, 0 falhas.

- [ ] **Passo 7: registrar a execução e commitar**

Preencher a seção "Registro de execução" no fim deste plano — o que a execução revelou e
onde o plano errou. É a convenção do repo, e esses registros costumam valer mais que o
plano em si.

```bash
git add docs/superpowers/plans/2026-09-03-fila-e-email-portaveis.md
git commit -m "docs: record what executing the portable queue plan taught"
```

---

## Fora de escopo, e por quê

- **Terraform, ECS, RDS, SES fora do sandbox.** É a fase 2, e depende da decisão de nuvem.
- **`Prumo.Notifications` publicado como container.** O `Dockerfile` já existe e não muda.
- **Testes dos workers.** Depois da extração, o que sobra neles é tradução de
  `NotificationOutcome` para a API da fila; testar isso seria testar o SDK.
- **`Prumo.Infrastructure` largar o `Azure.Messaging.ServiceBus`.** Conviver é a decisão 1.

## O que esta fase deixa pronto para a fase 2

Se a migração para AWS seguir, os itens 3, 4 e 5 do pass de Azure (ACS, Service Bus Basic,
worker em Container App com KEDA) deixam de fazer sentido e viram os equivalentes de AWS.
Se ela não seguir, nada aqui precisa ser revertido: `Notifications:Provider=ServiceBus` e
`Email:Provider=Acs` restauram o comportamento de hoje por configuração.

---

## Registro de execução

Executado em **2026-09-03**, tarefas 1 a 11 de 12. Suíte: 119 → **130**, build sem erro,
sem warning novo. A Task 12 (verificação de ponta a ponta) **não rodou** — ver o fim.

As tarefas 1 a 6 saíram por subagente, cada uma com revisão de aderência à spec e revisão
de qualidade; da 7 em diante o limite de sessão derrubou os subagentes e o resto foi feito
direto. Os achados abaixo vieram quase todos das revisões, e são o que o plano não previu.

### Onde o plano errou, e o que as revisões acharem

1. **O `default:` do switch era perda silenciosa de mensagem.** Tanto o worker do Service
   Bus quanto o do SQS mapeavam "qualquer coisa que não seja `Poison`" para completar/
   apagar. Um quarto `NotificationOutcome` seria descartado como se tivesse sido enviado —
   o oposto do que o XML doc da hierarquia fechada promete. E `switch` de *statement* não
   dá exaustividade em tempo de compilação, então listar os casos não bastaria: os dois
   workers agora listam `Handled`/`Duplicate` explicitamente e **estouram** no `default`.

2. **O `MessageId` sumiu na extração.** O worker antigo logava o identificador de
   transporte ao falhar o parse de JSON; o `NotificationHandler` não pode, porque só
   recebe o corpo — e essa ignorância é o ponto do seam. O worker do Service Bus passou a
   logar `MessageId` + motivo + detalhe **antes** de dead-letterar, o que acabou virando
   um superconjunto do log antigo: agora vale para os três casos de veneno, não só para
   `InvalidJson`.

3. **A troca do `try` que envolvia render *e* envio foi mudança de comportamento real,**
   e está certa: um `InvalidOperationException` vindo do *sender* era dead-lettered como
   `RenderFailed`. Agora só o render está no `try`. A Task 4 fixou isso com dois testes de
   caracterização, ambos provados por mutação (mover `_seen.Set` para antes do envio, e
   envolver o envio em `try/catch` — cada um derruba o teste esperado).

4. **`Sanitize` no worker do SQS faz mais que truncar.** O plano só truncava. Uma revisão
   notou que `Poison.Detail` carrega `ex.Message`, e a mensagem do
   `InvalidOperationException` do `NotificationRenderer` embute as chaves do payload —
   atributo de mensagem do SQS recusa caracteres de controle, e um atributo recusado no
   publish seria pior que um truncado: a mensagem envenenada não chegaria à DLQ.

5. **O `try/catch` do `SqsNotificationWorker.ProcessAsync` envolve a tradução também,** e
   não só o `HandleAsync` como o plano escrevia. Assim, falha da própria chamada de fila
   (ou o `throw` do `default`) deixa a mensagem reentregar em vez de derrubar o laço de
   polling — que é o equivalente ao que o SDK do Service Bus faz sozinho do outro lado.

6. **A janela de duplicata tem uma corrida conhecida e aceita.** `TryGetValue` seguido de
   `Set` não é atômico e o processor roda com `MaxConcurrentCalls = 4`: duas reentregas do
   mesmo `CorrelationId` podem passar as duas. Está comentado no campo `_seen` de
   propósito, para ninguém "consertar" com um lock atravessando o `await` do envio, que
   serializaria todos os envios.

7. **Asserções fracas viraram fortes.** O teste dos três tipos só checava
   `HtmlBody.Length > 0` — e é a **única** cobertura do `NotificationRenderer` no repo
   inteiro. Passou a checar o assunto exato por tipo e o valor substituído. As buscas em
   `MessageAttributes` trocaram indexer por `TryGetValue`, porque o indexer estourava
   `KeyNotFoundException` em vez de falhar legível justamente no caso que o teste existe
   para pegar.

### Detalhes de ferramenta que confundem quem for olhar depois

- **O rename do worker não aparece como rename.** `git mv` foi usado, mas o conteúdo mudou
  ~69% e a detecção do git é por similaridade, com limiar padrão de 50%. Use
  `git log --follow -M20%` para enxergar a história de `NotificationWorker.cs` através de
  `ServiceBusNotificationWorker.cs`.
- **`Prumo.Tests` não precisou de `PackageReference` da AWS**: os tipos chegam
  transitivamente pelo `ProjectReference` de `Prumo.Infrastructure`.
- **`AmazonSQSConfig.ServiceURL` e `RegionEndpoint` são mutuamente exclusivos** — atribuir
  um anula o outro. Por isso a região local vai em `AuthenticationRegion`.

### A Task 12 rodou — 2026-09-04

O Docker Desktop subiu e a verificação de ponta a ponta foi executada inteira, nesta ordem:
`docker compose --profile notifications up -d`, worker, API, `POST /api/auth/forgot-password`,
e-mail em disco, mensagem podre na DLQ, suíte. **Passou.** O que ela mostrou:

- **O ElasticMQ nasce com as duas filas**, e a `notifications` nasce com o redrive certo:
  `DeadLettersQueueData(notifications-dlq, 5)` e visibilidade de 60 s, lidos do log do
  próprio ElasticMQ. Nenhuma fila precisou ser criada à mão.
- **O caminho feliz é real.** A API logou `Notificação email.password-reset publicada
  (3910aa87-…)` pelo `SqsNotificationPublisher`, o worker consumiu e o `FileEmailSender`
  gravou `20260904-224801-272_nickolas.scheidt@gmail.com.html`. O corpo renderizou sem
  nenhum `{{placeholder}}` sobrando — o link de reset saiu com `uid` e `token` de verdade.
- **O caminho do veneno é real.** Corpo `isto-nao-e-json` publicado direto na fila virou
  `Mensagem b26d343c-… enviada para a DLQ: InvalidJson — 'i' is an invalid start of a
  value.` no log, e a mensagem chegou na DLQ com `PoisonReason=InvalidJson` e o
  `PoisonDetail` com o texto do erro. Os atributos sobreviveram ao `Sanitize`.
- **A suíte:** 130 aprovados, 0 falhas.

### Onde o plano errou na própria Task 12

1. **`forgot-password` responde 202, não 200.** O plano esperava 200. O 202 é o certo — o
   endpoint só publica na fila, o envio é assíncrono —, então quem for repetir a
   verificação não deve "consertar" o endpoint para casar com o plano.

2. **Trocar o emulador no compose não o tira da máquina de ninguém.** `saasbase-mssql` e
   `saasbase-servicebus` ficaram como containers órfãos: o compose não os define mais, mas
   eles carregam `restart: unless-stopped`, então o SQL Server **voltou sozinho** quando o
   Docker Desktop subiu — 1,25 GiB, exatamente o peso que a troca dizia ter eliminado. O
   `docker compose down` não os remove; só `--remove-orphans` ou `docker rm -f` resolve.
   A economia vale para quem clonar limpo; para quem já rodava o emulador é preciso um
   passo manual, e ele não está documentado em lugar nenhum.

### O que ficou pendente

Um resíduo para decidir junto: a seção `ServiceBus` continua nos dois `appsettings.json`
com a connection string do emulador (`UseDevelopmentEmulator=true`), e o emulador não
existe mais no compose. Manter a seção é o combinado — trocar de nuvem é mudar
`Notifications:Provider` — mas o valor agora aponta para nada em desenvolvimento. Ou vira
string vazia (e o publisher cai no `LoggingNotificationPublisher`), ou ganha um comentário
em algum lugar que não seja JSON.

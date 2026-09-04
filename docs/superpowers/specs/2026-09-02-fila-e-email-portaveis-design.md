# Design: fila e e-mail portáveis (fase 1 do desacoplamento de nuvem)

> Escrito em 2026-09-02. Cobre a **fase 1** do estudo de migração Azure → AWS:
> tirar do código a dependência dura de Azure, sem tocar em Terraform, sem conta
> AWS e sem sair do sandbox do SES. A fase inteira roda e se verifica offline,
> no `docker compose`.

## Objetivo

Uma varredura do backend em 2026-09-02 por `Azure`, `DefaultAzureCredential`,
`ApplicationInsights`, `Blob` e `azurecontainerapps` achou **exatamente duas
superfícies** de acoplamento a Azure:

| Projeto | Pacote |
|---|---|
| `Prumo.Infrastructure` | `Azure.Messaging.ServiceBus` |
| `Prumo.Notifications` | `Azure.Messaging.ServiceBus` + `Azure.Communication.Email` |

O Key Vault **não aparece no código** — os segredos chegam como variável de
ambiente comum, postas pelo Container App. O Angular não tem uma referência a
Azure. Ou seja: o que prende o Prumo à Azure são dois pacotes NuGet, e o alvo
desta fase é fazer com que a escolha de nuvem vire **configuração**, não
recompilação.

Isso vale mesmo que a decisão final seja continuar na Azure. O ganho não é
hipotético: o `docker compose` local troca ~2 GB de emulador por ~20 MB, e a
lógica de notificação passa a ter testes.

## O que esta fase NÃO faz

Nada de Terraform, nada de conta AWS, nada de pedir *production access* do SES,
nada de mexer em domínio ou DNS. Nenhum recurso de nuvem é criado. Se a decisão
for ficar na Azure, nada aqui precisa ser revertido.

## Decisões

| # | Decisão | Escolha | Por quê |
|---|---|---|---|
| 1 | Substituir ou conviver | **Conviver**: Service Bus e SQS lado a lado | O objetivo declarado é a nuvem virar variável de configuração. Apagar o caminho da Azure trocaria um acoplamento por outro |
| 2 | Como o worker fica portátil | **Extrair `NotificationHandler`**, um worker fino por fila | Ver "A alternativa recusada" abaixo |
| 3 | Seam entre lógica e fila | **`NotificationOutcome`** (`Handled` / `Duplicate` / `Poison`) | O resultado é a única coisa que as duas filas têm em comum. A *mensagem* não é |
| 4 | Veneno no SQS | **DLQ imediata**, publicando e apagando à mão | Preserva o comportamento de hoje. `maxReceiveCount` reprocessaria N vezes algo que já se sabe irrecuperável |
| 5 | Padrão local | **ElasticMQ/SQS** | Escolhido pelo Nickolas em 2026-09-02. O emulador do Service Bus puxa dois containers e ~2 GB para um projeto que roda em um |
| 6 | E-mail | **`SesEmailSender`** como terceiro `Email:Provider` | O `IEmailSender` já previa múltiplos provedores; `File` continua sendo o default de desenvolvimento |
| 7 | Chave do provedor de fila | **`Notifications:Provider`**, seção nova | `ServiceBus:ConnectionString` como chave de decisão não sobrevive a existirem duas filas |

## A alternativa recusada: `IQueueConsumer` genérico

O caminho óbvio seria um `IQueueConsumer` entregando um `IMessageContext` com
`CompleteAsync()` e `DeadLetterAsync(motivo, detalhe)`, e o `NotificationWorker`
atual sobreviveria quase intacto.

Foi recusado porque **vaza**. O Service Bus tem dead-letter nativa: uma chamada,
com motivo e descrição, e a mensagem sai da fila para a sub-fila. O SQS não tem
nada disso — dead-letter é uma *redrive policy* que só dispara por contagem de
recebimento; mandar algo para a DLQ na hora é publicar na outra fila e apagar
da origem, duas operações que podem falhar entre si. Um `IMessageContext` que
apresentasse as duas como o mesmo método estaria mentindo sobre o que acontece
quando o `SendMessage` funciona e o `DeleteMessage` falha.

O `NotificationOutcome` evita isso: ele diz **o que aconteceu com a
notificação**, e cada adapter decide o que isso significa para a sua fila.

## Arquitetura

```
Prumo.Api                          Prumo.Notifications
  NotificationConfiguration          NotificationHandler   <-- toda a lógica, testável
    | escolhe por Notifications:Provider    ^
    v                                       | NotificationOutcome
  INotificationPublisher                    |
    +-- ServiceBusNotificationPublisher   ServiceBusNotificationWorker --> Azure Service Bus
    +-- SqsNotificationPublisher          SqsNotificationWorker        --> Amazon SQS / ElasticMQ
    +-- LoggingNotificationPublisher      (escolhido por Notifications:Provider)
        (sem fila configurada)
```

### `NotificationHandler`

Recebe o corpo bruto da mensagem (string) e devolve um `NotificationOutcome`.
Concentra o que hoje está espalhado dentro dos handlers do `ServiceBusProcessor`:

1. Desserializa. Falha de JSON ou corpo `null` → `Poison`.
2. Consulta a janela de duplicata em `IMemoryCache` por `CorrelationId` → `Duplicate`.
3. Renderiza pelo `NotificationRenderer`. `NotSupportedException` /
   `InvalidOperationException` → `Poison`.
4. Envia pelo `IEmailSender`. Exceção **sobe** — falha de provedor é retry, não veneno.
5. Marca a janela e devolve `Handled`.

`NotificationOutcome` é um record hierárquico:
`Handled`, `Duplicate`, `Poison(string Reason, string Detail)`.

### Os dois workers

- **`ServiceBusNotificationWorker`** — o `NotificationWorker` de hoje, com o miolo
  removido. `Handled`/`Duplicate` → `CompleteMessageAsync`; `Poison` →
  `DeadLetterMessageAsync(reason, detail)`. Exceção que sobe → não completa, e o
  Service Bus reentrega.
- **`SqsNotificationWorker`** — laço de *long polling* (`WaitTimeSeconds = 20`,
  `MaxNumberOfMessages = 10`). `Handled`/`Duplicate` → `DeleteMessage`; `Poison` →
  `SendMessage` na DLQ **e então** `DeleteMessage` na origem, nessa ordem. Se o
  envio para a DLQ falhar, a mensagem **não** é apagada: ela volta pelo *visibility
  timeout* e a `maxReceiveCount` acaba levando para a DLQ de qualquer forma. A
  ordem torna a duplicata possível e a perda não.

O motivo e o detalhe do `Poison` viajam para a DLQ como `MessageAttributes`
(`PoisonReason`, `PoisonDetail`), que é o mais perto que o SQS chega da
dead-letter descrita do Service Bus.

### `SqsNotificationPublisher`

Espelha o `ServiceBusNotificationPublisher`: serializa o `NotificationMessage`,
manda `SendMessageAsync`, loga. `Type` e `CorrelationId` sobem como
`MessageAttributes` — mesma intenção de hoje, inspecionar uma fila parada sem
desserializar o corpo. `IAmazonSQS` é singleton no DI, como o `ServiceBusClient`.

### `SesEmailSender`

`IAmazonSimpleEmailServiceV2` + `SendEmailAsync` com corpo HTML. Valida
`SenderAddress` no construtor, como o `AcsEmailSender` faz. Sem `WaitUntil`
equivalente: o SES já é aceite-e-devolve, e o `MessageId` da resposta entra no
log no lugar do `operation.Id`.

Credenciais vêm da cadeia padrão do SDK (variável de ambiente, perfil, role da
task). O `SesEmailSender` não lê chave de configuração nenhuma — em produção é
role de IAM, e localmente ele nem é usado, porque o default local é `File`.

## Configuração

Seções novas, com os defaults de desenvolvimento no `appsettings.json`:

```json
"Notifications": { "Provider": "Sqs" },
"Sqs": {
  "QueueUrl": "http://localhost:9324/000000000000/notifications",
  "DeadLetterQueueUrl": "http://localhost:9324/000000000000/notifications-dlq",
  "ServiceUrl": "http://localhost:9324",
  "Region": "us-east-1"
},
"Email": { "Provider": "File", "Ses": { "SenderAddress": "" } }
```

`ServiceUrl` preenchido aponta o SDK para o ElasticMQ; vazio, para a AWS real.
A seção `ServiceBus` **permanece** intacta — trocar de nuvem é mudar
`Notifications:Provider` para `ServiceBus`.

`Notifications:Provider` ausente ou sem a fila correspondente configurada
mantém o comportamento atual: `LoggingNotificationPublisher`, que descarta e
grita em `Warning`.

## Ambiente local

O `docker-compose.yml` perde os serviços `servicebus` e `mssql` e ganha:

```yaml
elasticmq:
  image: softwaremill/elasticmq-native
  profiles: ["notifications"]
  ports: ["9324:9324", "9325:9325"]
  volumes: ["./elasticmq/elasticmq.conf:/opt/elasticmq.conf:ro"]
```

O `elasticmq.conf` declara `notifications` e `notifications-dlq` na subida, com
redrive policy de `maxReceiveCount = 5`. O perfil `notifications` continua
existindo — a API roda sem a fila, como hoje.

Efeito colateral bem-vindo: some o `ACCEPT_EULA: "Y"` chumbado no compose, que
estava na lista de pendências do pass de Azure.

## Testes

O `NotificationHandler` é a primeira vez que essa lógica fica testável, e é onde
o TDD desta fase acontece:

- os três tipos de `NotificationTypes` renderizam e chamam o `IEmailSender`;
- reentrega do mesmo `CorrelationId` dentro da janela → `Duplicate`, sem chamar o sender;
- o mesmo `CorrelationId` fora da janela → envia de novo;
- corpo que não é JSON → `Poison("InvalidJson", ...)`;
- corpo que desserializa para `null` → `Poison("EmptyBody", ...)`;
- tipo desconhecido → `Poison("RenderFailed", ...)`;
- `IEmailSender` lançando `HttpRequestException` → a exceção **sobe** (é retry, não veneno).

E, no publisher, `SqsNotificationPublisher` contra um `IAmazonSQS` substituto:
serializa o envelope no corpo e põe `Type`/`CorrelationId` nos atributos.

Os workers em si não ganham teste: depois da extração, o que sobra neles é
tradução de `NotificationOutcome` para a API da fila, e testar isso seria testar
o SDK.

## Fora de escopo, e por quê

- **`Prumo.Infrastructure` continua referenciando `Azure.Messaging.ServiceBus`.**
  Conviver é a decisão 1; o pacote só sairia se o Service Bus saísse junto.
- **Terraform, ECS, RDS, SES fora do sandbox.** É a fase 2, e depende de decisão
  de nuvem que não foi tomada.
- **`Prumo.Notifications` publicado como container em nuvem nenhuma.** O
  `Dockerfile` já existe e não muda.

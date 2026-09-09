# Design: ambiente dev na AWS (fase 2 da migração, primeira fatia)

> Escrito em 2026-09-07. A decisão de nuvem foi tomada pelo Nickolas nesta data:
> **migração full para AWS**. Isto cobre a **primeira fatia** da fase 2 — o
> mínimo que sobe e permite fazer login de um navegador. Não cobre SES, worker,
> rede privada, IAM database auth nem RLS; cada um vira sua própria spec.
>
>
> **Revisto duas vezes em 2026-09-08.**
>
> Na primeira, o Nickolas enquadrou este ambiente como a **versão piloto para
> testar o mercado**, não como caixa de teste descartável. Consequências: região
> `sa-east-1` (decisão 8), backup automático (decisão 10) e domínio deixando de
> ser pré-requisito (decisão 11).
>
> Na segunda, ele apontou que **~US$ 32/mês é caro para um piloto que ainda não
> vendeu nada** — e a verificação da conta real tinha acabado de mostrar que não
> há crédito nenhum para amortecer isso. O compute passou de EC2 para
> **Lightsail** (decisão 2), e com ele mudaram o deploy (decisão 7), o lugar dos
> segredos e a forma de entrar na máquina. Custo: ~US$ 14/mês.

## Objetivo

A fase 1 (`2026-09-02-fila-e-email-portaveis-design.md`) tirou a dependência dura
de Azure do código: a nuvem virou `Notifications:Provider` e `Email:Provider`.
Ela terminou dizendo que Terraform, ECS, RDS e SES eram a fase 2 e dependiam de
uma decisão de nuvem que não estava tomada. Está tomada.

O alvo desta fatia é **um ambiente na AWS que sobe e loga**, e nada além disso.
A escolha de escopo é deliberada: cada peça seguinte entra sobre algo que já
roda, em vez de tudo se verificar de uma vez no fim.

**A restrição, nas palavras do Nickolas em 2026-09-07:** o caminho mais fácil e
menos trabalhoso para subir na nuvem e testar. Vai ter **uma pessoa usando**, não
precisa escalar agora — mas os princípios têm que permitir escalar depois. É essa
frase que justifica as decisões 2 e 3, e é contra ela que elas devem ser julgadas
quando forem revisitadas.

**Não existe migração de dados nem janela de corte.** A stack Azure foi
destruída para $0 em 2026-06 (ver `NEXT_SESSION.md` no repo de DevOps); sobrou
apenas o storage account do tfstate. Isto é uma construção nova, não um cutover.

## O que esta fatia NÃO faz

Não cria SES, não verifica domínio para envio, não publica o worker de
notificação, não põe o banco em rede privada, não troca senha por IAM auth, não
liga RLS e não cria ambiente de produção. Também **não remove o caminho Azure do
código** — ver "O que fica para trás", no fim.

## Decisões

| # | Decisão | Escolha | Por quê |
|---|---|---|---|
| 1 | Escopo da primeira fatia | **Sobe e loga** | Escolhido pelo Nickolas em 2026-09-07. Login não depende de e-mail, então é o maior pedaço verificável que não espera o prazo do SES |
| 2 | Compute | **Uma instância Lightsail de 2 GB com `docker compose`** | Revisto em 2026-09-08 — ver "Por que Lightsail". Era EC2 `t3.small`; a troca corta o custo de ~US$ 32 para ~US$ 14/mês e o preço passa a ser fixo |
| 3 | Banco | **Postgres no próprio compose** | Monta `db/roles.sql` no entrypoint, então os dois roles do item 11 nascem sozinhos em volume novo. Custo zero além do EBS |
| 4 | Fila | **SQS real, sem worker ainda** | Fila e DLQ de verdade custam quase nada e provam o caminho de IAM sem chave. A API publica; ninguém consome até a spec do SES |
| 5 | Borda e TLS | **Caddy no compose, Let's Encrypt** | Sem custo de ALB, sem renovação para gerenciar. O JWT deixa de viajar em claro e o domínio já fica de pé para o SES |
| 6 | Frontend | **A imagem nginx do Angular, intacta** | Aquele `proxy_pass` literal foi o quinto e último bug do primeiro deploy na Azure. Caddy termina TLS e repassa; o nginx continua fazendo o proxy de `/api` |
| 7 | Deploy | **ECR + GitHub Actions por OIDC; a subida em si é um script seu, por SSH** | Revisto em 2026-09-08: o Lightsail não é alcançável por SSM Run Command. O OIDC do build sobrevive intacto; o que muda é quem aperta o botão do deploy |
| 8 | Região | **`sa-east-1` (São Paulo)** | Revisto em 2026-09-08 — ver "Por que São Paulo" abaixo. O piloto existe para causar boa impressão em cliente brasileiro, e 120 ms de latência trabalham contra isso |
| 9 | Terraform da Azure | **Fica onde está, intocado** | Mesma razão do código: arrancar agora não entrega nada. A árvore `azurerm` é apagada quando a AWS servir tráfego |
| 10 | Backup | **Snapshot diário automático, 7 dias, add-on da instância** | Acrescentado em 2026-09-08. O banco vive no disco da instância; sem isto, perder o disco perde o piloto inteiro. No Lightsail são quatro linhas de Terraform, contra uma role de IAM mais uma policy de DLM na versão EC2 |
| 11 | Nome de host inicial | **`sslip.io` sobre o IP estático; domínio próprio quando houver o que mostrar** | Acrescentado em 2026-09-08. O Caddy precisa de um *nome*, não de um domínio comprado. Isto tira o registrador do caminho crítico do primeiro `apply` |

## Por que São Paulo (revisão da decisão 8, 2026-09-08)

A escolha original foi `us-east-1`, com dois argumentos. Um deles não sobrevive à
verificação:

- *"É o caminho mais batido para sair do sandbox do SES."* **Falso na prática.** O
  SES roda em `sa-east-1` desde abril de 2020 e sair do sandbox é o mesmo ticket
  de suporte em qualquer região.
- *"Já é o default no `appsettings.json`."* Verdade, e irrelevante: é uma linha de
  configuração. Ela passa a ser `sa-east-1` na Task 9.

Sobrava preço contra latência. Na EC2 a conta era esta:

| | `us-east-1` | `sa-east-1` |
|---|---|---|
| `t3.small` on-demand | US$ 0,0208/h | US$ 0,0336/h (**+62%**) |
| Ambiente inteiro, 24/7 | ~US$ 21/mês | ~US$ 32/mês |
| Latência do Brasil | ~120 ms | ~15 ms |

**A revisão para Lightsail (decisão 2) apagou até esse preço:** o plano de 2 GB custa US$ 12 nas duas regiões. São Paulo passou a ser de graça em relação à Virgínia, com uma única diferença — a franquia de tráfego cai pela metade, de 3 TB para 1,5 TB, o que continua sendo muito mais do que um piloto usa.

Ou seja: a decisão de região, que era um trade, virou escolha óbvia.

O que torna isto decisão de *agora*, e não de depois: o Postgres vive no disco da instância. Mudar de região mais tarde não é trocar uma variável — é snapshot, cópia entre regiões e recriar tudo, com os dados do piloto dentro.

## Custo: não há crédito nenhum

Escrito primeiro assumindo conta nova, e **corrigido em 2026-09-08 depois de
olhar a conta real** (`7673-9793-9785`) pelo console:

- **Créditos: US$ 0,00.** Nenhum ativo, nenhum resgatável.
- A conta foi aberta em **junho de 2025**, então tanto o free tier de 12 meses
  quanto os US$ 100/200 do modelo novo (que vale para contas abertas a partir de
  julho de 2025) **não se aplicam**. Os 12 meses expiraram por volta de junho de
  2026.
- Custo corrente e do mês anterior: **US$ 0,00** — não há nada ligado na conta.

**Consequência prática: este ambiente custa do primeiro dia**, do bolso, sem
período de carência. Foi essa constatação que motivou a decisão 2 a trocar EC2 por
Lightsail no mesmo dia, derrubando o custo de ~US$ 32 para ~US$ 14/mês. Não existe a folga de "os primeiros meses são de
graça" que a primeira versão desta seção prometia.

Isso não muda nenhuma decisão de arquitetura — muda a expectativa, e muda o peso
do budget alarm da Task 0, que passa a ser a única coisa entre um erro de
configuração e uma fatura real.

Toda a discussão de Free Plan × Paid Plan que estava aqui saiu por ser irrelevante:
ela vale no cadastro de conta nova, e esta conta é anterior ao modelo.

## Por que Lightsail (revisão da decisão 2, 2026-09-08)

A escolha original foi uma EC2 `t3.small`. Ela funcionava; o problema é o preço
para o que este ambiente é. Com a conta real verificada e **zero créditos**
(ver "Custo: não há crédito nenhum"), ~US$ 32/mês saem do bolso desde o primeiro
dia, para um piloto que ainda não tem cliente pagante.

| | EC2 `t3.small` | Lightsail 2 GB |
|---|---|---|
| Compute | US$ 24,50 | incluído |
| Disco | ~US$ 3,30 (30 GB EBS) | incluído (60 GB) |
| IP público | US$ 3,65 | incluído |
| Tráfego | por GB | 1,5 TB incluído |
| Backup | ~US$ 1 (DLM) | ~US$ 1 (add-on) |
| **Total** | **~US$ 32/mês** | **~US$ 14/mês** |

Lightsail **é AWS**: mesma conta, mesmo console, mesma fatura, mesma região. Não
é sair da AWS — é usar a porta de entrada dela em vez de montar a máquina peça
por peça.

Três ganhos além do preço:

1. **O preço é fixo.** O plano custa US$ 12 aconteça o que acontecer com CPU e
   tráfego. Para quem está com medo da fatura, isso vale mais que a diferença.
2. **O backup é um add-on da instância**, não uma role de IAM mais uma policy de
   Data Lifecycle Manager. Sete snapshots retidos, rotacionados sozinhos.
3. **O console fica legível.** Uma tela com a instância, o disco e os snapshots,
   em vez de sete serviços. Isto atende diretamente o pedido do Nickolas de
   *"acessar o portal e entender exatamente o que foi feito"*.

### O que o Lightsail custa, e não é dinheiro

Esta parte é a que importa quando a decisão for revisitada.

- **Não existe instance profile.** Na EC2, a máquina falava com SQS e ECR sem que
  chave nenhuma existisse. No Lightsail é preciso uma chave IAM gravada em disco.
  A defesa é escopo: a chave pode `sqs:SendMessage` numa fila e ler duas imagens
  do ECR, e nada mais. **Um vazamento dá ao atacante o direito de enfileirar
  e-mail seu e baixar suas imagens** — pequeno, contido, mas não zero, e antes
  era zero.
- **O Parameter Store saiu junto.** Ele só fazia sentido com instance profile;
  sem ele, ler segredo exigiria uma chave capaz de ler todos os segredos, que é
  pior do que o problema que resolve. Os segredos passam a viver no `.env` da
  máquina — que é onde eles iam parar de qualquer jeito.
- **Voltou a porta 22.** Não há SSM Session Manager. Ela fica restrita ao IP de
  casa, e a chave privada nunca passa pelo Terraform. Ainda assim é superfície
  que não existia.
- **O deploy deixou de ser automático.** Sem SSM Run Command, as opções eram
  abrir a 22 para o mundo e guardar uma chave privada nos secrets do GitHub, ou
  o CI construir e **você** rodar `deploy.sh`. Escolhi o segundo: para uma pessoa,
  são 30 segundos, e a alternativa custa caro em superfície.
- **Migrar para EC2 depois é refazer a infra**, não trocar uma variável. Mas isso
  já valia: a spec sempre registrou que quase todo o Terraform desta fatia é
  descartável quando rede privada e IAM auth entrarem na pauta.

### O que ficou igual, de propósito

ECR, filas SQS, OIDC do GitHub para o build, Caddy com Let's Encrypt, o compose
de quatro serviços, o `sslip.io` e a região. A troca de compute mexeu em quatro
tasks do plano; as outras oito passaram intactas. Isso é sinal de que a fronteira
estava no lugar certo.

## A alternativa recusada: ECS Fargate

Foi a minha recomendação, e não foi a escolha. Fica registrada porque a razão
importa quando esta decisão for revisitada.

ECS Fargate é o equivalente direto do Container Apps e é o formato para onde o
alvo endurecido vai de qualquer jeito: rede privada, IAM database auth e um
sidecar cabem nele sem trocar de compute. O preço era mais Terraform agora — VPC,
subnets, ALB, target group, listener, task definition — e ~US$18/mês de ALB
enquanto ligado.

Uma caixa com compose chega ao "sobe e loga" com muito menos peça, e casava com o
hábito de ligar e destruir o ambiente inteiro — hábito que a revisão de 2026-09-08
aposenta: ambiente de piloto fica **ligado**, e `terraform destroy` passa a ser o
botão de desistir, não a rotina do fim do dia. **O custo é que quase todo o
Terraform desta spec é descartável** no dia em que rede privada e IAM auth
entrarem na pauta: aquilo não cabe numa caixa com `docker compose`. Isto está
escrito aqui para que esse dia não seja surpresa.

## Arquitetura

```
                        Internet
                           |
                        :443 (TLS)
                           |
   +-----------------------v------------------------+
   |  Lightsail 2 GB   (firewall: 80, 443, 22)       |
   |                                                 |
   |   caddy ---> web (nginx + Angular) ---> api     |
   |                                          |      |
   |                                          v      |
   |                                      postgres   |
   |                                                 |
   |   .env  <- segredos, 600, escritos a mao        |
   |   chave IAM: SendMessage + pull do ECR          |
   +-----------------------|-------------------------+
                           |
                +----------+----------+
                |                     |
               SQS                   ECR
          notifications          pull das
          + -dlq                  imagens

   snapshot diario: add-on da propria instancia
```

Do mundo, só 80 e 443. A porta 22 existe, restrita ao IP de casa. O Postgres não
publica porta: existe apenas na rede do compose.

Compare com a versão EC2 desta spec, que tinha VPC default, security group, IP
elástico, instance profile e SSM Parameter Store no lugar do que está acima. A
troca custou uma chave IAM em disco e ganhou metade da conta e um console que
cabe numa tela.

## Terraform

Árvore nova em `terraform/aws/dev/` no repo `SaaSBasePlatform-DevOps`, ao lado da
árvore `azurerm` existente, que não é tocada.

**Estado remoto:** bucket S3 novo, com *locking* nativo do S3 (`use_lockfile`),
sem tabela DynamoDB. Isso exige subir o `required_version` da árvore AWS para
`>= 1.10.0`; a árvore Azure continua em `>= 1.7.0`.

O que a árvore cria:

- **Instância:** Lightsail `small_3_0` — 2 GB, 2 vCPU, 60 GB de SSD, blueprint
  Amazon Linux 2023. Preço fixo, disco e tráfego inclusos.
- **IP estático** do Lightsail, para o nome não mudar a cada recriação.
- **Regras de porta:** 80 e 443 de `0.0.0.0/0`; 22 apenas do CIDR de casa.
- **Par de chaves:** só a metade pública, lida da sua máquina com `file()`. A
  privada nunca passa pelo Terraform e portanto nunca entra no state.
- **Backup:** o add-on `AutoSnapshot` da própria instância, disparando 06:00 UTC.
  O Lightsail retém os 7 mais recentes e rotaciona sozinho. É o único mecanismo
  de recuperação deste desenho — não há réplica, não há standby.
- **Usuário IAM da máquina** com exatamente três coisas: `sqs:SendMessage` na fila
  de notificações, leitura dos dois repositórios do ECR, e o token de login do
  ECR. **A chave de acesso não é criada pelo Terraform** — ela iria em texto claro
  para o state file. É criada à mão, uma vez.
- **Filas:** `notifications` e `notifications-dlq`, com redrive de
  `maxReceiveCount = 5` e visibilidade de 60s — os mesmos números do
  `elasticmq/elasticmq.conf`, para o local não ser mais permissivo que o remoto.
- **ECR:** dois repositórios, `prumo-api` e `prumo-web`.
- **OIDC:** provider do GitHub e uma role assumível pelos repos de aplicação,
  substituindo as federated credentials do Entra. Ela só empurra imagem.
- **Route 53:** zona e registro A apontando para o IP estático — **opcional**.
  Com `route53_zone_id` vazio, a árvore não cria nada de DNS e o ambiente atende
  pelo nome `sslip.io` do IP estático (ver decisão 11).

Sumiram, em relação à versão EC2: VPC, subnets, security group, IP elástico,
instance profile, role da instância, parâmetros do SSM e a policy de Data
Lifecycle Manager.

**O user-data** só instala Docker e prepara `/opt/prumo`. Os segredos entram no
`.env` por SSH, uma vez; o deploy é `deploy/deploy.sh`, que empacota os arquivos,
atualiza as tags e sobe o compose.

## O compose de deploy

Arquivo próprio, versionado no repo de DevOps — **não** é o `docker-compose.yml`
da raiz do repo da aplicação, que é de desenvolvimento e monta código local.

Quatro serviços: `caddy`, `web`, `api`, `postgres`. O `postgres` monta
`db/roles.sql` em `docker-entrypoint-initdb.d` exatamente como no local, e o
`Caddyfile` tem uma linha de domínio — o resto do TLS o Caddy resolve sozinho.

### O nome de host, sem depender de registrador (decisão 11)

O Let's Encrypt emite para qualquer nome que resolva para o IP da máquina; nada
exige que o nome tenha sido comprado. `sslip.io` resolve
`54-207-1-2.sslip.io` → `54.207.1.2` sem cadastro, sem DNS para configurar e sem
custo. Como o IP estático do Lightsail é fixo, o nome também é.

O ambiente sobe com esse nome e o Caddy tira certificado válido. **Trocar pelo
domínio de verdade depois é uma linha no `Caddyfile` e uma no
`appsettings.Production.json`** — nenhum recurso muda.

Isto é para destravar o primeiro `apply`, não é o estado final. O domínio próprio
é obrigatório em duas situações que chegam logo: mostrar o produto para um cliente
do piloto, e verificar DKIM no SES. Registrar custa ~R$ 40/ano.

## O que muda no repo da aplicação

Três coisas, todas pequenas, todas hoje apontando para Azure ou para lugar nenhum:

1. **`appsettings.Production.json` está com placeholders de Azure.** A connection
   string diz `CONFIGURE_VIA_AZURE_APP_SETTINGS_OR_KEY_VAULT` e o `AllowedHosts`
   e o CORS apontam para `yourdomain.com` — pendência que o `NEXT_SESSION.md`
   arrasta desde junho, contornada por env var no deploy. Com domínio real, viram
   valor real.

2. **Migration precisa rodar.** A API se recusa a servir com schema atrasado e a
   credencial da aplicação não faz DDL. Nesta fatia, `Database:MigrateOnStartup`
   fica ligado com `ConnectionStrings:MigratorConnection` — é o mecanismo que já
   existe, já é testado e hoje só o overlay `Demo` usa. **É a peça a substituir
   por um passo de deploy quando o compute mudar**, e o `deploy.yml` do repo de
   DevOps é onde ela vai morar.

3. **Nada de novo no código de notificação.** A guarda escrita em 2026-09-07 já
   cobre este ambiente: em Production, a API se recusa a subir sem
   `Sqs:QueueUrl`, com `Sqs:ServiceUrl` preenchida, ou com `Email:Provider=File`.
   Este desenho satisfaz as três por construção.

### Por que o ambiente não roda como `Development`

Seria o atalho óbvio para escapar das guardas, e é inseguro. Em Development o
`LoggingNotificationPublisher` sobe com `includePayload: true`, que escreve o
**token de reset de senha** no log — e o Serilog deste projeto tem sink para
tabela. Numa máquina alcançável da internet isso é caminho de tomada de conta.
Development não sai da máquina de desenvolvimento.

## Configuração

O que o `.env` da máquina precisa pôr no ambiente da API:

```
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=postgres;...;Username=prumo_app;...
ConnectionStrings__MigratorConnection=Host=postgres;...;Username=prumo_migrator;...
Database__MigrateOnStartup=true
Notifications__Provider=Sqs
Sqs__QueueUrl=https://sqs.sa-east-1.amazonaws.com/<conta>/notifications
Sqs__Region=sa-east-1
AWS_ACCESS_KEY_ID=<do usuário prumo-dev-box>
AWS_SECRET_ACCESS_KEY=<idem>
Jwt__Key=<gerado na Task 3>
Seed__AdminPassword=<escolhido na Task 3>
```

As duas linhas de `AWS_` são a marca do Lightsail. Na versão EC2 elas não existiam: o instance profile entregava a credencial ao SDK sem que nada fosse escrito em disco. Aqui elas são inevitáveis, e por isso a chave que elas carregam é a mais fraca possível (decisão 2, "O que o Lightsail custa").

O arquivo tem permissão `600` e dono `ec2-user`. Ele **não** é versionado, e não existe cópia dele em lugar nenhum da AWS — os valores moram no seu gerenciador de senhas. Perder os dois ao mesmo tempo é perder o ambiente.

`Sqs__ServiceUrl` **fica ausente**. É o que faz o SDK usar a região real; preenchida, ela apontaria para o emulador — e a guarda recusa o startup.

## Verificação

A fatia está pronta quando, com o ambiente aplicado do zero:

1. `terraform apply` termina e a instância fica `running`.
2. O domínio responde em HTTPS com certificado válido do Let's Encrypt.
3. A tela de login do Angular carrega.
4. O login com a conta semeada funciona do navegador, e de novo do celular — o
   mesmo critério que a Azure teve que passar em 2026-06.
5. `docker compose logs api` na instância, por SSH, não mostra
   `SelfLog` do Serilog reclamando: falha de sink é silenciosa neste projeto e já
   custou meses.
6. `POST /api/auth/forgot-password` responde **202** e a mensagem aparece na fila
   SQS real (`aws sqs receive-message`). Ninguém a consome — é o esperado nesta
   fatia.
7. O add-on `AutoSnapshot` está `Enabled` e, no dia seguinte ao primeiro boot,
   `aws lightsail get-auto-snapshots` lista um snapshot com status `Success`.
   Backup que nunca foi visto acontecer não é backup.
8. `terraform destroy` deixa a conta em custo zero, fora do bucket de estado, do
   ECR, dos snapshots retidos e da zona do Route 53, se houver — e da chave de
   acesso do `prumo-dev-box`, que foi criada fora do Terraform e precisa ser
   apagada à mão.

## O que fica para trás, de propósito

- **O caminho Azure no código.** `Azure.Messaging.ServiceBus` em
  `Prumo.Infrastructure` e `Prumo.Notifications`, o `AcsEmailSender`, o
  `ServiceBusNotificationPublisher` e o `ServiceBusNotificationWorker` continuam
  compilando e testados. Arrancá-los agora mexe no caminho que acabou de ser
  verificado de ponta a ponta e não entrega nada. Vira limpeza nomeada depois que
  a AWS servir tráfego.
- **`servicebus/config.json`** já é órfão: o `docker-compose.yml` não o referencia
  desde 2026-09-03. Sai na mesma limpeza.
- **A árvore `azurerm` e o tfstate na Azure.** Enquanto a AWS não servir tráfego,
  destruir a única coisa que sabe onde a Azure estava é apostar sem necessidade.

## Riscos conhecidos

- **São 2 GB para quatro containers**, um deles Postgres e outro .NET. Deve caber, com folga pequena. Se apertar, `medium_3_0` (4 GB, US$ 24/mês) é uma linha de Terraform — e mesmo assim continua abaixo do que a EC2 custava.
- **A chave IAM em disco é o preço do Lightsail.** Escopo mínimo: enfileirar numa fila, puxar duas imagens. Um vazamento não lê segredo nem cria recurso. Pequeno, contido, mas não zero — e na versão EC2 era zero.
- **A porta 22 voltou a existir**, restrita ao IP de casa. IP residencial que muda derruba o acesso, e sem Session Manager a única saída é um `apply` corrigindo o CIDR. Perder a chave privada é pior: só recriando a máquina.
- **Uma caixa continua sendo um ponto único de falha.** A decisão 10 resolve
  *perda de dados*, não *indisponibilidade*: se a instância morre, o piloto fica
  fora do ar até alguém recriar a máquina e restaurar o snapshot — na melhor das
  hipóteses meia hora, e só se alguém estiver olhando. Para um piloto com uma
  pessoa vendendo, é aceitável; para clientes pagantes, não é.
- **O snapshot é do disco, não do Postgres.** É um backup a frio, com o banco
  escrevendo: recupera, mas pode exigir recuperação de crash do próprio Postgres
  no boot. `pg_dump` para o S3 é o passo seguinte quando houver dado que doa
  perder, e não está nesta fatia.
- **Let's Encrypt tem limite de emissão por domínio.** Aplicar e destruir o
  ambiente muitas vezes no mesmo dia pode esbarrar nele. O volume do Caddy
  preserva o certificado entre subidas se não for destruído junto.

## As specs seguintes, na ordem provável

1. **SES e o worker.** É a que tem prazo fora do seu controle: sair do sandbox é
   ticket de suporte, leva dias e exige domínio verificado com DKIM. O domínio
   desta fatia já serve. Comece o pedido cedo.
2. **Endurecimento do item 11.** Rede privada, IAM database auth, `SslMode=VerifyFull`
   e, por último, RLS com `FORCE`. É aqui que o compute da decisão 2 sai do
   Lightsail — nada disso cabe nele, e é justamente o momento em que o preço
   deixa de ser o critério dominante porque já existe cliente pagando.
3. **Limpeza do caminho Azure.** Código, `servicebus/config.json`, árvore
   `azurerm` e o tfstate na Azure — tudo junto, depois que a AWS servir tráfego.

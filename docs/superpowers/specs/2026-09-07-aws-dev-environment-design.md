# Design: ambiente dev na AWS (fase 2 da migração, primeira fatia)

> Escrito em 2026-09-07. A decisão de nuvem foi tomada pelo Nickolas nesta data:
> **migração full para AWS**. Isto cobre a **primeira fatia** da fase 2 — o
> mínimo que sobe e permite fazer login de um navegador. Não cobre SES, worker,
> rede privada, IAM database auth nem RLS; cada um vira sua própria spec.

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
| 2 | Compute | **Uma EC2 `t3.small` com `docker compose`** | Escolhido pelo Nickolas em 2026-09-07, com a ressalva de "A alternativa recusada" à vista. Uma caixa, uma unidade de deploy, sem VPC elaborada, sem ALB, sem task definition |
| 3 | Banco | **Postgres no próprio compose** | Monta `db/roles.sql` no entrypoint, então os dois roles do item 11 nascem sozinhos em volume novo. Custo zero além do EBS |
| 4 | Fila | **SQS real, sem worker ainda** | Fila e DLQ de verdade custam quase nada e provam o caminho de IAM sem chave. A API publica; ninguém consome até a spec do SES |
| 5 | Borda e TLS | **Caddy no compose, Let's Encrypt** | Sem custo de ALB, sem renovação para gerenciar. O JWT deixa de viajar em claro e o domínio já fica de pé para o SES |
| 6 | Frontend | **A imagem nginx do Angular, intacta** | Aquele `proxy_pass` literal foi o quinto e último bug do primeiro deploy na Azure. Caddy termina TLS e repassa; o nginx continua fazendo o proxy de `/api` |
| 7 | Deploy | **ECR + GitHub Actions, aplicado por SSM Run Command** | Sem porta 22, sem chave SSH guardada. O OIDC do GitHub para IAM substitui o do Entra. É o pedaço de CI que sobrevive a qualquer compute depois |
| 8 | Região | **`us-east-1`** | Já é o default no `appsettings.json` e o caminho mais batido para sair do sandbox do SES. `sa-east-1` corta a latência para o Brasil e custa notavelmente mais — reabrir quando houver usuário de verdade |
| 9 | Terraform da Azure | **Fica onde está, intocado** | Mesma razão do código: arrancar agora não entrega nada. A árvore `azurerm` é apagada quando a AWS servir tráfego |

## A alternativa recusada: ECS Fargate

Foi a minha recomendação, e não foi a escolha. Fica registrada porque a razão
importa quando esta decisão for revisitada.

ECS Fargate é o equivalente direto do Container Apps e é o formato para onde o
alvo endurecido vai de qualquer jeito: rede privada, IAM database auth e um
sidecar cabem nele sem trocar de compute. O preço era mais Terraform agora — VPC,
subnets, ALB, target group, listener, task definition — e ~US$18/mês de ALB
enquanto ligado.

A EC2 com compose chega ao "sobe e loga" com muito menos peça, e casa com o
hábito de ligar e destruir o ambiente inteiro. **O custo é que quase todo o
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
   |  EC2 t3.small  (VPC default, SG so 80/443)      |
   |                                                 |
   |   caddy ---> web (nginx + Angular) ---> api     |
   |                                          |      |
   |                                          v      |
   |                                      postgres   |
   +-----------------------|-------------------------+
                           |  instance profile (sem chave)
              +------------+------------+
              |            |            |
             SQS          SSM          ECR
        notifications   Parameter    pull das
        + -dlq            Store       imagens
```

Nenhuma porta além de 80 e 443 é alcançável. O Postgres não publica porta: existe
apenas na rede do compose. O acesso administrativo à instância é por **SSM Session
Manager**, então não há porta 22 aberta nem chave SSH para vazar.

## Terraform

Árvore nova em `terraform/aws/dev/` no repo `SaaSBasePlatform-DevOps`, ao lado da
árvore `azurerm` existente, que não é tocada.

**Estado remoto:** bucket S3 novo, com *locking* nativo do S3 (`use_lockfile`),
sem tabela DynamoDB. Isso exige subir o `required_version` da árvore AWS para
`>= 1.10.0`; a árvore Azure continua em `>= 1.7.0`.

O que a árvore cria:

- **Rede:** usa a VPC default. Um security group liberando 80 e 443 de `0.0.0.0/0`
  e nada mais. Sem regra de saída restrita — o compose precisa puxar do ECR.
- **Instância:** `t3.small` (x86 de propósito: `t4g` seria ~20% mais barata, mas
  exigiria imagens arm64, e o `Dockerfile` de hoje não é multi-arch). IP elástico,
  para o registro DNS não mudar a cada recriação.
- **Instance profile** com quatro permissões e nada mais: `sqs:SendMessage` na
  fila de notificações, `ssm:GetParameter` no prefixo dos segredos, leitura do
  ECR, e a policy gerenciada do agente SSM.
- **Filas:** `notifications` e `notifications-dlq`, com redrive de
  `maxReceiveCount = 5` e visibilidade de 60s — os mesmos números do
  `elasticmq/elasticmq.conf`, para o local não ser mais permissivo que o remoto.
- **ECR:** dois repositórios, `prumo-api` e `prumo-web`.
- **OIDC:** provider do GitHub e uma role assumível pelos três repos, substituindo
  as federated credentials do Entra.
- **Parâmetros SSM** (SecureString): JWT key, `Seed:AdminPassword` e as senhas dos
  roles `prumo_app` e `prumo_migrator`. Os valores **não** entram no Terraform —
  são postos à mão uma vez, e a árvore só declara os parâmetros.
- **Route 53:** zona e registro A apontando para o IP elástico.

**O user-data** lê os parâmetros do SSM, escreve o `.env` que o compose consome,
faz login no ECR e sobe o compose. Ligar o ambiente é `terraform apply`; desligar
é `terraform destroy`.

## O compose de deploy

Arquivo próprio, versionado no repo de DevOps — **não** é o `docker-compose.yml`
da raiz do repo da aplicação, que é de desenvolvimento e monta código local.

Quatro serviços: `caddy`, `web`, `api`, `postgres`. O `postgres` monta
`db/roles.sql` em `docker-entrypoint-initdb.d` exatamente como no local, e o
`Caddyfile` tem uma linha de domínio — o resto do TLS o Caddy resolve sozinho.

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

O que o `.env` gerado pelo user-data precisa pôr no ambiente da API:

```
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=postgres;...;Username=prumo_app;...
ConnectionStrings__MigratorConnection=Host=postgres;...;Username=prumo_migrator;...
Database__MigrateOnStartup=true
Notifications__Provider=Sqs
Sqs__QueueUrl=https://sqs.us-east-1.amazonaws.com/<conta>/notifications
Sqs__Region=us-east-1
Jwt__Key=<do SSM>
Seed__AdminPassword=<do SSM>
```

`Sqs__ServiceUrl` **fica ausente**. É o que faz o SDK usar a região real e a
credencial do instance profile; preenchida, ela apontaria para o emulador e ainda
trocaria a role da instância por credencial fixa — e a guarda recusa o startup.

## Verificação

A fatia está pronta quando, com o ambiente aplicado do zero:

1. `terraform apply` termina e a instância fica `running`.
2. O domínio responde em HTTPS com certificado válido do Let's Encrypt.
3. A tela de login do Angular carrega.
4. O login com a conta semeada funciona do navegador, e de novo do celular — o
   mesmo critério que a Azure teve que passar em 2026-06.
5. `docker compose logs api` na instância, via SSM Session Manager, não mostra
   `SelfLog` do Serilog reclamando: falha de sink é silenciosa neste projeto e já
   custou meses.
6. `POST /api/auth/forgot-password` responde **202** e a mensagem aparece na fila
   SQS real (`aws sqs receive-message`). Ninguém a consome — é o esperado nesta
   fatia.
7. `terraform destroy` deixa a conta em custo zero, fora do bucket de estado, do
   ECR e da zona do Route 53.

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

- **`t3.small` são 2 GB para quatro containers**, um deles Postgres e outro .NET.
  Deve caber, com folga pequena. Se apertar, `t3.medium` é uma linha de Terraform.
- **Uma caixa é um ponto único de falha, e o backup é problema seu** — snapshot do
  volume EBS, à mão. Para um ambiente de desenvolvimento que se destrói de
  propósito, é aceitável; para produção, não é.
- **Let's Encrypt tem limite de emissão por domínio.** Aplicar e destruir o
  ambiente muitas vezes no mesmo dia pode esbarrar nele. O volume do Caddy
  preserva o certificado entre subidas se não for destruído junto.

## As specs seguintes, na ordem provável

1. **SES e o worker.** É a que tem prazo fora do seu controle: sair do sandbox é
   ticket de suporte, leva dias e exige domínio verificado com DKIM. O domínio
   desta fatia já serve. Comece o pedido cedo.
2. **Endurecimento do item 11.** Rede privada, IAM database auth, `SslMode=VerifyFull`
   e, por último, RLS com `FORCE`. É aqui que o compute da decisão 2 é trocado.
3. **Limpeza do caminho Azure.** Código, `servicebus/config.json`, árvore
   `azurerm` e o tfstate na Azure — tudo junto, depois que a AWS servir tráfego.

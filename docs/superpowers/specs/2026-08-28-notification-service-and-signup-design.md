# Design: serviço de notificação + item 8 (cadastro, confirmação e senha esquecida)

> Escrito em 2026-08-28. Cobre os dois últimos blocos de aplicação do MVP: o
> **serviço de notificação** (bloco 5 do roteiro de 2026-08-18) e o **item 8**.
> Eles vêm num design só porque o item 8 é o primeiro consumidor do serviço, e
> desenhá-los separados obrigaria a inventar duas vezes o mesmo contrato.

## Objetivo

Hoje ninguém entra no sistema sozinho: `POST /api/auth/register` existe e é
público, mas não há confirmação de e-mail, não há recuperação de senha e **não
há infraestrutura de e-mail nenhuma**. O admin cria a conta do membro com uma
senha que ele mesmo digita — o que significa que a senha inicial de todo mundo
passa pelo admin.

O alvo é fechar o ciclo de conta: **cadastrar → confirmar e-mail → ser adicionado
a um tenant → recuperar a senha sozinho**, com o e-mail saindo por um serviço
separado que pode falhar sem derrubar nada.

## A ordem que este design assume

Decidido em 2026-08-28: **terminar tudo o que é aplicação e só depois fazer um
pass único de Azure.** Por isso este design é **local-first** — tudo abaixo roda
e se verifica no `docker compose`, e o pass de Azure fica sendo "trocar o
emulador pelo recurso real e publicar o container", sem mudar código de domínio.

## Decisões

| # | Decisão | Escolha | Por quê |
|---|---|---|---|
| 1 | Provedor de e-mail | **Azure Communication Services** | Pay-per-use **sem piso mensal** (US$ 0,00025/e-mail), então criar o recurso não quebra a postura de custo zero. Subdomínio `*.azurecomm.net` grátis permite testar sem domínio próprio. SDK oficial, Terraform e Managed Identity — soma à história de Azure. O tier gratuito do SendGrid virou trial de 60 dias em 2025, então a opção que o backlog assumia deixou de existir |
| 2 | Fila | **Azure Service Bus Basic** | US$ 0,05 por milhão de operações, dead-letter nativo, e **emulador oficial em Docker**, o que dá paridade local com o mesmo código. Basic não tem tópicos nem sessões — para "envie este e-mail" não faz falta |
| 3 | Onde mora o serviço | **Projeto novo na mesma solution**, container próprio | Repo separado acrescentaria CI/CD sem acrescentar argumento. O limite que interessa é de processo e de deploy, não de repositório |
| 4 | Como se entra num tenant | **Admin adiciona pelo e-mail**, sem link com token | Decisão de 2026-08-18. É o que torna o e-mail *notificação e não autorização*, e é essa propriedade que permite o serviço ser best-effort |
| 5 | Admin criando conta com senha | **Sai** | Substituído por convite pendente. Duas portas para a mesma coisa confunde, e a atual faz a senha inicial de todo mundo passar pelo admin |
| 6 | Cadastro devolve sessão? | **Não** | Devolve "confirme seu e-mail". Login antes da confirmação responde 403 com motivo próprio, distinguível de senha errada |
| 7 | Usuários que já existem | **Migration marca todos como confirmados** | `RequireConfirmedEmail = true` trancaria para fora quem foi criado pelo admin antes disso |

## Arquitetura

### O contrato entre a API e o serviço

A API publica um evento na fila e **não espera resposta**. O serviço consome,
renderiza e envia.

```
Prumo.Api ──publica──> fila "notifications" ──consome──> Prumo.Notifications ──> ACS
                            │
                            └── dead-letter (Service Bus, nativo)
```

Um envelope só, com o tipo dentro, para a fila não virar uma por assunto:

```json
{
  "type": "email.confirmation | email.password-reset | email.tenant-invitation",
  "to": "pessoa@exemplo.com",
  "correlationId": "<guid>",
  "data": { "...": "campos do tipo" }
}
```

**O que o evento NÃO carrega:** senha, hash, token de sessão. O token de
confirmação vai no `data` porque é o objeto da mensagem — mas é o token do
Identity, de uso único e expirável, não uma credencial.

**Idempotência:** Service Bus entrega ao menos uma vez, então o consumidor pode
ver a mesma mensagem duas vezes. Para e-mail, o efeito de um envio duplicado é um
e-mail repetido — irritante, não perigoso. Guardar `correlationId` visto num
`IMemoryCache` curto no serviço resolve o caso comum sem inventar tabela nova.

### O serviço

`Prumo.Notifications`, um Worker Service: `ServiceBusProcessor` → resolve o
template pelo `type` → `IEmailSender`. Duas implementações de `IEmailSender`:

- **`AcsEmailSender`** — `Azure.Communication.Email`, usado em produção.
- **`FileEmailSender`** — grava o e-mail renderizado em disco (e loga o link).
  É o default em Development, e é o que permite verificar o item 8 inteiro sem
  provedor, sem domínio e sem custo.

Templates em arquivos `.html` embutidos, com substituição simples de
placeholders. Nada de engine de template — são três e-mails.

### O que a API passa a ter

- `POST /api/auth/register` — cria o usuário **não confirmado**, publica
  `email.confirmation`, devolve 202 sem token.
- `POST /api/auth/confirm-email` e `resend-confirmation`.
- `POST /api/auth/forgot-password` — **sempre 202**, exista o e-mail ou não.
  Responder 404 para e-mail inexistente transforma o endpoint em enumerador de
  contas.
- `POST /api/auth/reset-password`.
- `SignIn.RequireConfirmedEmail = true`, e o login devolve 403 com um código
  distinguível quando falta confirmar.
- `POST /api/tenants/{id}/invitations` — o admin digita o e-mail. Conta existe →
  vira membro na hora; não existe → linha em `TenantInvitations`, que o
  `RegisterAsync` resolve quando alguém se cadastra com aquele e-mail.

`TenantInvitations`: `(TenantId, Email, Role, InvitedBy, CreatedAt, AcceptedAt)`,
`ITenantScoped`, única por `(TenantId, Email)` enquanto pendente.

### O que o frontend passa a ter

Cadastro, "confirme seu e-mail", esqueci a senha, nova senha, e a tela de
**"aguardando convite"** para quem confirmou e não pertence a tenant nenhum. A
tela de membros troca o diálogo de criar usuário pelo de convidar, e passa a
listar convites pendentes com opção de cancelar.

### Publicação da mensagem: o ponto que mais pode dar errado

A API grava no banco (usuário criado, convite criado) **e** publica na fila. Se a
publicação falhar depois do commit, a pessoa existe e não recebe e-mail.

**A escolha aqui é assumir isso, não resolver com outbox.** Justificativa: o
efeito de perder a mensagem é "não fui avisado", e todo fluxo tem reenvio manual
(`resend-confirmation`, "esqueci a senha" de novo, o admin reenvia o convite).
Um outbox transacional acrescentaria tabela, worker e ordenação para proteger
contra uma falha cujo remédio já existe e é de um clique. Fica registrado como
decisão consciente — se um dia entrar cobrança por e-mail, a conta muda.

## Testes e verificação

Na suíte:
- `RegisterAsync` cria usuário não confirmado, não devolve token e publica um
  evento (publisher fake).
- Login com e-mail não confirmado → 403 com o código próprio.
- `forgot-password` responde 202 para e-mail inexistente **e não publica nada**.
- Convite para e-mail sem conta vira linha pendente; cadastro com aquele e-mail
  consome a linha e cria a associação; cadastro com outro e-mail não.
- Consumidor: mensagem malformada vai para dead-letter em vez de derrubar o
  processo.

Manual, contra o `docker compose` (Postgres + Redis + emulador do Service Bus):
1. Cadastro → e-mail aparece em disco pelo `FileEmailSender` → abrir o link →
   login passa a funcionar.
2. Esqueci a senha → link → nova senha → login com a nova, e a antiga falha.
3. Admin convida e-mail sem conta → cadastro depois → a pessoa cai dentro do
   tenant com o cargo certo.
4. Matar o serviço de notificação e repetir o cadastro: **a conta é criada
   normalmente** e a mensagem espera na fila; subir o serviço entrega o e-mail.
   É essa a prova de que o limite do microserviço está no lugar certo.

## Fora de escopo, de propósito

- **Tudo de Azure**: criar o recurso de ACS, domínio verificado, Service Bus real,
  Container App com escala a zero por KEDA, e o passo de migration do item 11.
  Vão no pass único de infra, que exige `az login`.
- **Outbox transacional** — ver a justificativa acima.
- **Preferências de notificação, histórico de e-mails enviados, outros canais.**
  O serviço nasce com um canal e três mensagens.

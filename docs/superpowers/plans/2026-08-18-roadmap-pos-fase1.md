# Roteiro pós-fase 1 — o que falta para fechar o MVP

**Escrito em 2026-08-18**, depois de a fase 1 de isolamento de tenant ser verificada de
ponta a ponta e de sete decisões pendentes serem fechadas numa sessão só.

Isto **não é um plano de implementação** — é a ordem e o porquê dela. Cada bloco vira (ou
já é) um plano próprio, porque são subsistemas independentes e um documento só ficaria
grande demais para executar.

---

## As sete decisões de 2026-08-18

| # | Assunto | Decisão |
|---|---|---|
| 1 | Tela de membros (item 5) | **Uma tela só, linha expansível.** Cargo no dropdown da linha, feature roles no painel que expande. `/admin/members` e `/admin/tenant` somem. |
| 2 | Role nova (item 3) | **Nasce vazia.** Zero permissão, zero `ResourcePermission`. Fail-closed. |
| 3 | Roles/`Permission` no seeder vs migration | **Fica no seeder por ora.** O item 3 muda de novo como essa lista nasce; mover agora é trabalho que ele desfaz. |
| 4 | Log do support-access | **Entidade própria** (`SupportAccessLog`), fora do `PermissionAuditLog`. |
| 5 | Microserviços | **Extrair só o serviço de notificação.** Não fatiar por módulo. |
| 6 | Convite (item 8) | **Admin adiciona por e-mail.** Sem link com token. |
| 7 | Commits antigos em português | **Ficam como estão.** Só os 3 de hoje foram reescritos. |

Publicação do repo: **decidida como "daqui a um tempo"**, e os quatro itens de
preparação (varredura de segredo no histórico, item 11, `NU1903`, README) vão **juntos,
num pass só, mais para frente**. Não entram nesta semana.

---

## A ordem, e por que ela

### 1. Fase 2 — limpeza do acesso a dados *(primeiro, e é backend puro)*

Remover os **86 `IgnoreQueryFilters`** e as checagens redundantes, agora que
`[TenantModule]` resolve o tenant pela rota e o filtro global é fail-closed.

**Por que primeiro, e não o RBAC que dá valor visível:** `TenantService.cs` tem **12**
dessas chamadas, **duas delas na query de membros** — exatamente o método que o item 4
vai reescrever e que o item 5 vai consumir. Verificado, não é suposição. Fazer o RBAC
antes significa escrever código novo em cima de um padrão que a fase 2 vai apagar, e
depois reescrever. Fazer a limpeza antes deixa o resto da semana em terreno limpo.

**O contra, honesto:** é o bloco maior e o único sem nada visível no fim. Se a semana
apertar, é o que mais dói ter começado. Se você preferir garantir as telas primeiro, a
ordem alternativa é 2 → 3 → 1, e o custo é retrabalho em `TenantService`.

Plano detalhado: `2026-08-18-phase2-data-access-cleanup.md`.

### 2. Item 4 — as roles do usuário aparecerem

Bug confirmado vivo no smoke test: o dashboard diz "Roles atribuídas: 0" e "Nenhuma role
atribuída" para quem comprovadamente tem role. Duas causas somadas — a lista de usuários
nasce com `roles: []` chumbado, e o `TenantMemberDto` não traz as roles.

**Por que antes do item 5:** a linha expansível da tela nova só mostra as chaves de módulo
sem N+1 se o DTO já vier com elas. O item 4 é a fundação do item 5, não um vizinho.

### 3. Item 5 — fundir as telas de membros

A tela única com linha expansível. Some `/admin/members` e `/admin/tenant`; os dados do
tenant descem para um rodapé editável. **Aqui também morre a verruga achada no smoke
test:** `app.routes.ts:94` é a única rota admin sem `canActivate`, e é por isso que
"Tenant" aparecia no menu de um Member simples — a rota deixa de existir.

O `[+ Adicionar membro]` desta tela é o ponto de entrada que o item 8 vai reusar.

### 4. Item 3 — criar e excluir role

Backend (criar/excluir; listar já saiu em 2026-08-18) + página separada no frontend.
Role nasce vazia.

**O que este item obriga a revisitar:** `Permissions.Roles.All` é hoje uma lista `readonly`
travada por `CanonicalRolesTests`. Com role virando dado criado pelo usuário, ela vira
consulta ao Identity e aqueles testes mudam de sentido. É também o momento de reabrir a
decisão 3 (seeder vs migration), que foi adiada exatamente para cá.

### 5. Serviço de notificação — o microserviço, com motivo de verdade

Um serviço separado, com container e deploy próprios no repo de DevOps, consumindo fila.

**Por que este e não fatiar por módulo:** RH, Financeiro e Contas a Pagar compartilham
tenant, usuário e contabilidade — separá-los viraria chamada síncrona entre serviços, que
é o antipadrão que lê como distributed monolith. Notificação é o oposto: assíncrona, sem
transação compartilhada com ninguém, e a falha dela **não pode** derrubar o cadastro.
É um limite que se defende numa entrevista.

**A decisão 6 é o que torna isso possível.** Como o convite é o admin adicionando por
e-mail, o e-mail é *notificação e não autorização*: se o envio falhar, ninguém entra em
tenant nenhum por engano — a pessoa só não é avisada. É essa propriedade que permite o
serviço ser best-effort e assíncrono. Com link-com-token seria o contrário: o e-mail
viraria o mecanismo de acesso, e um serviço que pode falhar não poderia carregá-lo.

**Escopo:** publicar evento na fila a partir da API; serviço consome e envia; provedor de
e-mail escolhido; Terraform + workflow no repo de DevOps. Entra aqui a competência de
infra que o repo de DevOps existe para mostrar.

### 6. Item 8 — cadastro, confirmação de e-mail, esqueci a senha

O maior, e o único que não é trabalho de uma tarde. Depende do serviço de notificação
estar de pé e do `[+ Adicionar membro]` do item 5.

Inclui: `SignIn.RequireConfirmedEmail = true`, os quatro endpoints (confirmar, reenviar,
esqueci, resetar), as quatro telas, e a tela de "aguardando convite" para quem se
cadastrou e não pertence a tenant nenhum. Os tokens expiráveis já vêm prontos do Identity
(`AddDefaultTokenProviders()` já está ligado) — não precisa inventar nada.

---

## Fora desta semana, de propósito

- **Preparação para publicar** (varredura de segredo no histórico, item 11 / sair do
  superuser, `NU1903`, README) — pass próprio, mais para frente, os quatro juntos.
- **Pass de infra** (`2026-08-11-infra-rename-pass.md`) — renomear os 3 repos, corrigir as
  federated credentials no Entra. Exige `az login`, é tarefa a dois. **Acrescentar à lista
  daquele plano:** o `docker-compose.yml` ainda usa nomes pré-rename
  (`saasbase-postgres`, `saasbase-redis`), e o banco continua `SaaSBasePlatformDb`.
- **Verrugas pequenas:** o chip do usuário sobrepondo o avatar no header; os módulos sem
  controller que ainda estão no catálogo de permissões (`products`, `customers`, `stock`).
- **Fora do escopo do MVP por decisão antiga:** dashboards; contas a pagar / plano de
  contas / razão geral.

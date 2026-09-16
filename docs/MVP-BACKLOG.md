# Backlog para fechar o MVP

> Levantado em 2026-08-05, a partir de uma varredura no código dos dois repos
> (`SaaSBasePlatform` e `SaaSBasePlatform-Angular`). Cada item abaixo foi
> **verificado no código**, não é chute — os arquivos e linhas estão citados.

## Contexto: o que foi corrigido antes deste backlog

Dois bugs que deixavam o menu vazio (só "Administração › Tenant" e o Dashboard
apareciam) já foram corrigidos e estão na `main` dos dois repos:

- **Backend `73dc458`** — `GET /api/resources/my-permissions` retornava 500 quando
  não havia tenant selecionado. Sem tenant o filtro global do EF é desligado, a
  query lia os recursos de *todos* os tenants e o `ToDictionary(r => r.Code, …)`
  estourava com chave duplicada. O guard de `HasTenant` subiu para o topo de
  `BuildUserPermissionsDto`, o que de quebra fechou um vazamento cross-tenant.
- **Frontend `22aba79`** — a API serializa enums como string (`"Full"`), mas o
  `PermissionLevel` do Angular é numérico. `"Full" >= 1` é sempre `false`, então
  **todo** item de menu e rota com `resourceCode` era negado. Os payloads agora
  são normalizados na entrada (`normalizeUserResourcePermissions`).

⚠️ **Pendente de verificação:** isso foi validado por testes unitários e build
(backend 63/63, frontend 5/5), **não** ponta a ponta na UI. Ao retomar: reiniciar
a API (o processo em `localhost:5201` roda o binário antigo) e fazer logout/login
para limpar o `localStorage` com os níveis em string. Se ainda faltar algum
módulo no menu, provavelmente é recurso faltando no `TenantBootstrapSeeder`
daquele tenant, não mais gating.

---

## 1. Botão "Abrir painel admin" não faz nada — ✅ FEITO em 2026-08-18

**Causa era:** `dashboard-overview.component.html:60` apontava para `routerLink="/admin"`,
e essa rota não existe no `app.routes.ts` — só existem `/admin/permissions`,
`/admin/users-roles`, `/admin/tenant` e `/admin/members`. Existe um
`modules/admin/admin-panel.component.ts` órfão, nunca registrado em rota nenhuma.

**Feito:** o link passou a apontar para `/admin/permissions`. O componente órfão **não**
foi registrado de propósito — o próprio HTML dele diz "Integração com backend pendente",
e `dashboard/admin` é outro placeholder ("Dashboards administrativos em breve"). Registrar
qualquer um dos dois entregaria uma página morta. Verificado no browser: o clique navega.

## 2. Role "Usuario" fantasma na tela de Permissões por Role — ✅ FEITO em 2026-08-18

**Causa era:** `permissions-management.component.ts:49` tinha a lista chumbada no front:

```ts
readonly availableRoles: string[] = ['Administrador', 'Funcionario', 'Cliente', 'Usuario'];
```

As roles reais do backend (`Permissions.Roles`) são: `Administrador`, `Funcionario`,
`Cliente`, `RH`, `Financeiro`, `ContasAPagar`. Ou seja: além de `Usuario` não existir,
**faltavam três roles** na tela (RH, Financeiro, ContasAPagar).

**O bloqueio declarado aqui estava errado.** Este item nunca dependeu do item 3: a linha
entrou em `42dd62a` (2026-03-09) e nunca mais foi tocada, e desde `b2046cd` (2026-06-20)
já existia `GET /tenants/{id}/assignable-roles`, que a tela *Roles por Usuário* consome.

**Feito, pela raiz e não pelo sintoma** (apagar só o `Usuario` deixaria as três roles
faltando): `Permissions.Roles.All` no Domain + `GET /api/permissions/roles` no
`PermissionsController`, e o componente passou a buscar de lá. Três testes em
`Prumo.Tests/Domain/CanonicalRolesTests.cs` travam a lista, inclusive um que quebra se
`All` e `AssignableFeatureRoles` divergirem.

**Por que `Administrador` entra na lista e `assignable-roles` não serviu:** aquele endpoint
devolve só as roles que um admin de tenant pode *atribuir* a membros, e de propósito exclui
o master admin. Mas esta tela configura `RolePermission`, e as do `Administrador` são reais
— o `PermissionAuthorizationHandler` lê os claims `permission` montados a partir delas.
Tirar o master daqui removeria funcionalidade de verdade. (O bypass do master em
`ResourcePermissionService` é do *outro* sistema, o de `ResourcePermission` por recurso.)

## 3. Criar role — ✅ 3A e 3B FEITOS em 2026-08-26

**Causa:** confirmado — não há `RolesController`. Nenhum endpoint de **criar/excluir**
role. Para *listar* já existem dois, ambos devolvendo listas fixas do Domain:
`GET /api/tenants/{tenantId}/assignable-roles` (`AssignableFeatureRoles`, sem o master) e
`GET /api/permissions/roles` (`Roles.All`, com o master — acrescentado em 2026-08-18 pelo
item 2).

**O que precisa:**
- Backend: criar / excluir role. A listagem já está resolvida.
- **✅ DECIDIDO em 2026-08-18: role nova nasce vazia** — zero `Permission` e zero
  `ResourcePermission`. O admin concede depois, na tela de Permissões por Role. É
  fail-closed, combina com a fase 1, e não inventa acesso que ninguém pediu. O custo
  aceito é que a role precisa passar por duas telas para ficar útil.
- Decidir o que acontece com as listas fixas do Domain quando roles viram dado: hoje
  `Roles.All` é `readonly` e testado; com criação dinâmica ele vira consulta ao Identity,
  e `CanonicalRolesTests` muda de sentido. **É aqui que o desvio do spec (roles no seeder
  vs migration) volta à mesa — decidido em 2026-08-18 esperar este item.**
- Frontend: ~~página nova, **separada**, não embutida na tela de Permissões por Role~~
  — **decisão revertida em 2026-08-26**, ver abaixo.

**Esforço:** médio. **Já não bloqueia o item 2** — aquele foi fechado em 2026-08-18.

---

### Decisões de 2026-08-26 — e o achado que as motivou

**O achado, verificado:** existem **três** sistemas de permissão, e **só um gateia**.

| Sistema | Guarda | Quem gateia com isso |
|---|---|---|
| `ResourcePermission` | Role × Recurso → `None/Read/Write/Full` | **tudo**: menu, guards de rota e a API via `[TenantModule]` |
| `RolePermission` + catálogo `Permissions.cs` | Role × string (`employees.edit`) | **nada** |
| `TenantUserRole` | quem tem qual role no tenant | quais módulos aparecem |

O `RolePermission` não é subutilizado, está **desconectado**: **zero** controllers usam
`[Authorize(Policy=…)]` (os 32 `[Authorize]` são por role ou só autenticação), e no
frontend o `hasPermission()` existe mas **ninguém o chama**. As policies são registradas
no startup (`AuthenticationConfiguration.cs:58`) e o `PermissionAuthorizationHandler`
existe, mas nada os consome. **Logo, a tela "Permissões por Role" configura hoje um
sistema que não tem efeito em autorização nenhuma.**

**Consequência prática que motivou a pergunta do Nickolas** ("como faço um funcionário
que vê mas não edita?"): esse caso **já é expressável** — é `ResourcePermission` de nível
`Read`, porque o `[TenantModule]` infere o nível do verbo (`GET`→`Read`,
`POST/PUT/PATCH`→`Write`, `DELETE`→`Full`). O endpoint que concede já existe
(`POST /api/resources/assign`, com `roleId`+`resourceId`+`Level`). **Falta só a tela.**

**Decisão 1 — aposentar o `RolePermission`.** Fica um sistema só, o de níveis. Saem o
catálogo de ~40 permission strings, as policies, o handler e o `CanonicalRolesTests`.

**Decisão 2 — a tela "Permissões por Role" vira "Roles"** e ganha criar/excluir +
a grade Recurso × Nível. **Isto reverte a decisão de 2026-08-18** ("página separada"),
e o motivo da reversão é o achado acima: separar as telas só fazia sentido enquanto se
acreditava que os dois sistemas eram ambos reais. Com um só, são a mesma tela.

**Decisão 3 — fatiar em 3A e 3B**, porque a aposentadoria toca dois lugares perigosos:
o `AuthService.cs:280` chama `GetUserPermissionsForTenantAsync` **dentro da emissão do
token** (é onde a fase 2 quase quebrou o login), e o `PermissionAuditLog` é modelado em
torno de `PermissionId`/`PermissionName` — precisa ser **remodelado** para auditar
Recurso × Nível, não simplesmente apagado.

- **3A (aditivo, primeiro):** criar/excluir role + grade Recurso × Nível. Nada é
  removido, login e auditoria não são tocados. No fim, a role "só leitura" já funciona.
- **3B (limpeza, depois):** aposentar `RolePermission` + catálogo + policies + handler,
  remodelar o audit log, e converter o `PermissionsController` — que é o único usuário de
  `[RequireResourceAccess]` e já estava marcado como pendência desde a fase 2.

**Alcance medido:** 28 arquivos citam `RolePermission`, mas **12 são migrations**
(histórico, não se toca) — o código vivo são ~14 arquivos, mais o frontend.

---

### ✅ 3A FEITO em 2026-08-26

Plano e registro de execução:
`docs/superpowers/plans/2026-08-26-item3a-criar-role-e-grade-de-niveis.md`.
Backend na branch `feature/item3a-tenant-roles` (6 commits), frontend na `main` do
repo Angular (2 commits). Suíte **110** (eram 101).

**O objetivo, medido:** um membro cuja única chave é a role "Leitura" (com `Read` em
`HR.Employees`) recebe **200 no GET** e **403 no POST** de `/employees`. É o
"funcionário que vê mas não edita" que originou o item.

**O que entrou:** `ApplicationRole.TenantId` nullable + índice
`(NormalizedName, TenantId)` **`NULLS NOT DISTINCT`**; `TenantRoleAdminService` e
`TenantRolesController` (rota por tenant, `[TenantModule("Role.Management")]`);
`assignable-roles` passou a consultar o Identity; e a tela **"Roles"**, que substituiu
"Permissões por Role", com criar/excluir e a grade Recurso × Nível.

**A armadilha do Postgres que quase passou:** `NULL` não é igual a `NULL`, então sem
`NULLS NOT DISTINCT` o índice **não protegeria as roles canônicas** — duas "RH" globais
passariam. Verificado nos dois sentidos contra o PG 17.9.

**O bug que só a API viva pegou:** criar a role e listá-la como atribuível funcionavam,
mas **atribuí-la a um membro devolvia 400** — `AssignFeatureRoleAsync` ainda validava
contra a lista fixa do Domain. Build e 108 testes verdes com o fluxo quebrado no meio.
O mesmo método resolvia a role **só por nome**, o que com homônimas pegaria a linha
errada; **a auditoria dos usos de `RoleManager` não viu este site porque ele consulta
`_db.Roles` direto.**

**Ainda no banco de dev, de propósito:** a role `Leitura` e o usuário
`leitor@teste.local`, como demonstração viva do caso de uso.

### 3B — o que sobrou

Aposentar `RolePermission` + o catálogo `Permissions.cs` + as policies +
`PermissionAuthorizationHandler`; **remodelar** o `PermissionAuditLog` (hoje é
`PermissionId`/`PermissionName`, precisa virar Recurso × Nível); e converter o
`PermissionsController`. Cuidado com `AuthService.cs:280`, que chama
`GetUserPermissionsForTenantAsync` **dentro da emissão do token**.

## 4. Contagem de roles por usuário sempre mostra 0

**Duas causas somadas:**

1. `users-roles-management.component.ts:88` monta a lista de usuários com
   `roles: []` chumbado, e só busca as roles de verdade quando você **clica** no
   usuário (`loadSelectedUserRoles`). Então a coluna `rolesCount` nasce 0 para
   todo mundo.
2. Mesmo clicando no `admin@SBP.com` dá 0, e aí é correto: ele tem a role
   **global** `Administrador`, mas nenhuma *feature role* por tenant. A tela só
   conta feature roles.

**O que precisa:** incluir as roles no `TenantMemberDto`
(`Prumo.Application/DTOs/Tenants/TenantDtos.cs:23`) para o
`GET /tenants/{id}/members` já devolver tudo numa tirada só (evita N+1), e
**decidir** se a role global entra na contagem.

**Esforço:** pequeno/médio.

## 5. Fundir as telas de membros — ✅ FEITO em 2026-08-26

O Nickolas quer: mover "adicionar membro" para *Roles por Usuário*, e remover as
telas *Membros do Tenant* e *Tenant*.

**A pegadinha que precisa ser explicada antes de mexer** — hoje existem
**dois sistemas de papel diferentes**, e cada tela mexe num:

| | O que é | Onde vive | Qual tela gerencia |
|---|---|---|---|
| **Owner / Admin / Member** | Seu *cargo administrativo* dentro do tenant. Diz se você pode convidar gente, trocar o papel dos outros, mexer no tenant. | coluna `TenantUsers.Role` (enum `TenantRole`) | `/admin/members` (CRUD completo) |
| **RH, Financeiro, ContasAPagar, Funcionario, Cliente** | Suas *feature roles*: a que módulos/telas você tem acesso. | tabela `TenantUserRoles` | `/admin/users-roles` |
| **Administrador** (global) | Role global do ASP.NET Identity, vale em **todos** os tenants. É o "master admin". | `AspNetUserRoles` | nenhuma tela hoje |

Analogia: **Owner/Admin/Member = seu cargo na empresa** (sócio, gerente,
funcionário). **Feature roles = as chaves que você tem** (chave do RH, chave do
financeiro). São coisas independentes: dá pra ser Member com a chave do
financeiro, ou Admin sem chave nenhuma de módulo.

Por isso, se fundir as duas telas numa lista só, a tela fundida precisa
gerenciar **os dois níveis** — senão some o único jeito de promover alguém a
Admin do tenant. Ou seja: uma tela com duas seções, não uma lista simples.

Sobre remover `/admin/tenant`: ela mostra nome/slug do tenant + lista de membros
só-leitura. É a única rota **sem guard** no `app.routes.ts` (foi por isso que ela
continuou aparecendo quando tudo o mais sumiu). Remover é ok, mas some a única
tela que mostra os dados do tenant.

**Esforço:** médio.
**✅ DECIDIDO em 2026-08-18 — uma tela só, com linha expansível.** Não fica mais
esperando conversa.

A lista de membros vira a única tela. O **cargo** (Owner/Admin/Member) é um dropdown
na própria linha; as **feature roles** ficam num painel que abre ao expandir a linha.
`/admin/members` e `/admin/tenant` somem, e o menu fica com uma entrada só. Os dados
do tenant (nome/slug) descem para um rodapé editável na mesma tela, para não perder
a única tela que os mostrava.

```
MEMBROS                              [+ Adicionar membro]

  Nome            Email           Cargo        Desde
▾ Nickolas        nick@x.com      [Owner  ▾]   27/04
   └─ Chaves de módulo:
      [✓] RH   [ ] Financeiro   [ ] ContasAPagar
      [ ] Funcionario  [ ] Cliente

▸ Maria           maria@x.com     [Member ▾]   05/05

Dados do tenant: BiomePampa (bmp)      [editar]
```

Por que este formato: mantém os **dois níveis** visíveis (o risco levantado acima),
é o menor número de cliques, e o `[+ Adicionar membro]` daqui é exatamente o ponto
de entrada que o item 8 vai reusar para convidar por e-mail.

**Dependência:** precisa do item 4 antes — a linha só consegue mostrar as chaves sem
N+1 se o `TenantMemberDto` já trouxer as roles.

---

### ✅ Feito em 2026-08-26 — como ficou

Plano e registro de execução: **`docs/superpowers/plans/2026-08-26-item5-tela-unica-de-membros.md`
no repo Angular** (o item é 100% frontend — nenhum arquivo do backend foi tocado).
Branch `feature/item5-single-member-screen`, 6 commits. **3 telas viraram 1**, 9 arquivos
apagados (862 linhas), suíte Angular 9 testes, backend 101/101 inalterado.

**Duas decisões estreitaram o escopo:**

1. **O rodapé de dados do tenant ficou só-leitura.** Nenhum `PUT /api/tenants/{id}` foi
   criado — ele não existia, e criá-lo trazia a questão de o slug ser usado no login por
   slug. Editar o tenant vira item próprio se fizer falta.
2. **O botão "Desativar usuário" saiu da tela.** Ele bate em `DELETE /api/auth/users/{id}`,
   que vale em **todos** os tenants; numa tela por tenant ele ficaria ao lado de "remover
   deste tenant" com ícone quase igual (`person_off` × `person_remove`) e raio de ação
   muito maior. O endpoint segue vivo, sem UI — volta como tela de master admin se
   precisar.

**Também morreu aqui** a verruga do smoke test de 2026-08-18: `admin/tenant` era a única
rota admin sem `canActivate`, e por isso "Tenant" aparecia no menu de um Member simples.
Ela e `admin/users-roles` viraram `redirectTo` para `/admin/members`.

**O bug que a execução descobriu, e que não era novo:** a API serializa `TenantRole` como
**string** (`"Owner"`), mas o enum do Angular é numérico — então a coluna Cargo dizia
"Desconhecido" e, pior, `myRole === TenantRole.Admin` era sempre falso, **escondendo o
dropdown de cargo e o botão de remover** de quem tinha direito a eles. A tela antiga tinha
exatamente o mesmo defeito (indexava um `Record<number, string>` e comparava com `1`/`2`),
então `/admin/members` **nunca** mostrou seus botões de gestão — a fusão só tornou isso
visível. É a mesma armadilha de `22aba79` com `PermissionLevel`. **Regra para o próximo
endpoint: enum atravessa o wire como string, normalize na fronteira** (aqui,
`toTenantRole`, com fallback para `Member`).

## 6. Botão "Configurações" no menu do usuário — ✅ FEITO em 2026-08-18

**Causa era:** `layout.component.html:25` — `<button mat-menu-item>` sem nenhum
`(click)`. Decisão: **ocultar**, não precisa fazer nada no MVP.

**Feito:** removido, com um comentário no lugar dizendo por quê. O menu do usuário agora
tem só "Trocar Tenant" e "Sair", conferido no browser.

## 7. Renomear o sistema — ✅ FEITO em 2026-08-11

O nome escolhido foi **Prumo** — do instrumento de prumo, e da expressão "estar a
prumo": em ordem, correto. Raiz de namespace `Prumo`; "Prumo ERP" fica como nome
de exibição (README, `<title>`, cabeçalho da UI), fora do código.

**Os ~18.500 do levantamento original estavam errados** — aquele número contava
`bin`/`obj`. Contando só arquivos rastreados pelo git, o escopo real era
**1.573 ocorrências em 204 arquivos** no backend e **48 em 116 arquivos** no
frontend. O rename levou minutos, não meio dia.

Três variantes que um find/replace ingênuo teria errado, e por isso a busca foi
feita com `saas.?base.?platform`, não com a string literal:

- `SaaS BasePlatform` **com espaço** — rodapé do login no frontend.
- `saas-baseplatform-erp` — nome do projeto Angular, em 5 arquivos que precisam
  concordar entre si (`angular.json`, `package.json`, `package-lock.json`,
  `karma.conf.js`, `Dockerfile`).
- `saas_baseplatform_*` — as chaves do `localStorage`.

**O que garantiu que o banco não fosse tocado:** `SaaSBasePlatformDb` não contém
`SaaS_BasePlatform` (não tem o underscore), então o replace do namespace nunca
encostou no nome do banco. Os nomes de banco ficaram **deliberadamente
inalterados** — renomear banco não é find/replace, e o item 12 já vai ter que
unificar isso quando consertar o `ApplicationDbContextFactory`.

**Efeitos colaterais, ambos aplicados:**
- JWT `Issuer`/`Audience` agora são `PrumoApi`/`PrumoClient`. Invalida os tokens
  em circulação — todo mundo reloga uma vez.
- As chaves do `localStorage` viraram `prumo_*`. Força logout, que aliás é
  exatamente o que a dívida de verificação do menu já exigia.

**Verificação:** backend `dotnet build` com 0 erros e **63/63 testes aprovados**;
frontend com build de produção limpo. Os diretórios mortos `BiomePampa.*` foram
apagados no mesmo pass (item 9), junto com o scaffold `BiomePampa.Api.http`, que
ainda apontava para `/weatherforecast/`.

### O que fica de fora do rename (por ora)

Isso é infra, custa caro e não muda nada funcional. Combinado: fica para quando
o MVP estiver fechado e o Nickolas for mexer no DevOps.

- Nome do repositório no GitHub (`SaaSBasePlatform`, `SaaSBasePlatform-Angular`)
- Imagem no ACR (`saasbase-api`)
- Repo `nickolascheidt/SaaSBasePlatform-DevOps`
- Recursos do Azure

## 8. Cadastro + confirmação de e-mail + esqueci a senha — ✅ FEITO em 2026-08-28

> Junto dele nasceu o **serviço de notificação** (`Prumo.Notifications`), que era o
> pré-requisito. Design e registro de execução em
> `docs/superpowers/specs/2026-08-28-notification-service-and-signup-design.md`.
>
> **O que ficou para o pass de Azure:** criar o recurso de ACS e o Service Bus real,
> publicar o container do worker e ligar o KEDA. Local, roda inteiro no
> `docker compose --profile notifications up -d`, com os e-mails indo para disco.

**Como ficou:**

- `POST /api/auth/register` responde **202 sem token**: a conta nasce não confirmada.
- Login recusa e-mail não confirmado com **403 + `code: "email_not_confirmed"`**, depois
  da checagem de senha — recusar antes contaria quais endereços têm conta.
- `confirm-email`, `resend-confirmation`, `forgot-password` (sempre 202) e `reset-password`.
- **Convite:** `POST /tenants/{id}/invitations`. E-mail com conta entra na hora; sem conta
  vira `TenantInvitation` pendente, que o cadastro consome. O admin **não define mais
  senha de ninguém** — `CreateAndAddMemberAsync` foi apagado.
- Cinco telas novas no Angular, e a de membros lista os convites pendentes.

**A armadilha que quase passou:** `SignIn.RequireConfirmedEmail` **não funcionaria** aqui.
Aquela opção só é aplicada por `PasswordSignInAsync`, e o `AuthService` usa
`CheckPasswordSignInAsync`, que a ignora. Ligar o flag pareceria certo e não faria nada.

**Redefinir a senha também confirma o e-mail:** quem abriu o link provou ter acesso à
caixa, que é o que a confirmação verifica. Sem isso, quem esquecesse a senha antes de
confirmar redefiniria e continuaria trancado para fora.

---

### O registro original do item

O maior item, e o único que não é trabalho de uma tarde.

**Estado atual:**
- ✅ `POST /api/auth/register` já existe e é público (com rate limiting).
- ✅ `AddDefaultTokenProviders()` já está ligado
  (`Prumo.Api/Configuration/DatabaseConfiguration.cs:32`), então os
  **tokens expiráveis** de confirmação de e-mail e de reset de senha vêm prontos
  do Identity. Não precisa inventar nada.
- ❌ **Não existe nenhuma infraestrutura de e-mail.** Zero referência a SMTP,
  SendGrid, MailKit ou `IEmailSender` no projeto inteiro. Precisa escolher o
  provedor e, em produção, um domínio verificado.
- ❌ `SignIn.RequireConfirmedEmail` não está configurado.

**O que precisa:**
- Provedor de e-mail + `IEmailSender`
- Endpoints: confirmar e-mail, reenviar confirmação, esqueci a senha, resetar senha
- Telas: cadastro, "confirme seu e-mail", esqueci a senha, nova senha
- `SignIn.RequireConfirmedEmail = true`

**✅ DECISÃO DE PRODUTO FECHADA em 2026-08-18.** Quem se cadastra sozinho não
pertence a tenant nenhum e cai numa tela de **"aguardando convite para um tenant"**
depois de confirmar o e-mail.

**Como se entra num tenant: o admin adiciona pelo e-mail.** Sem link com token.
O admin digita o e-mail no `[+ Adicionar membro]` da tela de membros (item 5); se a
conta já existe, entra direto; se não, fica um **convite pendente** que se resolve
sozinho quando a pessoa se cadastrar com aquele e-mail. O `GET /tenants/users/lookup`
já existe e é a peça de busca.

Por que não link com token: o link seria superfície de segurança nova (geração,
expiração, revogação, reuso) num sistema cujo diferencial é justamente o isolamento
de tenant ter sido auditado. Adicionando por e-mail, **o e-mail é só notificação, não
mecanismo de autorização** — se o envio falhar, ninguém entra em tenant nenhum por
engano; a pessoa só não é avisada. Isso também deixa o serviço de notificação (abaixo)
ser best-effort, o que é o que permite ele ser assíncrono.

**✅ E-mail sai do monolito.** Decidido em 2026-08-18 que a infraestrutura de e-mail
não vira `IEmailSender` dentro da API: vira um **serviço de notificação separado**,
consumindo fila, com container e deploy próprios no repo de DevOps. Ver a seção
"Serviço de notificação" no roteiro
`docs/superpowers/plans/2026-08-18-roadmap-pos-fase1.md`.

## 9. Limpeza — ✅ FEITO em 2026-08-26

- ✅ **2026-08-11, junto com o rename (item 7):** apagados `BiomePampa.*` da raiz do
  backend — eram só `bin`/`obj` de antes do rename anterior.
- ✅ **Planos soltos** em `docs/superpowers/plans/` foram commitados em 2026-08-17.
- ✅ **Módulos fantasma no catálogo** (`products`, `customers`, `stock`): resolvido por
  tabela rasa — o catálogo inteiro morreu com o item 3B, porque não gateava nada.
- ✅ **`TenantBootstrapSeeder` só rodava na criação do tenant**, então módulo novo nunca
  chegava a tenant existente. Resolvido em 2026-08-26 com
  `SyncResourcesForAllTenantsAsync`, que completa o catálogo de todo tenant a cada boot.
  Só insere `Resource`; nenhuma permissão é tocada, então nada revogado volta.
- ✅ **`console.log` de debug no frontend.** As três linhas que disparavam a cada login
  (uma imprimia o início do JWT, outra o e-mail) foram removidas. Os `console.warn` de
  acesso negado no guard ficaram atrás de `!environment.production` — são diagnóstico
  útil em desenvolvimento, mas em produção contariam a estranhos o que existe e o que
  falta para alcançar. Os `console.error` de falha real ficaram.


---

# Segurança e banco de dados

> Levantado em 2026-08-05, numa segunda varredura, a partir da pergunta "se eu
> subir isso na Azure, alguém consegue derrubar meu banco?". Mesmo critério dos
> itens acima: tudo verificado no código ou no Postgres local, com arquivo e linha.

## 10. O filtro global de tenant é fail-open — e já vazou uma vez

> ✅ **FECHADO.** Fase 1 (2026-08-13) tornou o filtro fail-closed. Fase 2
> (2026-08-25) colheu o resultado: **86 → 18 `IgnoreQueryFilters`**, e as 4
> entidades sem coluna `TenantId` (`WorkLog`, `Payment`, `PaymentPeriod`,
> `JournalLine`) ganharam **query filter por navegação** — sem migration, sem
> desnormalizar a coluna. Os 4 avisos
> `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning` do
> startup foram a 0. Os 18 que sobram são cross-tenant de propósito
> (startup/seeders e emissão de token), cada um comentado, e
> `DataAccessHygieneTests` quebra o build se aparecer um sem justificativa.
> Plano: `docs/superpowers/plans/2026-08-18-phase2-data-access-cleanup.md`.

**Causa:** `Prumo.Infrastructure/Data/ApplicationDbContext.cs:77-80`:

```csharp
modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
    _tenantContext == null
    || !_tenantContext.HasTenant
    || e.TenantId == _tenantContext.TenantId);
```

Sem tenant resolvido, as duas primeiras condições dão `true` e o filtro libera
**todas as linhas de todos os tenants**. Ele não filtra nada.

Isso não é hipótese: é a causa raiz do bug corrigido em `73dc458`, descrito no
topo deste documento. A query lia os recursos de todos os tenants e o
`ToDictionary` estourou com chave duplicada. **O 500 foi o que denunciou — o
vazamento cross-tenant estava junto, silencioso.** Se o dicionário não tivesse
reclamado, ninguém teria notado.

E token sem tenant é cenário suportado e testado: `AuthService.cs:262` só emite o
claim `tenant_id` quando há tenant selecionado, e `AuthServiceTokenTests.cs:215`
afirma explicitamente que o login sem tenant gera token **sem** o claim.

**Por que nada mais vaza hoje:** o isolamento real está em outra camada — cada
action re-checa a associação. `EmployeesController.cs:12` roteia por
`api/tenants/{tenantId:guid}/employees` (o tenantId vem da **URL**, não do token)
e a linha 40 faz `if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();`.
Trocar o GUID na URL pelo de outro tenant dá 403. E `EmployeeService.cs:18` ainda
filtra na mão: `.IgnoreQueryFilters().Where(e => e.TenantId == tenantId)`.

Ou seja: a rede de segurança está desligada justamente no caso em que mais
importaria, e o que segura é a disciplina de escrever a checagem em toda action
nova. O `73dc458` é a prova do que acontece quando alguém esquece.

**O que precisa:** inverter para fail-closed — sem tenant, não retorna nada — com
opt-out explícito e nomeado para os casos legítimos (seeders, migrations, telas
cross-tenant). O `IgnoreQueryFilters()` já é usado assim nos services.

### Correções a este item (varredura de 2026-08-06)

Duas coisas que este item afirmava e que **não se confirmaram** no código:

**(a) Os seeders não são risco de regressão.** O texto acima dizia "médio em
regressão, o `DbInitializer` e os seeders rodam sem tenant". Verificado: o
`DbInitializer`, o `TenantBootstrapSeeder` e o `ChartOfAccountsSeeder` **já usam
`IgnoreQueryFilters()` em todas as leituras**. O que sobra sem o guard são dois
sites de escrita (`context.RolePermissions.RemoveRange` em `DbInitializer.cs:38`
e `context.TenantUserRoles.Add` em `:235`), e escrita não passa por query filter.
Fail-closed não quebra o seeding.

**(b) O raio de impacto é minúsculo e conhecido.** Rastreando toda query em
entidade `ITenantScoped` que **não** chama `IgnoreQueryFilters` — as únicas que
de fato dependem do filtro global — o resultado são **25 sites em 2 arquivos**:

| Arquivo | Entidades | Sites |
|---|---|---|
| `Prumo.Application/Services/ResourcePermissionService.cs` | `Resources`, `ResourcePermissions` | 16 |
| `Prumo.Infrastructure/Authorization/PermissionService.cs` | `RolePermissions`, `PermissionAuditLogs` | 9 |

São exatamente os dois arquivos onde o vazamento do `73dc458` aconteceu. Não é
coincidência: é o único lugar do código que ainda confia no filtro.

**Por que só esses dois.** O backend tem **dois mundos disjuntos** de resolução
de tenant, e o filtro global só alcança um:

- **Mundo URL** — 9 controllers roteados por `api/tenants/{tenantId:guid}/…`. O
  tenant vem da **rota**; o `TenantResolutionMiddleware` preenche o
  `TenantContext` pelo **claim do JWT**, que pode ser outro tenant. Os services
  não podem confiar no `TenantContext`, então usam `IgnoreQueryFilters` e filtram
  na mão. O filtro global é irrelevante aqui.
- **Mundo TenantContext** — 4 controllers sem `tenantId` na rota (`Auth`,
  `Permissions`, `Resources`, `Tenants`). Dependem do `TenantContext`. É aqui
  que o fail-open morde.

**Consequência para o esforço:** fail-closed puro é barato (2 arquivos). Mas
fazer o filtro global virar rede de segurança **de verdade** — que é o objetivo —
exige resolver o `TenantContext` **pela rota**, não pelo claim; só então os ~87
`IgnoreQueryFilters` podem cair. O frontend já é compatível com isso: ele reemite
o token no `selectTenant` **e** manda `X-Tenant-Id` em todo request
(`jwt.interceptor.ts:24`), então claim e rota concordam na prática.

**Achado colateral:** o `Repository<T>` / `UnitOfWork`
(`Prumo.Infrastructure/Repositories/`) — o único consumidor natural
do filtro global, porque não chama `IgnoreQueryFilters` — é **código morto**.
Está registrado no DI (`DependencyInjectionConfiguration.cs:18`), mas nenhum
service injeta `IRepository<Algo>`; as únicas referências estão dentro dos
próprios arquivos dele. O `CLAUDE.md` manda usar `IRepository<T>` em vez de
injetar o `ApplicationDbContext` direto — o código faz o contrário em todo lugar.
Ou apagar, ou passar a usar, ou corrigir o `CLAUDE.md`.

**Escopo `ITenantScoped`:** só 10 entidades implementam a interface (`Account`,
`AccountsPayableCategory`, `AccountsPayableEntry`, `Employee`, `JournalEntry`,
`PermissionAuditLog`, `Resource`, `ResourcePermission`, `RolePermission`,
`TenantUserRole`). **`WorkLog`, `Payment`, `PaymentPeriod`, `JournalLine` e
`TenantGlSettings` não são** — não têm filtro global nenhum e dependem 100% da
checagem manual via entidade pai.

### Auditoria dos 87 `IgnoreQueryFilters` — deu limpa

Vale registrar porque custa caro redescobrir, e porque **muda o argumento deste
item**. A suspeita natural é que `IgnoreQueryFilters()` seja um bypass barato que
desliga a segurança. Auditados os 87 sites, um a um: **não é.**

- **73 sites** filtram por `TenantId` explicitamente na mesma query. O filtro
  automático é substituído por um manual, não removido.
- **4 sites** são intencionalmente cross-tenant e corretos: `TenantService`
  buscando tenant por slug (login, `:29` e `:81`), listando as memberships do
  usuário (`:60`, tela de seleção de tenant), e `DbInitializer.cs:35` limpando
  uma role legada em todos os tenants. Filtrar por tenant aí seria o bug.
- **10 sites** leem `WorkLogs`/`Payments`/`PaymentPeriods` filtrando só por
  `EmployeeId` — o cheiro clássico de IDOR. **Todos têm guard antes**:
  `WorkLogService.cs:17` chama `RequireEmployeeAsync`, que faz
  `Employees.AnyAsync(e => e.TenantId == tenantId && e.Id == employeeId)`
  (`:133-138`); mesmo padrão em `PaymentService.cs:83` e
  `PaymentPeriodService.cs:83`; e o `DeleteAsync` valida o período por
  `p.Employee.TenantId == tenantId` (`:124`) antes do `ExecuteUpdateAsync` da
  linha `:131`. Provado que o `employeeId` pertence ao tenant, filtrar pelo
  `employeeId` **é** filtrar pelo tenant.

**Conclusão: 87 de 87 corretos.** A disciplina segurou.

**Por que o item continua valendo, mesmo assim.** O `IgnoreQueryFilters` torna a
proteção **opt-out por site de chamada**: o filtro global é o único mecanismo que
protege sem ninguém pedir, e cada chamada transfere a responsabilidade para a
linha seguinte — sem compilador, tipo ou teste cobrindo essa transferência. Só
disciplina. E o `73dc458` prova que ela falha: aquele vazamento não veio de quem
*desligou* o filtro, veio de quem **confiou** nele num caminho sem tenant. Das
duas formas de errar, o projeto cometeu justamente a que ninguém vigiava.

O argumento deste item, portanto, **não** é "consertar código desleixado" — o
código não está desleixado. É: *um sistema cuja segurança depende de 87 lembretes
individuais funciona até o dia em que alguém escreve o 88º; o objetivo é reduzir
isso a um lugar só, que erra fechado.*

**Achado menor, de legibilidade:** em `WorkLogs`, `Payments` e `PaymentPeriods` o
`IgnoreQueryFilters()` é **no-op** — essas entidades não são `ITenantScoped`, não
há filtro para ignorar. A chamada não faz nada, mas lê como bypass deliberado de
uma proteção que não existe. Código que mente sobre o que faz; limpar junto.

**Esforço:** pequeno se for só fail-closed. Médio se for a rede de segurança real
(resolver tenant pela rota + remover os `IgnoreQueryFilters`).
**Prioridade: alta.** É o item de segurança de melhor retorno do backlog.

## 11. A aplicação conecta no banco como superuser — ✅ METADE LOCAL FEITA em 2026-08-28

> A parte que roda e se verifica contra o Docker local está pronta: dois roles, a
> migration fora do startup e o sink de log parando de fazer DDL. O que sobra é
> **rede e identidade na Azure** (private endpoint/VNet, Entra ID + Managed
> Identity) e, depois disso, RLS com `FORCE` — tudo listado no fim desta seção.
>
> Design: `docs/superpowers/specs/2026-08-28-postgres-least-privilege-design.md`,
> que tem no fim o registro do que a execução revelou.

**Causa:** `appsettings.json:3` — `Username=postgres;Password=postgres`.
Credencial default, commitada no repo (que é privado — confirmado via `gh`).

Superuser do Postgres **ignora toda checagem de permissão**: um
`DROP SCHEMA public CASCADE` apaga as 25 tabelas, e não há grant, revoke ou
policy que segure. Se um dia entrar RLS, superuser ignora as policies também — e
o *owner* da tabela ignora a menos que se use `ALTER TABLE ... FORCE ROW LEVEL
SECURITY`. Até 2026-08-28 as 25 tabelas eram todas owned by `postgres` (verificado
no banco local); hoje pertencem a `prumo_migrator`. Então **RLS sem separar os
roles antes seria falsa sensação de segurança** — é por isso que ele vem depois.

O que empurrava para o superuser eram duas coisas, e só a primeira estava
levantada: `InitializeDatabaseAsync` rodava as migrations a cada startup, e o sink
PostgreSQL do Serilog criava a tabela `logs` sozinho, também pela conexão da
aplicação.

**O que já está certo:** `appsettings.Production.json:3` sobrescreve o
`DefaultConnection` com o placeholder `CONFIGURE_VIA_AZURE_APP_SETTINGS_OR_KEY_VAULT`.
Em Production não existe fallback silencioso para `postgres/postgres` — a app
quebra no startup se a env var `ConnectionStrings__DefaultConnection` não vier.
Falha segura, comportamento correto, manter assim.

**✅ Feito em 2026-08-28:**

- **Dois roles**, em `db/roles.sql` (idempotente, montado no
  `docker-entrypoint-initdb.d`): `prumo_migrator` é dono de tudo em `public` e é o
  único com DDL; `prumo_app` tem só `SELECT/INSERT/UPDATE/DELETE`. Transferir a
  **posse** das tabelas era metade do trabalho — quem possui a tabela faz DDL nela
  por mais que se revogue grant.
- **`ALTER DEFAULT PRIVILEGES`** para o migrator, senão toda migration com tabela
  nova exigiria reeditar o script, e o esquecimento só apareceria como 500.
- **Migration fora do startup**, atrás de `Database:MigrateOnStartup` (default
  `false` em todo ambiente, `true` só no overlay `Demo`). Quando ligada, migra por
  uma conexão separada com a credencial do migrator — nunca pela da aplicação.
- **Guarda de schema:** o startup compara as migrations do assembly com o banco e
  **recusa servir** se estiver atrasado, dizendo o comando a rodar. É leitura pura
  (`GetPendingMigrationsAsync`), e fica fora do `catch` que engole o resto.
- **O Serilog parou de fazer DDL:** `needAutoCreateTable: false` e a tabela `logs`
  virou a migration `CreateLogsTable`.
- ~~Corrigir o template `PostgresProd`~~ — o `appsettings.ConnectionStrings.json`
  foi apagado no item 12; não existe mais nenhum `SslMode`/`TrustServerCertificate`
  no repo. O `VerifyFull` volta à pauta quando a connection string de produção
  existir de verdade.

**O que falta, e é na Azure:**

- Rede: private endpoint / VNet, sem acesso público. Evitar "permitir acesso de
  qualquer serviço do Azure" — é 0.0.0.0/0 abrangendo outros tenants do Azure.
  Hoje o `terraform/modules/postgres/main.tf` tem exatamente essa regra
  (`allow-azure-services`, 0.0.0.0) e `public_network_access_enabled = true`.
- Auth: Entra ID + Managed Identity em vez de senha — elimina o segredo armazenado.
- O **passo de migration no deploy**: com a migration fora do startup, o
  `deploy.yml` do repo de DevOps precisa rodar `dotnet ef` (ou um bundle) antes de
  o container subir. Enquanto a stack está destruída, nada quebra.
- `SslMode=VerifyFull` na connection string de produção, quando ela existir.
- Só depois de tudo isso, RLS — e com `FORCE`.

**Esforço do que sobra:** médio, e majoritariamente DevOps/Terraform (repo
`SaaSBasePlatform-DevOps`). Exige `az login`, é tarefa a dois.

## 12. Config de banco: um arquivo morto e o `dotnet ef` apontando para outra base

Dois problemas no mesmo lugar.

**(a) O `appsettings.ConnectionStrings.json` nunca é lido.** Não existe nenhum
`AddJsonFile` no projeto inteiro — `Program.cs:11` usa o `CreateBuilder` padrão,
que carrega só `appsettings.json` + overlay de ambiente + env vars + user secrets.
A connection string efetiva vem do `appsettings.json`. O `README.md:68` e o
`CLAUDE.md` descrevem errado ao dizer que as connection strings moram nesse arquivo.

**(b) O design-time factory tem a base chumbada — e é uma base diferente.**

| Quem | Onde | Base |
|---|---|---|
| App em runtime | `appsettings.json:3` | `SaaSBasePlatformDb` |
| `dotnet ef` (migrations) | `ApplicationDbContextFactory.cs:14` | `SaaSBasePlatform` |

`dotnet ef database update` aplica numa base, a app roda em outra. Verificado no
Postgres local: as duas existem, ambas com `__EFMigrationsHistory` e as mesmas 25
tabelas de entidade. A **única** diferença é a tabela `logs`, criada pelo sink do
Serilog (`LoggingConfiguration.cs:50-52`, `tableName: "logs"`) na base da app.
Os schemas só estão iguais porque o `DbInitializer` roda migration no startup —
é coincidência, não design.

**Risco prático:** aplicar uma migration pela CLI, conferir no DBeaver em
`SaaSBasePlatform`, ver tudo certo, e a app estar lendo `SaaSBasePlatformDb`.

**O que precisa:** o factory ler a configuração em vez de hardcodar; e ou passar a
carregar o `appsettings.ConnectionStrings.json` de fato, ou apagá-lo e corrigir
o README e o CLAUDE.md.

**Esforço:** pequeno.

### Complementos (varredura de 2026-08-06)

- O arquivo é morto duas vezes: além de não existir `AddJsonFile` em lugar nenhum,
  ele **não é copiado para o output** — não há entrada para ele no
  `Prumo.Api.csproj` nem no `Dockerfile`. Mesmo que alguém adicionasse
  o `AddJsonFile`, não funcionaria no container sem também mexer no build.
- **Quem diverge é só o factory.** O `DefaultConnection` dentro do
  `appsettings.ConnectionStrings.json` aponta para `SaaSBasePlatformDb` — a
  **mesma** base do `appsettings.json`. As três configurações concordam; o único
  fora do lugar é o `SaaSBasePlatform` chumbado em
  `ApplicationDbContextFactory.cs:14`. Isso reduz o conserto a um ponto só.
- O factory constrói `new ApplicationDbContext(optionsBuilder.Options)` — o
  construtor **sem** `ITenantContext` (`ApplicationDbContext.cs:12`), que deixa
  `_tenantContext` nulo. Sob fail-closed (item 10) esse construtor passa a filtrar
  tudo. Design-time só roda migration, que não faz query — então é inofensivo,
  mas precisa estar escrito no design para ninguém "consertar" errado depois.

## 13. As feature roles não são checadas na API — gating só no frontend

> Item novo, levantado em 2026-08-06 ao mapear o raio do item 10.

> ✅ **FECHADO.** Fase 1 pôs `[TenantModule]` nos 9 controllers de módulo, com
> `TenantCoverageTests` quebrando o build se uma action sob `{tenantId}` ficar
> descoberta. Fase 2 (2026-08-25) removeu as **23 checagens `CanAccess`** que o
> gate já fazia, mantendo as **19 `CanManage`** — cargo administrativo, que o
> `[TenantModule]` nunca checa. Medido na API viva: com cargo rebaixado a
> Member, ler funcionários dá 200 e criar categoria dá 403.
>
> **Continua aberto, como item próprio:** o `[RequireResourceAccess]` do
> `PermissionsController`. Aquele controller não tem `{tenantId}` na rota, então
> tirar o atributo o deixaria sem gate nenhum; converter a rota quebra 7 chamadas
> do `api.service.ts`. Decidir junto com o item 3.

**Causa:** `[RequireResourceAccess]` — o atributo que consulta as
`ResourcePermissions`, ou seja, a máquina inteira de feature roles — é usado em
**um único controller**, o `PermissionsController` (5 actions). Em nenhum módulo
de negócio. Levantamento controller a controller:

| Controller | Gate real |
|---|---|
| `Employees`, `WorkLogs`, `Payments`, `PaymentPeriods` | `CanAccess(role) => role.HasValue` |
| `ChartOfAccounts`, `GeneralLedger`, `AccountsPayable` ×3 | idem (`CanManage` para escrita) |
| `PermissionsController` | `[RequireResourceAccess("permissions", …)]` |
| `ResourcesController` | `[Authorize(Roles = "Administrador")]` |

`role.HasValue` significa **"é membro deste tenant"** — Owner, Admin **ou**
Member indistintamente. É a coluna `TenantUsers.Role`, não as feature roles.

**Consequência:** RH, Financeiro e ContasAPagar são gating **de frontend**. O
menu esconde o módulo de quem não tem a feature role (isso funciona, é o
`resourceCode` nas rotas), mas um `GET /api/tenants/{id}/employees` com o token
de **qualquer** membro do tenant devolve 200 com a folha inteira. A chave tranca
a porta da sala, não o cofre.

**Gravidade, com honestidade:** isto **não é cross-tenant**. Continua sendo
preciso ser membro daquele tenant — trocar o GUID na URL pelo de outro tenant dá
403 (item 10 explica por quê). É "qualquer funcionário da empresa lê o RH da
empresa pela API", não "outra empresa lê seus dados". Mas é exatamente o que as
feature roles existem para impedir, então hoje elas não entregam o que prometem.

**O que precisa:** decidir o resourceCode de cada módulo e pôr
`[RequireResourceAccess("<code>", <nível>)]` nos 9 controllers, com `Read` nas
leituras e `Full`/`Write` nas escritas. Cuidado: o `TenantBootstrapSeeder` só
roda na criação do tenant (ver item 9), então tenant antigo pode não ter o
`Resource` correspondente — e aí o atributo negaria acesso a todo mundo.

**Esforço:** médio. Toca 9 controllers e depende do catálogo de recursos estar
completo por tenant.
**Prioridade: alta**, e maior que a do item 10 em impacto prático — 9 controllers
com dado de negócio, contra 2 arquivos de tela administrativa.

---

## 14. Achados da sessão de design de 2026-08-11

> Levantados ao desenhar o pacote 10+12+13. Os dois primeiros são **bugs ativos**,
> não riscos teóricos. O design completo está em
> `docs/superpowers/specs/2026-08-11-backend-tenant-isolation-design.md`.

**(a) O seeder ressuscita grants revogados a cada startup.** ⚠️ bug ativo.
`DbInitializer.EnsureTenantBootstrapAsync` percorre *todos* os tenants no boot e
reaplica o `TenantBootstrapSeeder` inteiro — inclusive `ResourcePermissions`
(`Administrador` ganha `Full` em tudo; cada role de módulo ganha `Full` no módulo
dela). Um admin revoga o acesso do RH a `employees`, alguém reinicia a API, e o
grant volta como `Full`. **Revogação não sobrevive a um restart.** O seeder checa
`exists` antes de inserir, então não distingue "nunca concedido" de "revogado" —
ambos são ausência de linha. *Tratado no design (seção 3): a função é apagada.*

**(b) Senha do admin chumbada e logada em texto puro.** ⚠️ bug ativo.
`admin@SBP.com` / `Admin@123` no `DbInitializer`, e a senha sai em log nível Info.
O Serilog tem sink para tabela — em produção isso deposita a credencial do admin
master no armazenamento de log, não só no repo. *Tratado no design (seção 3).*

**(c) `POST /api/tenants` sem checagem de role.** Só `[Authorize]`. Qualquer
usuário autenticado cria tenant à vontade. No modelo em que tenant é vendido, é
furo de segurança e de negócio. *Tratado no design (seção 4).*

**(d) O middleware aceita o claim `tenant_id` sem reconferir associação.** Só o
caminho do header `X-Tenant-Id` chama `IsMemberAsync`. Quem for removido de um
tenant mantém o `TenantContext` daquele tenant até o token expirar — até 8h de
defasagem na revogação. *Tratado no design (seção 1, passo 4).*

**(e) Worktrees obsoletos** em `.worktrees/` ainda com a estrutura pré-rename
(`SaaS_BasePlatform.*`). Não afetam a `main`. Higiene, fora do design.

**(f) `NU1903` de severidade alta** em `System.Security.Cryptography.Xml` 10.0.7 e
`Microsoft.OpenApi` 2.0.0, mais o bundle inicial do Angular estourando o budget em
419 kB. Higiene, fora do design.

---

## 15. `ArgumentException` não é mapeada — validação de negócio devolve 500

> Achado em 2026-08-25, durante a verificação da fase 2. **Não é regressão** — o
> `ExceptionHandlingMiddleware` está intocado desde antes.

`Prumo.Api/Middleware/ExceptionHandlingMiddleware.cs` mapeia `ValidationException`,
`KeyNotFoundException`, `UnauthorizedAccessException` e `InvalidOperationException`.
**`ArgumentException` não está na lista** e cai no 500 padrão.

Vários services validam com `ArgumentException`: `AccountsPayableService`
("Category name is required.", "Description is required."), `EmployeeService`
("CPF is required.", "HourlyRate must be greater than zero."), `AccountService`
("Account code is required."), `JournalService`, `PaymentPeriodService`
("StartDate must be before EndDate.").

**Reproduzido:** `POST /api/tenants/{id}/accounts-payable/categories` com
`{"name":""}` → **500**, e o log registra `Unhandled exception: Category name is
required.` Deveria ser **400**.

O impacto é maior do que parece: o frontend trata 400 mostrando a mensagem ao
usuário, e 500 como falha genérica. Hoje toda validação de negócio dessas aparece
como erro do sistema.

**Correção:** um `case ArgumentException:` devolvendo 400, junto do
`InvalidOperationException`. É de uma linha; o que falta é decidir se a mensagem
da exceção vai para o corpo da resposta (as atuais são seguras, mas vira contrato).

---

## Onde o projeto está — 2026-09-10

> **Leia isto primeiro ao retomar.** Substitui a "ordem sugerida" antiga, que ficou
> inteira para trás.

**Tudo o que é aplicação está feito** — os itens 8 e 11 saíram em 2026-08-28. O que sobra
é infraestrutura, num pass único, ordenado para não pagar `terraform apply` (e o tempo do
Nickolas) mais de uma vez:

| Falta | Por quê ainda não |
|---|---|
| **O piloto na AWS** | Tudo o que é aplicação está feito. Sobra infraestrutura, e ela **mudou de nuvem**: ver abaixo. |

**A infra saiu da Azure e virou um piloto na AWS** (decidido em 2026-09-07/08). Não é
mais "ambiente de teste descartável": é a versão que vai à frente de cliente. Uma
instância Lightsail de 2 GB em `sa-east-1` rodando `docker compose` — Caddy, o nginx do
Angular, a API e o Postgres — com TLS e snapshot diário, por ~US$ 14/mês. O desenho está
em `docs/superpowers/specs/2026-09-07-aws-dev-environment-design.md` e o plano, com 13
tasks, em `docs/superpowers/plans/2026-09-07-aws-dev-environment.md`.

Estado em 2026-09-14: **nada aplicado, nenhum recurso na AWS, custo US$ 0,00.** A Task 9
(tirar os placeholders de Azure do overlay de produção) saiu em 2026-09-09. Em 2026-09-10
todo o HCL que não exige conta foi escrito, validado offline contra o provider real e
commitado no repo devops: tasks 1 (parcial), 2, 4 e 6. **Em 2026-09-14 o resto do código
saiu** — tasks 5, 7, 8 e 10 escritas, mais dois scripts (`scripts/aws/bootstrap-dev.sh` e
`gen-secrets.sh`) que automatizam os passos de CLI das tasks 1 a 3. Tudo em `main` nos três
repos. O "Registro de execução" no fim do plano diz exatamente o que ficou de fora e lista
os três erros do plano que só apareceram ao escrever o código.

**O que sobrou não é código: é conta.** O `apply`, o `.env` na máquina, a chave do
`prumo-dev-box`, o par SSH, os secrets do GitHub e a verificação da Task 11.

**O bloqueio é a Task 0, e é trabalho humano no console:** console + MFA no usuário IAM
`nickolas`, rotação de uma chave de acesso de 448 dias e o budget alarm. A conta **não tem
crédito nenhum** e o free tier expirou — esse alarme é o único aviso entre um erro e uma
fatura. Enquanto `aws sts get-caller-identity` não devolver um ARN que não termine em
`:root`, nada que gasta dinheiro começa.

A árvore `azurerm` do repo devops **não foi tocada** e o `rg-saasbase-tfstate` continua de
pé na Azure, custando centavos. Decidir se ele morre é assunto para depois de o piloto
subir, não antes.

**No código da aplicação, porém, a Azure acabou em 2026-09-16.** O provider portável do
item 14 tinha duas metades; a de Azure — `ServiceBusNotificationPublisher`,
`ServiceBusNotificationWorker`, `AcsEmailSender` e os três pacotes — foi removida, e não
sobra pacote Azure no grafo nem por transitividade. Fila é SQS, e-mail é SES ou arquivo.
`Notifications:Provider` **continua existindo**, agora com um único valor válido: é o que
faz uma config herdada com `ServiceBus` derrubar o startup em vez de subir um worker de
SQS calado.

Além desses, seguem fora do escopo **por decisão**, não por esquecimento:

- **Pass de infra** (`2026-08-11-infra-rename-pass.md`) — renomear os 3 repos e corrigir
  as federated credentials no Entra. Exige `az login`, é tarefa a dois.
- **Preparar para publicar** — sobrou **só decidir o que fazer com o histórico**. O
  `NU1903` e o README saíram em 2026-09-16, e a varredura de segredo está feita e
  reconfirmada (ver o registro da auditoria acima).
- **Fora do MVP desde sempre:** dashboards como produto, e contas a pagar / plano de
  contas / razão geral.

### O que foi feito, em ordem

| # | Item | Quando |
|---|---|---|
| 1 | Fase 1 de isolamento de tenant (itens 10 + 12 + 13) | 2026-08-13, verificada em 08-18 |
| 2 | Rename para Prumo (item 7) | 2026-08-11 |
| 3 | Quick wins — itens 1, 2 e 6 | 2026-08-18 |
| 4 | Fase 2 — limpeza do acesso a dados | 2026-08-25 |
| 5 | Itens 4 e 15 — roles do membro e mapeamento de erro | 2026-08-25 |
| 6 | Item 5 — tela única de membros | 2026-08-26 |
| 7 | Item 3A — role por tenant e grade de níveis | 2026-08-26 |
| 8 | Item 3B — aposentadoria do `RolePermission` e auditoria de verdade | 2026-08-26 |
| 9 | Recursos próprios para as abas do dashboard + sync de catálogo | 2026-08-26 |
| 10 | Item 9 — limpeza, incluindo o logging de debug | 2026-08-26 |
| 11 | Item 11, metade local — roles do Postgres e migration fora do startup | 2026-08-28 |
| 12 | Serviço de notificação (`Prumo.Notifications`) — fila, worker e envio | 2026-08-28 |
| 13 | Item 8 — cadastro, confirmação, senha esquecida e convites | 2026-08-28 |
| 14 | Fila e e-mail portáveis — provider escolhido por configuração, ElasticMQ no lugar do emulador do Service Bus | 2026-09-03 |
| 15 | Identidade Prumo no frontend — paleta, tema M2 próprio e o selo, no lugar do deeppurple-amber | 2026-09-10 |
| 16 | Auditoria adversarial de isolamento de tenant, os 4 achados fechados nos três repos (PRs #24, Angular #23, DevOps #7) e higiene de branches e segredos | 2026-09-15 |
| 17 | Limpeza: chave JWT fora do repo, os dois `NU1903`, os warnings do compilador e a metade Azure do provider de notificação | 2026-09-16 |

### Registro da auditoria de tenant (2026-09-15)

A pergunta foi: "usuário legítimo do tenant A consegue ler, alterar ou apagar dado do
tenant B, ou ganhar autorização que não deveria?". Leitura completa do backend (todos os
controllers, services, os 14 `IgnoreQueryFilters`, o filtro global, o `[TenantModule]`,
o middleware e a emissão de token). **Nenhum caminho devolve dado de negócio de outro
tenant.** Os quatro achados viviam nos controllers isentos do `[TenantModule]`
(`TenantsController`, `AuthController`) e estão fechados na PR #24, cada um com teste que
falhou antes da correção:

1. Admin fabricava um segundo Owner (`AddMember`, `InviteMember` e o convite aceito no
   cadastro aceitavam `Role=Owner`), que ninguém removia nem rebaixava. Owner só nasce
   com o tenant.
2. Remover membro deixava as `TenantUserRoles` órfãs: readmitido, voltava com os módulos
   antigos. Saem junto.
3. `GET /tenants/{id}/assignable-roles` não provava associação.
4. Os dois `users/lookup` resolviam qualquer e-mail para id e nome, para qualquer
   autenticado, sem rate limit. Só o master.

Foram junto: `RequireResourceAccessAttribute` apagado (morto e sem prova de associação);
rate limit `public` passou de janela única do site para por IP; `ForwardedHeaders` de
dois saltos (Caddy, nginx) para o IP real; `UseHttpsRedirection` fora. No DevOps, dois
defeitos que derrubariam o piloto no primeiro deploy: Caddy apontava para `web:80` (o
nginx escuta em 8080) e o compose não definia `API_HOST`, então a API recusava o `Host`
com 400. No Angular, o dropdown de cargo oferecia Owner e havia código morto.

**Verrugas conhecidas, deixadas de propósito (sem impacto de segurança):**

- `WorkLogsController` ignora o `employeeId` da rota em `GetById`/`Update`/`Delete`.
- Marcar título pago escreve no Razão (`GlPostingService`) sem permissão de Razão.
- `Employee.ApplicationUserId` aceita qualquer GUID de usuário, sem validar tenant.
- O header `X-Tenant-Id` do interceptor do Angular não tem efeito: a API o ignora quando
  o token tem claim, e o SPA nunca fica sem claim.
- Token de usuário desativado vale até expirar (até 8h). `[TenantModule]` reprova quem
  saiu do tenant, mas `IsActive` ninguém reconfere por request.
- `ResourcePermissionService.GetEffectiveRoleIdsAsync` resolve roles globais por nome
  sem filtrar `TenantId`; só é seguro porque nomes canônicos são reservados.

**O que só se prova contra a API viva** (não rodou: Docker parado no dia): `my-permissions`
com token de quem acabou de ser removido; header `X-Tenant-Id` sem claim para tenant de
que não se é membro; `caddy validate` no Caddyfile novo; `alg: none` no JWT; 200 chamadas
ao `users/lookup` num minuto para confirmar o 429.

**Varredura de segredos (working tree e os 248 + 128 + 60 commits, por regex, sem
gitleaks):** nenhuma credencial real. O que há no histórico do API é senha de dev e demo
(`postgres/postgres`, SA do SQL Server local, `.env.example`) e a `Admin@123` do
`DbInitializer` antigo (corrigido em `7ed7048`). Dois pontos ficaram abertos, e hoje
sobra um:

- ~~`Prumo.Api/appsettings.Demo.json` traz a chave JWT em texto no repo.~~ **Fechado em
  2026-09-16.** Nenhum overlay commita chave: dev lê de user secrets, o resto da env var
  `Jwt__Key`, e a ausência derruba o startup como a connection string de Production.
  Confirmado no caminho que o piloto roda `ASPNETCORE_ENVIRONMENT=Production`, **não
  `Demo`** — o compose injeta `Jwt__Key` do `.env` do `gen-secrets.sh`. Aquela chave nunca
  assinaria token no piloto; era higiene, não furo aberto.
- **Segue aberto:** antes de tornar qualquer repo público, o histórico do API precisa de
  reescrita ou de aceitação consciente. Os três repos são privados e sem secret scanning
  do GitHub. As quatro chaves JWT de demo que o histórico guarda estão todas fora da
  árvore e nunca assinaram nada fora de máquina local.

**Varredura refeita de forma independente em 2026-09-16** (253 + 128 + 60 commits), e
bate: nenhum `AKIA`/`ASIA`, nenhuma chave privada, nenhum token de provedor, e nenhum
`.tfstate`, `.env` ou `.pem` jamais commitado nos três repos — só o `.env.example` da era
BiomePampa, que carregava a senha da SA do SQL Server local e foi apagado em `368b564`.

Higiene feita no mesmo dia: só `main` sobrou, local e remoto, nos três repos; `origin/HEAD`
do API voltou a apontar para `main`.

### Quatro coisas que valem lembrar antes de escrever código novo

1. **Enum atravessa o wire como string.** `TenantRole` chega `"Owner"`, `PermissionLevel`
   chega `"Read"`. Tratar como número dá sempre falso e some com controles da tela sem
   erro nenhum. Já mordeu **quatro** vezes. Normalize na fronteira: `toTenantRole` e
   `toPermissionLevel` vivem em `core/models`.
2. **Rotina de startup que escreve permissão é sempre suspeita.** Duas vezes um seeder
   ressuscitou grant revogado (4203a15, e o backfill de dashboard em 08-26). Se precisa
   rodar uma vez, é migration.
3. **O master admin não enxerga gating.** Ele é bypass em duas camadas (role global
   `Administrador` e Owner/Admin do tenant). Testar autorização com a conta `admin@SBP.com`
   não prova nada — use um Member com role limitada.
4. **Falha de sink do Serilog é silenciosa.** A exceção morre dentro do batch periódico.
   O sink do Postgres passou meses sem gravar uma linha por causa de um `DateTimeOffset`
   com fuso local (item 11, 2026-08-28), e nada no console denunciava. O `SelfLog` agora
   está ligado; se ele falar, alguma escrita de log está se perdendo.

## Fora do escopo do MVP

- Dashboards: OK como estão.
- Contas a pagar, plano de contas, razão geral: testados há um tempo, presumidos
  funcionando, não são preocupação agora.

---

## Registro do item 3B (2026-08-26)

**O sistema `RolePermission` foi aposentado.** Saíram: `PermissionsController`,
`PermissionService`/`IPermissionService`, as entidades `RolePermission`, `Permission` e
`PermissionAuditLog`, o catálogo de ~40 strings em `Permissions.cs`, as policies por
permissão e o `PermissionAuthorizationHandler`. Nada disso gateava coisa alguma: zero
`[Authorize(Policy=…)]` nos controllers e `hasPermission()` nunca chamado no frontend.

**Auditoria, que era o ponto cego:** conceder e revogar nível de recurso — o que de fato
dá acesso — **não deixava rastro nenhum**. A linha guardava quem criou, mas revogar
apagava a linha e o histórico junto. Agora há `ResourcePermissionAuditLog`
(role × recurso, de qual nível para qual, por quem), e o support-access do master admin
ganhou o `SupportAccessLog` próprio, executando a decisão de 2026-08-18 — antes ele
usava o `PermissionAuditLog` com sentinelas.

**Dados descartados:** 31 permissões e 217 grants, todos seed gerado por código, e uma
tabela de auditoria com **zero** linhas. Nada escrito por humano se perdeu.

**O risco era a emissão do token** — `AuthService` chamava o serviço aposentado enquanto
montava os claims, e é esse caminho que quase quebrou o login na fase 2. O claim
`permission` saiu junto; o teste que afirmava a presença dele agora **afirma a ausência**,
para que ressuscitar o sistema morto falhe alto.

**O defeito que a verificação pegou, e que era meu:** o backfill que preservava os
acessos de dashboard rodava **a cada boot** e por isso **desfazia revogações** — tirar
`Dashboard.HR` de uma role que ainda tivesse `HR.Employees` durava até o restart. Mesma
forma do bug 4203a15. Virou migration, que roda uma vez. Provado: revogação sobrevive ao
restart agora, e não sobrevivia antes.

**Também aqui:** `ResourcePermissions` **não tem coluna `Id` nem `IsActive`** — a chave é
composta `(TenantId, RoleId, ResourceId)`. SQL cru contra essa tabela precisa saber disso.

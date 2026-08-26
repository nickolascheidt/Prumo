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

## 3. Não existe tela (nem API) para criar role

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
- Frontend: página nova. Decisão do Nickolas: **página separada**, não embutida
  na tela de Permissões por Role.

**Esforço:** médio. **Já não bloqueia o item 2** — aquele foi fechado em 2026-08-18.

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

## 8. Cadastro de usuário + confirmação de e-mail + esqueci minha senha

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

## 9. Limpeza

- ✅ **Feito em 2026-08-11, junto com o rename (item 7):** apagados
  `BiomePampa.Api/`, `BiomePampa.Application/`, `BiomePampa.Domain/`,
  `BiomePampa.Infrastructure/`, `BiomePampa.Tests/` da raiz do backend — eram só
  `bin`/`obj` de antes do rename anterior, nenhum arquivo rastreado pelo git.
- Existem planos não commitados em `docs/superpowers/plans/`: `hr-module-backend`,
  `hr-module-frontend`, `user-management`.
- O catálogo de permissões (`Permissions.cs`) ainda lista módulos que não têm
  controller: `products`, `customers`, `stock`.
- O `TenantBootstrapSeeder` só roda na **criação** do tenant — tenant antigo não
  recebe recurso novo. Vai doer quando um módulo novo for adicionado.

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

## 11. A aplicação conecta no banco como superuser

**Causa:** `appsettings.json:3` — `Username=postgres;Password=postgres`.
Credencial default, commitada no repo (que é privado — confirmado via `gh`).

Superuser do Postgres **ignora toda checagem de permissão**: um
`DROP SCHEMA public CASCADE` apaga as 25 tabelas, e não há grant, revoke ou
policy que segure. Se um dia entrar RLS, superuser ignora as policies também — e
o *owner* da tabela ignora a menos que se use `ALTER TABLE ... FORCE ROW LEVEL
SECURITY`. Hoje as 25 tabelas são todas owned by `postgres` (verificado no banco
local). Então **RLS sem separar os roles antes seria falsa sensação de segurança.**

O que empurra para o superuser é `Program.cs:30`: `InitializeDatabaseAsync` roda
as migrations a cada startup, e migration precisa de DDL.

**O que já está certo:** `appsettings.Production.json:3` sobrescreve o
`DefaultConnection` com o placeholder `CONFIGURE_VIA_AZURE_APP_SETTINGS_OR_KEY_VAULT`.
Em Production não existe fallback silencioso para `postgres/postgres` — a app
quebra no startup se a env var `ConnectionStrings__DefaultConnection` não vier.
Falha segura, comportamento correto, manter assim.

**O que precisa, antes de subir para a Azure:**

- Dois roles no Postgres: um **migrator** com DDL, usado só no passo de migration,
  e um **runtime** só com DML nas tabelas da app. A API conecta como runtime.
- Tirar a migration do startup (ou pôr atrás de flag), para a app rodando nunca
  precisar de DDL.
- Rede: private endpoint / VNet, sem acesso público. Evitar "permitir acesso de
  qualquer serviço do Azure" — é 0.0.0.0/0 abrangendo outros tenants do Azure.
- Auth: Entra ID + Managed Identity em vez de senha — elimina o segredo armazenado.
- Corrigir o template `PostgresProd` em `appsettings.ConnectionStrings.json:5`:
  tem `SslMode=Require;Trust Server Certificate=true`, e o `Trust Server Certificate`
  **desliga a validação do certificado** (abre espaço para MITM). Em produção,
  `SslMode=VerifyFull`.
- Só depois de tudo isso, RLS — e com `FORCE`.

**Esforço:** médio, e majoritariamente DevOps/Terraform (repo `SaaSBasePlatform-DevOps`).

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

## Ordem sugerida

> **Atualizado em 2026-08-06.** Decisão do Nickolas: atacar segurança primeiro, e
> tratar **10 + 12 + 13 como um design único** — "o que garante isolamento e
> permissão no backend" — em vez de três consertos soltos. O alvo escolhido é
> *rede de segurança de verdade*: um endpoint novo escrito sem cuidado não deve
> conseguir vazar dado, mesmo que o dev esqueça a checagem. Isso implica resolver
> o `TenantContext` pela rota, tornar o filtro global fail-closed e autoritativo,
> derrubar os ~87 `IgnoreQueryFilters` e levar o `[RequireResourceAccess]` aos 9
> controllers de módulo.

1. ~~**Pacote de segurança do backend — itens 10 + 13 + 12**~~ — ✅ **fase 1 feita,
   mergeada e verificada de ponta a ponta** (2026-08-13, verificação fechada em
   2026-08-18). Spec em
   `docs/superpowers/specs/2026-08-11-backend-tenant-isolation-design.md`; o registro
   do que foi entregue e do que foi provado está no fim de
   `docs/superpowers/plans/2026-08-11-tenant-isolation-phase1.md`. **A fase 2, de
   limpeza (~87 `IgnoreQueryFilters` + checagens redundantes), ainda não tem plano
   escrito — é o próximo trabalho grande.**
2. ~~**Rename**~~ — ✅ **feito em 2026-08-11.** O sistema é **Prumo**. Ver item 7.
3. ~~**Quick wins** — itens 1 e 6~~ — ✅ **feitos em 2026-08-18**, junto com o item 2.
4. **Pacote RBAC** — sobrou o item **3**. Os itens 4 e 15 saíram em 2026-08-25
   (PR #14 nos dois repos, mergeado), e o **item 5 saiu em 2026-08-26** — a tela
   única de membros está de pé. O item 2 saiu na frente em 2026-08-18 porque não
   estava bloqueado como este documento dizia.
   *(desbloqueado em 2026-08-06: a explicação Owner/Admin/Member vs feature roles
   foi dada e entendida — ver a tabela no item 5.)*

   **O item 3 é o próximo**, e ele obriga a revisitar duas coisas já registradas:
   `Permissions.Roles.All` é hoje uma lista `readonly` travada por
   `CanonicalRolesTests` — com role virando dado criado pelo usuário, ela vira
   consulta ao Identity e aqueles testes mudam de sentido; e é o momento de reabrir
   a decisão "seeder vs migration", adiada exatamente para cá. Junto dele vai o
   `[RequireResourceAccess]` do `PermissionsController`, que ficou fora da fase 2 de
   propósito (aquele controller não tem `{tenantId}` na rota).
5. **Cadastro + e-mail** (item 8) — maior, e com decisão de produto pendente.
6. **Item 11** — portão antes de subir para produção. É trabalho de DevOps, corre
   em paralelo com o resto.

## Fora do escopo do MVP

- Dashboards: OK como estão.
- Contas a pagar, plano de contas, razão geral: testados há um tempo, presumidos
  funcionando, não são preocupação agora.

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

## 1. Botão "Abrir painel admin" não faz nada

**Causa:** `dashboard-overview.component.html:60` aponta para `routerLink="/admin"`,
e essa rota não existe no `app.routes.ts` — só existem `/admin/permissions`,
`/admin/users-roles`, `/admin/tenant` e `/admin/members`. Existe um
`modules/admin/admin-panel.component.ts` órfão, nunca registrado em rota nenhuma.

**Esforço:** 1 linha (apontar para uma rota que existe) ou registrar o componente órfão.
**Prioridade:** baixa — o Nickolas disse que por ora não precisa.

## 2. Role "Usuario" fantasma na tela de Permissões por Role

**Causa:** `permissions-management.component.ts:49` tem a lista chumbada no front:

```ts
readonly availableRoles: string[] = ['Administrador', 'Funcionario', 'Cliente', 'Usuario'];
```

As roles reais do backend (`Permissions.Roles`, em
`SaaS_BasePlatform.Domain/Authorization/Permissions.cs:85`) são:
`Administrador`, `Funcionario`, `Cliente`, `RH`, `Financeiro`, `ContasAPagar`.

Ou seja: além de `Usuario` não existir, **faltam três roles** na tela (RH,
Financeiro, ContasAPagar). Apagar `Usuario` da lista resolve o sintoma e mantém
o problema — o certo é buscar do backend.

**Bloqueio:** não existe endpoint de listar roles (ver item 3).
**Esforço:** pequeno, depois do item 3.

## 3. Não existe tela (nem API) para criar role

**Causa:** confirmado — não há `RolesController`. Nenhum endpoint de
listar/criar/excluir role. O único parecido é
`GET /api/tenants/{tenantId}/assignable-roles`, que devolve a lista fixa de
`Permissions.Roles.AssignableFeatureRoles`.

**O que precisa:**
- Backend: `RolesController` com listar / criar / excluir.
- Definir o que acontece com as `ResourcePermissions` de uma role recém-criada
  (nasce sem nenhuma? herda de um template?).
- Frontend: página nova. Decisão do Nickolas: **página separada**, não embutida
  na tela de Permissões por Role.

**Esforço:** médio. É o item que destrava o item 2.

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
(`SaaS_BasePlatform.Application/DTOs/Tenants/TenantDtos.cs:23`) para o
`GET /tenants/{id}/members` já devolver tudo numa tirada só (evita N+1), e
**decidir** se a role global entra na contagem.

**Esforço:** pequeno/médio.

## 5. Fundir as telas de membros — CONVERSAR ANTES

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
**➡️ AÇÃO: explicar isso melhor amanhã antes de implementar.**

## 6. Botão "Configurações" no menu do usuário

**Causa:** `layout.component.html:25` — `<button mat-menu-item>` sem nenhum
`(click)`. Decisão: **ocultar**, não precisa fazer nada no MVP.

**Esforço:** trivial, 4 linhas.

## 7. Renomear o sistema — VEREDITO: fácil, ~meio dia, banco não é tocado

Os números assustam mas enganam: **368 arquivos / ~18.500 ocorrências** no
backend, sendo que praticamente todas são `namespace SaaS_BasePlatform.X` e
`using SaaS_BasePlatform.X`. É find/replace mecânico + renomear 5 diretórios +
o `.slnx` + o `Dockerfile`.

**O ponto que foi verificado com cuidado:** as **588 ocorrências dentro das
migrations** são nomes de tipo CLR no model snapshot
(`modelBuilder.Entity("SaaS_BasePlatform.Domain.Entities.ApplicationUser")`).
Trocam no mesmo find/replace, o snapshot continua consistente, e **nenhum nome
de tabela ou coluna muda** → não precisa de migration nova, não precisa mexer no
Postgres.

**Frontend:** 9 arquivos, 35 ocorrências. Trivial.

**Efeitos colaterais, ambos benignos:**
- JWT `Issuer`/`Audience` carregam o nome (`SaaS_BasePlatformApi`, em
  `appsettings*.json`). Trocar invalida os tokens em circulação — todo mundo
  reloga uma vez.
- As chaves do `localStorage` (`saas_baseplatform_token`, `_user`,
  `_permissions`, `_resource_permissions`, `_tenant_id`) idem, se quiser trocar.

**Precedente:** ainda existem `BiomePampa.Api/`, `BiomePampa.Domain/`,
`BiomePampa.Application/`, `BiomePampa.Infrastructure/`, `BiomePampa.Tests/` na
raiz — só lixo de `bin`/`obj`. **O projeto já foi renomeado uma vez e deu certo.**

**⚠️ FALTA O NOME NOVO.** É o único bloqueio deste item.

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
  (`SaaS_BasePlatform.Api/Configuration/DatabaseConfiguration.cs:32`), então os
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

**Decisão de produto (encaminhada):** quem se cadastra sozinho não pertence a
tenant nenhum — e usuário sem tenant vê menu vazio. A inclinação do Nickolas é
**abrir uma tela de "aguardando convite para um tenant"** depois do cadastro
confirmado. Falta fechar: quem convida? é por link, ou o admin do tenant adiciona
pelo e-mail? (o `GET /tenants/users/lookup` por e-mail já existe e ajudaria aqui).

## 9. Limpeza

- Apagar `BiomePampa.Api/`, `BiomePampa.Application/`, `BiomePampa.Domain/`,
  `BiomePampa.Infrastructure/`, `BiomePampa.Tests/` da raiz do backend — são só
  `bin`/`obj` de antes do rename anterior.
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

**Causa:** `SaaS_BasePlatform.Infrastructure/Data/ApplicationDbContext.cs:77-80`:

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
| `SaaS_BasePlatform.Application/Services/ResourcePermissionService.cs` | `Resources`, `ResourcePermissions` | 16 |
| `SaaS_BasePlatform.Infrastructure/Authorization/PermissionService.cs` | `RolePermissions`, `PermissionAuditLogs` | 9 |

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
(`SaaS_BasePlatform.Infrastructure/Repositories/`) — o único consumidor natural
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
  `SaaS_BasePlatform.Api.csproj` nem no `Dockerfile`. Mesmo que alguém adicionasse
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

## Ordem sugerida

> **Atualizado em 2026-08-06.** Decisão do Nickolas: atacar segurança primeiro, e
> tratar **10 + 12 + 13 como um design único** — "o que garante isolamento e
> permissão no backend" — em vez de três consertos soltos. O alvo escolhido é
> *rede de segurança de verdade*: um endpoint novo escrito sem cuidado não deve
> conseguir vazar dado, mesmo que o dev esqueça a checagem. Isso implica resolver
> o `TenantContext` pela rota, tornar o filtro global fail-closed e autoritativo,
> derrubar os ~87 `IgnoreQueryFilters` e levar o `[RequireResourceAccess]` aos 9
> controllers de módulo.

1. **Pacote de segurança do backend — itens 10 + 13 + 12** — um design só.
   *(em brainstorming; spec ainda não escrito)*
2. **Rename** — quanto mais código escrever, mais caro fica. *(bloqueado: falta o
   nome; o Nickolas quer uma sessão de brainstorm de nome)*
3. **Pacote RBAC** — itens 2, 3, 4 e 5 se tocam, fazer juntos.
   *(desbloqueado em 2026-08-06: a explicação Owner/Admin/Member vs feature roles
   foi dada e entendida — ver a tabela no item 5)*
4. **Quick wins** — itens 1 e 6, entram em qualquer momento.
5. **Cadastro + e-mail** (item 8) — maior, e com decisão de produto pendente.
6. **Item 11** — portão antes de subir para produção. É trabalho de DevOps, corre
   em paralelo com o resto.

## Fora do escopo do MVP

- Dashboards: OK como estão.
- Contas a pagar, plano de contas, razão geral: testados há um tempo, presumidos
  funcionando, não são preocupação agora.

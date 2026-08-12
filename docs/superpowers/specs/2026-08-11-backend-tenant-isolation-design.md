# Design: isolamento e permissão no backend (itens 10 + 12 + 13)

> Escrito em 2026-08-11. Cobre os itens **10**, **12** e **13** do
> `docs/MVP-BACKLOG.md` como um design único, mais seis achados novos levantados
> durante a sessão de design que não estavam no backlog.

## Objetivo

Hoje o isolamento entre tenants funciona por disciplina: cada action reescreve a
checagem, e o filtro global do EF é fail-open — sem tenant resolvido ele libera
todas as linhas de todos os tenants. O commit `73dc458` provou que a disciplina
falha, e falhou justamente por quem *confiou* no filtro, não por quem o desligou.

O alvo é **rede de segurança de verdade**: um endpoint novo escrito sem cuidado
não deve conseguir vazar dado nem autorizar quem não devia, mesmo que o
desenvolvedor esqueça toda a checagem.

## O achado que amarra os três itens

O `[RequireResourceAccess]` chama:

```csharp
UserHasAccessAsync(userId, resourceCode, minimumLevel)
```

**sem `tenantId`**. Ele resolve as permissões contra o `TenantContext` ambiente,
que o `TenantResolutionMiddleware` preenche a partir do **claim do JWT**. Os 9
controllers de módulo, porém, leem os dados pelo `tenantId` da **rota**.

Consequência: aplicar o atributo nesses controllers sem antes corrigir a origem
do `TenantContext` produz uma autorização que consulta um tenant e uma leitura
que devolve outro. O item 13 não é "depois" do item 10 — ele é **incorreto** sem
ele. É por isso que os três viraram um design só.

## Decisões tomadas

| # | Decisão | Escolha |
|---|---|---|
| 1 | Como o sistema reage ao esquecimento de um dev | **Quebrar no startup/testes** — teste de arquitetura, falha alta e cedo |
| 2 | O que fazer com as proteções existentes | **Aditivo agora, limpeza depois** — nada é removido na fase 1 |
| 3 | Como garantir o catálogo de recursos por tenant | **Migration de backfill** + bootstrap só na criação do tenant |
| 4 | Rota e claim discordando | **Rejeitar com 403** |
| 5 | Onde mora o enforcement | **Um atributo por controller**, que declara *e* aplica |
| 6 | Acesso de suporte do master admin | **Por associação**, com endpoint de auto-inclusão auditado |

A decisão 1 tem um limite que precisa estar dito: o teste de arquitetura garante
que o controller **declarou**, não que a declaração está certa. Ele pega o
esquecimento, não o engano. Por isso a asserção extra descrita na seção de testes.

## Achados novos desta sessão

Nenhum destes estava no backlog. Os dois primeiros são bugs ativos.

1. **O seeder ressuscita grants revogados a cada startup.** O
   `DbInitializer.EnsureTenantBootstrapAsync` percorre *todos* os tenants no boot
   e reaplica o `TenantBootstrapSeeder` inteiro — inclusive `ResourcePermissions`
   (`Administrador` ganha `Full` em tudo; cada role de módulo ganha `Full` no
   módulo dela). Um admin revoga o acesso do RH a `employees`, alguém reinicia a
   API, e o grant volta como `Full`. **Revogação não sobrevive a um restart.** O
   seeder checa `exists` antes de inserir, então não distingue "nunca concedido"
   de "revogado" — ambos são ausência de linha.
2. **A senha do admin está chumbada e é escrita no log em texto puro.**
   `admin@SBP.com` / `Admin@123` no `DbInitializer`, e a senha sai em log nível
   Info. O Serilog tem sink para tabela, então em produção isso deposita a
   credencial do admin master no armazenamento de log.
3. **`POST /api/tenants` não tem checagem de role.** Só `[Authorize]`. Qualquer
   usuário autenticado cria tenant. No modelo em que tenant é vendido, é furo de
   segurança e de negócio.
4. **O middleware aceita o claim `tenant_id` sem reconferir associação.** Só o
   caminho do header `X-Tenant-Id` chama `IsMemberAsync`. Quem for removido de um
   tenant mantém o `TenantContext` daquele tenant até o token expirar — até 8h.
5. **Worktrees obsoletos** em `.worktrees/` ainda com a estrutura pré-rename.
6. **`NU1903` de severidade alta** em `System.Security.Cryptography.Xml` 10.0.7 e
   `Microsoft.OpenApi` 2.0.0.

Os itens 5 e 6 são higiene e ficam fora deste design; entram no backlog.

## Seção 1 — O gate único: `[TenantModule]`

Um atributo aplicado no topo de cada controller de módulo, ex.
`[TenantModule("employees")]`, implementando `IAsyncAuthorizationFilter`. Como
filtro de autorização, ele roda **depois** do roteamento, então os valores de
rota estão garantidamente disponíveis — sem depender de onde o `UseRouting`
implícito do `WebApplication` foi inserido no pipeline.

Sequência, e **a ordem importa**:

1. **Identifica o usuário** pelo claim `NameIdentifier`. Ausente → `401`.
2. **Lê o `tenantId` da rota.** É entrada não confiável: o alvo da prova, não a
   prova.
3. **Exige o claim `tenant_id` presente e igual ao da rota.** Diferente → `403`.
   Ausente → `403`.
4. **Prova a associação:** linha em `TenantUsers` para (`tenantId` da rota,
   `userId` do token). Sem linha → `403`. O `TenantRole` encontrado vai para
   `HttpContext.Items`, para as actions reaproveitarem.
5. **Preenche o `TenantContext` com o tenant da rota.** A partir daqui contexto
   ambiente e rota são iguais por construção.
6. **Checa a permissão de recurso** via `UserHasAccessAsync`, que agora resolve as
   roles no tenant correto *porque o passo 5 veio antes*. O nível sai do verbo
   HTTP — `GET`/`HEAD` → `Read`, `POST`/`PUT`/`PATCH` → `Write`, `DELETE` →
   `Full` — com override explícito por action quando fugir da regra.

**As roles não vão para o código.** O atributo declara recurso e nível mínimo. O
mapeamento role → recurso é dado, por tenant, na tabela
`ResourcePermission (TenantId, RoleId, ResourceId, Level)`. Isso permite, sem
recompilar nada: tenant A dando `Full` em `employees` para `RH`; tenant B dando
`Read` no mesmo recurso para `Financeiro`; tenant C não dando a ninguém. Planos e
assinaturas diferentes são grants diferentes.

**Segurança da resolução pela rota.** "Resolver pela rota" nunca significa
confiar na rota. O `userId` vem do JWT assinado (confiável), o `tenantId` vem da
URL (não confiável), e o passo 4 prova no banco que aquele usuário pertence
àquele tenant. O passo 3 acrescenta que a rota tem de bater com o tenant
selecionado. É a combinação que dá a garantia — e é exatamente o que hoje não
existe, porque o `TenantContext` sai do claim enquanto o service lê pela rota,
dois valores independentes sem ninguém conferindo que são iguais.

### Mapeamento de `resourceCode` por controller

Conferido contra `TenantBootstrapSeeder.DefaultResources`, que tem 12 recursos:

| Controller | `resourceCode` |
|---|---|
| `EmployeesController` | `HR.Employees` |
| `WorkLogsController` | `HR.WorkLogs` |
| `PaymentsController` | `HR.Payments` |
| `PaymentPeriodsController` | `HR.PaymentPeriods` |
| `ChartOfAccountsController` | `ChartOfAccounts.Management` |
| `GeneralLedgerController` | `GeneralLedger.Management` |
| `AccountsPayableEntriesController` | `AccountsPayable.Entries` |
| `AccountsPayableCategoriesController` | `AccountsPayable.Entries` |
| `AccountsPayableReportsController` | `AccountsPayable.Entries` |

**A lacuna, e a decisão:** o módulo de Contas a Pagar tem **três** controllers e
apenas **um** recurso no catálogo. Não existem `AccountsPayable.Categories` nem
`AccountsPayable.Reports`. Os três apontam para `AccountsPayable.Entries` — um
código por módulo. Inventar dois recursos novos agora significaria criar entradas
de catálogo, migration de backfill e grants para uma granularidade que ninguém
pediu. Fica registrado como refinamento futuro: se um dia categorias ou
relatórios precisarem de permissão própria, são recursos novos entrando pela
migration de backfill descrita na seção 3.

Consequência a aceitar: quem tiver `Write` em `AccountsPayable.Entries` pode
gerenciar categorias. Hoje isso é gateado por `CanManage` (Owner/Admin), que
permanece no lugar durante a fase 1 — então na prática nada afrouxa agora.

### `TenantsController` é exceção explícita

O `TenantsController` tem rota de controller `api/tenants`, sem `{tenantId}`, mas
**algumas actions têm** (`POST {tenantId:guid}/members`, etc.). Ele não recebe
`[TenantModule]`, e o motivo é de fundo: é o controller que *gerencia* associação,
então exigir associação provada nele criaria dependência circular — não haveria
como adicionar o primeiro membro, nem como o master admin se auto-incluir.

Ele mantém as próprias checagens e entra numa **lista de exceções declarada no
teste de arquitetura**, para que a exceção seja visível e revisável, nunca
implícita. Mesma coisa para `AuthController`, `PermissionsController` e
`ResourcesController`, que não são roteados por tenant.

### Consequência deliberada do passo 3

Hoje um token **sem** tenant selecionado consegue usar as rotas
`api/tenants/{id}/…`, porque esses controllers nunca olharam o claim. Passa a
receber `403`. O frontend não quebra — ele sempre seleciona tenant antes e
reemite o token —, mas scripts e chamadas manuais que pulavam esse passo
começam a falhar. É aperto proposital, não regressão.

## Seção 2 — Filtro global fail-closed (item 10) e config de banco (item 12)

```csharp
modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
    _tenantContext != null && _tenantContext.HasTenant && e.TenantId == _tenantContext.TenantId);
```

Sem tenant resolvido, não volta linha nenhuma — em vez de voltar todas.

Três consequências que precisam estar escritas para ninguém "consertar" errado:

- **Os 25 sites de `ResourcePermissionService` (16) e `PermissionService` (9)** são
  os únicos que dependem do filtro global — exatamente os dois arquivos onde o
  vazamento do `73dc458` aconteceu. Passam a devolver vazio sem tenant.
- **O construtor sem `ITenantContext`** (`ApplicationDbContext.cs:12`, usado pelo
  `ApplicationDbContextFactory`) deixa `_tenantContext` nulo e portanto filtra
  tudo. Design-time só roda migration, e migration não faz query: inofensivo, mas
  documentado.
- **Os seeders não quebram** — já usam `IgnoreQueryFilters()` em todas as
  leituras, e escrita não passa por query filter.

`ITenantContext` está registrado como `AddScoped`
(`DependencyInjectionConfiguration.cs:27`) — **verificado**. O passo 5 vale pelo
resto da requisição sem vazar entre requisições.

**Item 12**, que entra como consequência natural:

- O `ApplicationDbContextFactory` para de ter a base chumbada
  (`SaaSBasePlatform`) e passa a ler a configuração, eliminando a divergência com
  a base efetiva da aplicação (`SaaSBasePlatformDb`).
- O `appsettings.ConnectionStrings.json` é apagado: nenhum `AddJsonFile` o carrega
  e ele nem é copiado para o output. `README.md` e `CLAUDE.md` são corrigidos, já
  que ambos descrevem esse arquivo como fonte das connection strings.

## Seção 3 — Semeadura: o que é migration e o que é runtime

**Migration** (roda uma vez, versionada, registrada no `__EFMigrationsHistory`):

- Roles globais do Identity e o catálogo de `Permission` — estáticos, conhecidos
  em build time.
- **Backfill de catálogo por módulo**, com SQL sobre os tenants existentes:

  ```sql
  INSERT INTO "Resources" ("TenantId", "Code", ...)
  SELECT t."Id", 'employees', ... FROM "Tenants" t
  WHERE NOT EXISTS (
    SELECT 1 FROM "Resources" r WHERE r."TenantId" = t."Id" AND r."Code" = 'employees'
  );
  ```

  Uma migration por módulo novo. Paga em definitivo a dívida do item 9 ("tenant
  antigo não recebe recurso novo") e é o que impede o `[TenantModule]` de trancar
  um tenant inteiro fora por falta de linha no catálogo.

**Runtime, só na criação do tenant:**

- O `TenantBootstrapSeeder`, chamado de `TenantService.CreateAsync`, onde já está.
  Catálogo **e** grants padrão, uma vez, no nascimento do tenant. Uma migration
  não consegue semear um tenant que ainda não existe.

**Deixa de existir:**

- `DbInitializer.EnsureTenantBootstrapAsync` — a origem do achado nº 1.

Cobertura completa e sem sobreposição: tenant antigo pela migration de backfill,
tenant novo pelo bootstrap de criação, e nada rodando repetidamente.

O `MigrateAsync` no startup **fica por ora**: com a semeadura dentro das
migrations, o histórico faz dele um no-op a partir da segunda execução. Removê-lo
continua valendo por privilégio de DDL, mas isso é o **item 11** e é trabalho de
DevOps, fora deste pacote.

**Credencial do admin** (achado nº 2). Não vai para migration — migration não tem
`UserManager` para gerar hash, e cravar um hash pronto no repo é pior que o
problema. Fica em runtime, com regra por ambiente:

- **Development:** cria o admin com senha vinda de configuração/user secrets.
- **Production:** não cria admin padrão. Exige variável de ambiente e falha alto
  se ela não vier.
- **Nunca** escreve a senha no log, em ambiente nenhum.

## Seção 4 — Provisionamento e suporte

- **`POST /api/tenants` passa a exigir a role global `Administrador`** (achado
  nº 3).
- **Acesso de suporte é associação, sem exceção.** Nos tenants que o master admin
  cria, o `CreateAsync` já o insere como `Owner` — nada muda. Não há bypass: um
  bypass reintroduziria o vazamento cross-tenant que o trabalho de RBAC removeu, e
  seria um caminho que o teste de arquitetura **não consegue ver**, por ser a
  exceção legítima.
- **Endpoint novo, só para master admin,** que o insere como membro de um tenant
  que ele não criou, gravando em `PermissionAuditLog`. Suporte continua possível,
  mas vira ato registrado em vez de poder invisível.
- **Entrega ao cliente** já existe via `CreateAndAddMemberAsync`: cria-se o tenant,
  cria-se o usuário do cliente e ele fica como Admin do próprio tenant.

## Seção 5 — Testes

**Teste de arquitetura** — materializa a decisão 1. Por reflexão sobre os
controllers:

1. Toda action cujo **template de rota efetivo** (controller + action combinados)
   contém `{tenantId}` **tem** que estar coberta por `[TenantModule]`, salvo se a
   classe estiver na lista de exceções declarada no próprio teste
   (`TenantsController`, `AuthController`, `PermissionsController`,
   `ResourcesController`). Sem cobertura e sem exceção, falha com o nome da
   classe e da action.

   A avaliação é **por action, não por classe**, e a diferença é material: o
   `TenantsController` tem rota de controller sem `{tenantId}` e actions com. Uma
   regra por classe deixaria passar exatamente esse caso.
2. O `resourceCode` declarado **tem** que existir no catálogo de recursos. Um erro
   de digitação como `[TenantModule("employes")]` não acharia grant nenhum e
   negaria o endpoint para todo mundo, para sempre — um `403` permanente que
   parece problema de permissão do usuário. Esta asserção é o que cobre o
   *engano*, já que a primeira só cobre o *esquecimento*.

**Testes de isolamento** — a rede de regressão que hoje não existe automatizada
(o trabalho anterior validou cross-tenant dirigindo a API por script, o que prova
mas não protege):

- membro do tenant A batendo na rota do tenant B → `403`
- claim de A com rota de B → `403`
- token sem claim de tenant na rota de A → `403`
- membro de A **sem** a feature role → `403` no módulo
- membro de A **com** a feature role → `200`
- consulta em entidade `ITenantScoped` sem tenant resolvido → vazio, não tudo
- **grant revogado continua revogado depois de reiniciar** — regressão direta do
  achado nº 1

O projeto usa xUnit + NSubstitute e não tem harness de integração HTTP. O atributo
é testado isoladamente, fabricando o `AuthorizationFilterContext`, o que cobre os
seis ramos de decisão sem subir a aplicação. Um harness com `WebApplicationFactory`
seria mais fiel, mas é peso adicional e **não** é necessário para as garantias
acima; fica como opção futura.

## Seção 6 — Faseamento

**Fase 1 — aditiva, nada é removido.** As proteções antigas permanecem como
redundância, de modo que qualquer quebra tenha origem identificável.

1. `[TenantModule]` criado e aplicado nos 9 controllers
2. Filtro global fail-closed
3. Item 12: factory lendo configuração, arquivo morto apagado, docs corrigidos
4. Semeadura reorganizada: migrations + `EnsureTenantBootstrapAsync` apagado
5. `POST /api/tenants` restrito + endpoint de suporte auditado
6. Credencial do admin por ambiente, e fora do log
7. Os testes da seção 5

**Fase 2 — limpeza, só depois da fase 1 verde e rodada de verdade.** Vira um plano
separado, escrito depois.

- Remover os ~87 `IgnoreQueryFilters` que viraram redundantes
- Remover `CanAccess`/`GetRoleAsync` por action, usando o papel já resolvido em
  `HttpContext.Items`
- Migrar o `PermissionsController` para `[TenantModule]` e aposentar o
  `[RequireResourceAccess]`, ficando com um atributo só
- Limpar os `IgnoreQueryFilters` que são **no-op** em `WorkLogs`, `Payments` e
  `PaymentPeriods` — essas entidades não são `ITenantScoped`, não há filtro para
  ignorar, e a chamada lê como bypass deliberado de uma proteção que não existe

A fase 1 é grande — sete frentes, e a de semeadura mexe em migration. O plano de
implementação deve quebrá-la em commits pequenos e verificáveis.

## Fora de escopo

- **Item 8** (cadastro, confirmação de e-mail, esqueci a senha): não tem relação
  com isolamento, não há infra de e-mail no projeto, e ainda depende de decisão de
  produto. Vale registrar uma interação favorável: com a regra da seção 1, quem se
  cadastra sozinho e não pertence a tenant nenhum recebe `403` em tudo que é
  tenant-scoped — que é exatamente o estado de "aguardando convite" desejado.
- **Item 11** (superuser do Postgres, roles migrator/runtime, rede, RLS): DevOps,
  corre em paralelo.
- **Itens 1–6** (quick wins e pacote RBAC): sem relação com este design.
- Achados nº 5 e nº 6 (worktrees obsoletos, `NU1903`): higiene, vão para o backlog.

## A verificar durante a implementação

- Se algum fluxo legítimo depende de chamar rota de tenant **sem** claim de tenant
  selecionado — a consequência deliberada do passo 3 o quebraria. É a única
  incógnita que pode exigir revisão de decisão já tomada.
- O `ChartOfAccountsSeeder` **não** entra neste design: ele semeia dado de negócio
  (plano de contas), não catálogo de permissão, e roda só na criação do tenant.
  Nada no `[TenantModule]` depende dele. Fica registrado só para que ninguém o
  arraste junto por parecer irmão do `TenantBootstrapSeeder`.

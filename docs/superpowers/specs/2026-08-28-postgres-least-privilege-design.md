# Design: a aplicação deixa de ser superuser do Postgres (item 11, parte local)

> Escrito em 2026-08-28. Cobre a metade do item 11 do `docs/MVP-BACKLOG.md` que
> roda e se verifica **inteira contra o Docker local**. A metade de Azure
> (private endpoint/VNet, Entra ID + Managed Identity, RLS com `FORCE`) fica
> para o pass de infra, que exige `az login` e é tarefa a dois.

## Objetivo

Hoje a API conecta como `postgres` (`appsettings.json:3`,
`Username=postgres;Password=postgres`). Superuser ignora toda checagem de
permissão: um `DROP SCHEMA public CASCADE` vindo de um bug ou de uma injeção
apaga as 25 tabelas, e não há grant, revoke ou policy que segure. Também torna
RLS inútil — superuser ignora policies, e o *owner* da tabela ignora a menos que
se use `ALTER TABLE … FORCE ROW LEVEL SECURITY`.

O alvo: **a conexão de runtime não consegue fazer DDL.** Prova de aceite é
negativa e direta — um `CREATE TABLE` pela conexão da aplicação tem que devolver
`permission denied`, com as telas continuando a funcionar.

## O que empurra a aplicação para o superuser

Três coisas, e só a primeira era conhecida:

1. **`Program.cs:30` → `DbInitializer.InitializeAsync`** roda
   `context.Database.MigrateAsync()` a cada startup. Migration é DDL.
2. **O Serilog faz DDL em runtime.** `LoggingConfiguration.cs:54` passa
   `needAutoCreateTable: true` ao sink PostgreSQL, que emite um
   `CREATE TABLE IF NOT EXISTS logs` **na connection string da aplicação**, no
   startup. Achado desta sessão; não estava no backlog. Sozinho, ele quebraria a
   aplicação num role só-DML.
3. **As tabelas são todas owned by `postgres`.** Mesmo que a app trocasse de
   role, um role que *possui* a tabela mantém DDL sobre ela.

O que **não** é problema: criar as roles canônicas, o backfill e a criação do
admin, que o `InitializeAsync` também faz, são DML puro e continuam rodando no
startup sem privilégio extra.

## Uma correção ao backlog

O item 11 pede corrigir `SslMode=Require;Trust Server Certificate=true` no
template `PostgresProd` de `appsettings.ConnectionStrings.json:5`. **Esse arquivo
foi apagado no item 12** e hoje não existe nenhuma ocorrência de `SslMode` ou
`TrustServerCertificate` no repo. O bullet está resolvido por tabela rasa; o
`VerifyFull` volta à pauta quando a connection string de produção existir de
verdade, no pass de Azure.

## Decisões

| # | Decisão | Escolha |
|---|---|---|
| 1 | Quem faz DDL | **Dois roles**: `prumo_migrator` (dono do schema) e `prumo_app` (só DML) |
| 2 | Onde vive a criação dos roles | **Script SQL idempotente versionado**, montado no `docker-entrypoint-initdb.d` |
| 3 | Migration no startup | **Sai**, atrás de `Database:MigrateOnStartup`, default `false` em **todo** ambiente |
| 4 | O que acontece se o schema estiver atrasado | **Quebra o startup** com mensagem que diz o comando a rodar |
| 5 | Tabela `logs` do Serilog | Criada por **migration**; `needAutoCreateTable` vira `false` |
| 6 | Tabelas futuras | `ALTER DEFAULT PRIVILEGES`, para migration nova não exigir editar o script |

### Por que o default do flag é `false` também em Development

A tentação é deixar `MigrateOnStartup: true` em dev, pela conveniência do
`dotnet run`. Mas aí a conexão de dev volta a precisar de DDL e o exercício vira
teatro: o caminho que se testa todo dia deixa de ser o caminho que roda em
produção. O flag existe como escotilha (quando ligado, migra por uma conexão
**separada**, com a credencial do migrator, e não pela conexão da aplicação),
mas o padrão é o honesto: migration é um passo à parte.

O custo é um comando a mais num clone novo, e a decisão 4 paga por ele — em vez
de subir contra um schema velho e falhar de forma obscura mais tarde, o startup
morre dizendo `dotnet ef database update -p Prumo.Infrastructure -s Prumo.Api`.

### Por que a checagem de pendência é só-leitura

`GetPendingMigrationsAsync` lê `__EFMigrationsHistory` e compara com o assembly.
Nenhum privilégio além de `SELECT`, e nada escrito. É o que permite manter uma
verificação de integridade no startup sem reabrir o buraco que estamos fechando.

## Arquitetura

### Os dois roles

```
prumo_migrator  — LOGIN, dono de todas as tabelas de public, CREATE no schema
prumo_app       — LOGIN, USAGE em public, SELECT/INSERT/UPDATE/DELETE nas
                  tabelas, USAGE/SELECT/UPDATE nas sequences. Sem CREATE,
                  sem ownership, não é membro de prumo_migrator.
```

O script (`db/roles.sql`) é idempotente e faz, nesta ordem: cria os dois roles
se não existirem; transfere para `prumo_migrator` a posse de tudo que hoje
pertence a `postgres` em `public`; concede os privilégios de DML a `prumo_app`;
revoga `CREATE` em `public` de `PUBLIC` e de `prumo_app`; e registra
`ALTER DEFAULT PRIVILEGES FOR ROLE prumo_migrator` para que tabela criada por
migration futura já nasça com o grant certo.

O `ALTER DEFAULT PRIVILEGES` é o que impede este script de virar dívida: sem
ele, toda migration que criasse tabela exigiria lembrar de reeditá-lo — e
esquecer só apareceria em runtime, num 500.

### As duas connection strings

- `ConnectionStrings:DefaultConnection` → `prumo_app`. É a única que o
  `ApplicationDbContext` registrado no DI conhece, e a única que o Serilog usa.
- `ConnectionStrings:MigratorConnection` → `prumo_migrator`. Lida pelo
  `ApplicationDbContextFactory` (design-time, `dotnet ef`) e pelo caminho do
  flag da decisão 3. **Ausente em produção**, onde migration é passo de deploy.

O `ApplicationDbContextFactory` passa a preferir `MigratorConnection` e cair
para `DefaultConnection` quando ela não existir — assim `dotnet ef` funciona num
banco que ainda não tem os roles separados.

### O startup, depois da mudança

```
InitializeAsync
├── se Database:MigrateOnStartup  → migra por um contexto próprio (MigratorConnection)
├── senão                         → GetPendingMigrationsAsync; se houver, LANÇA
├── roles canônicas do Identity   (DML)
├── backfill de roles por tenant  (DML)
└── admin semeado                 (DML)
```

O `catch` que hoje engole tudo em `InitializeAsync` **não pode** engolir a falha
de migration pendente — senão a decisão 4 não existe na prática. Essa exceção
sobe e derruba o processo.

## Testes

O que dá para cobrir com a suíte (xUnit + EF InMemory) é pouco e é honesto dizer
qual é:

- `MigrateOnStartup` ausente da configuração resolve para `false`.
- O factory de design-time prefere `MigratorConnection` e cai para
  `DefaultConnection` quando ela falta.

O resto é verificação manual contra Postgres real, e é onde está a prova:

1. `CREATE TABLE ddl_probe(x int)` pela connection string da aplicação →
   **`permission denied for schema public`**. Este é o aceite do item.
2. `dotnet ef database update` com a `MigratorConnection` → sucesso.
3. API sobe, `/health/ready` verde, login funciona, a tabela `logs` recebe
   linhas novas (prova que a decisão 5 não quebrou o sink).
4. Smoke test das telas de módulo com um Member de role limitada — as mesmas
   asserções dos passes anteriores, para garantir que nenhum caminho de escrita
   dependia de privilégio que sumiu.
5. Startup com migration pendente → processo morre com a mensagem certa.

O passo 4 é o que pega o risco real desta mudança: um `INSERT` que funcionava
por ser superuser e agora falta grant. Não há como deduzir isso do código.

## Fora do escopo, de propósito

- **Repo de DevOps.** O passo de migration no `deploy.yml` e a Managed Identity
  vão no pass de Azure. Enquanto a stack está destruída, nada quebra.
- **RLS.** Só faz sentido depois dos roles separados, e com `FORCE`. É o passo
  seguinte deste mesmo item, não deste PR.
- **Trocar a senha do `postgres` local.** O superuser continua existindo para
  administrar o banco; o que muda é que a aplicação não o usa.

---

## Registro de execução (2026-08-28)

Feito na branch `feature/item11-postgres-least-privilege`. Suíte **111** (eram
107). Tudo abaixo foi verificado contra PG 17.9 em `docker compose` e a API viva
em `localhost:5201`.

### O achado que mudou o escopo: o sink de log nunca funcionou

O design previa mexer no Serilog porque `needAutoCreateTable: true` fazia DDL na
conexão da aplicação. Ao verificar que a tabela `logs` continuava recebendo
linhas, veio a surpresa: **ela estava vazia, e sempre esteve** — a linha mais
antiga era a de teste inserida na própria sessão.

Com o `SelfLog` do Serilog ligado, a causa apareceu na primeira tentativa:

```
Exception while emitting periodic batch from Serilog.Sinks.PostgreSQL.PostgreSQLSink:
System.ArgumentException: Cannot write DateTimeOffset with Offset=-03:00:00 to
PostgreSQL type 'timestamp with time zone', only offset 0 (UTC) is supported.
```

O `TimestampColumnWriter` entrega o `LogEvent.Timestamp` cru, que é um
`DateTimeOffset` no fuso local; o Npgsql recusa qualquer offset diferente de zero
em `timestamptz`. **Todo batch morria**, e o Serilog engole exceção de sink por
design. Corrigido com um `UtcTimestampColumnWriter` de cinco linhas
(`logEvent.Timestamp.UtcDateTime`), e o `SelfLog` ficou ligado — foi o silêncio
que escondeu isso por meses.

Isso amplia uma frase do backlog que era falsa na prática: *"não escreva senha em
log porque o Serilog tem sink para tabela"*. O sink existia; a tabela, não
recebia nada.

### As duas coisas que a verificação provou, e que o design só supunha

1. **Transferir a posse era metade do trabalho.** `prumo_app` recebe
   `permission denied for schema public` num `CREATE TABLE` — mas o `DROP TABLE`
   só falha (`must be owner of table Tenants`) porque as tabelas passaram a
   pertencer a `prumo_migrator`. Revogar grant sem trocar o dono teria deixado o
   buraco aberto.
2. **O `ALTER DEFAULT PRIVILEGES` funciona como esperado.** Tabela criada depois
   pelo migrator já nasce legível pelo app — testado criando e apagando uma tabela
   de sonda.

### O que foi verificado, e como

| O quê | Resultado |
|---|---|
| `CREATE TABLE` pela conexão da app | `permission denied for schema public` |
| `DROP TABLE "Tenants"` pela conexão da app | `must be owner of table Tenants` |
| Usuário real da conexão da API | `prumo_app` em `pg_stat_activity` |
| Leitura dos módulos | `employees`, `accounts-payable/*`, `chart-of-accounts`, `roles`, `members` → 200 |
| Escrita: categoria de contas a pagar | POST 201, PUT 200, DELETE 204 |
| Escrita: conceder nível de recurso | 200, e `ResourcePermissionAuditLogs` 2 → 3 |
| Tabela `logs` recebendo linha | 3 linhas do startup, `SelfLog` em silêncio |
| Startup com migration pendente | processo morre citando `dotnet ef database update`; porta não responde |
| Escotilha `Database:MigrateOnStartup` | migra pelo migrator e sobe; `health=200` |
| `dotnet ef database update` reaplicado sobre tabela existente | passa, graças ao `IF NOT EXISTS` |

### Um desvio do design

O `Down` da migration `CreateLogsTable` **não dropa a tabela**. Reverter a
migration num banco onde `logs` é anterior a ela apagaria histórico de log que a
migration não criou. Está comentado no próprio arquivo.

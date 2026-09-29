# Prumo — API

[![ci](https://github.com/nickolascheidt/Prumo/actions/workflows/ci.yml/badge.svg)](https://github.com/nickolascheidt/Prumo/actions/workflows/ci.yml)

*[Read in English](README.md)*

Backend de um ERP multi-tenant feito com ASP.NET Core 10 e PostgreSQL: RH (funcionários,
horas trabalhadas, pagamentos e períodos de pagamento), contas a pagar e um núcleo
financeiro simples (plano de contas e razão geral), com controle de acesso por papel
dentro de cada tenant.

É um projeto de portfólio e não roda em produção. O foco é a engenharia de isolamento
entre tenants e de controle de acesso, que é onde um sistema multi-tenant costuma errar.

Repositórios relacionados:
[Prumo-Angular](https://github.com/nickolascheidt/Prumo-Angular) (o SPA) ·
[Prumo-DevOps](https://github.com/nickolascheidt/Prumo-DevOps) (Terraform e scripts de deploy para a AWS)

![Dashboard do Prumo](https://raw.githubusercontent.com/nickolascheidt/Prumo-Angular/main/screenshots/dashboard.png)

Mais telas no [README do frontend](https://github.com/nickolascheidt/Prumo-Angular#readme).

## O que vale olhar

- **Isolamento de tenant que falha fechado.** Toda entidade de tenant passa por um filtro
  global do EF Core. Sem tenant resolvido, a consulta devolve *nada*, e não tudo.
  Entidades sem coluna `TenantId` (horas, pagamentos, linhas de lançamento) são filtradas
  pelo pai. Contornar o filtro com `IgnoreQueryFilters` exige um comentário explicando por
  que a leitura é cross-tenant, e um teste de arquitetura quebra o build sem ele.
- **Um único portão nas rotas de tenant.** Dado de tenant vive em `api/tenants/{tenantId}/…`,
  e o `[TenantModule("<recurso>")]` prova que quem chama pertence ao tenant *da rota*,
  recusa token selecionado para outro tenant e só então confere a permissão no recurso. O
  nível exigido vem do verbo HTTP. Outro teste de arquitetura quebra o build se alguma
  action sob `{tenantId}` ficar sem o portão.
- **RBAC por tenant, com auditoria.** O acesso é `papel × recurso → None / Read / Write /
  Full`. Cada tenant pode criar seus próprios papéis (dois tenants podem ter papéis de
  mesmo nome, garantido por um validador de roles do Identity próprio e um índice
  `NULLS NOT DISTINCT`). Toda concessão e revogação fica registrada, e o acesso de suporte
  do master admin a um tenant é uma associação auditada, não um atalho.
- **Banco com privilégio mínimo.** A API conecta como `prumo_app`, que só faz DML. As
  migrations rodam com outro papel, `prumo_migrator`. No startup a API confere se o schema
  bate com as migrations e se recusa a servir se não bater.
- **Ciclo de conta sem enumeração.** O cadastro não devolve sessão até o e-mail ser
  confirmado, e os endpoints de esqueci-a-senha e de reenviar confirmação respondem igual
  exista o endereço ou não. Admins adicionam membros por convite, então a senha de
  ninguém passa por eles.

## Stack

| Camada | Tecnologia |
|---|---|
| Runtime | .NET 10 / ASP.NET Core |
| Dados | Entity Framework Core 10 (code-first) sobre PostgreSQL 17 |
| Autenticação | ASP.NET Core Identity + JWT |
| Validação | FluentValidation |
| Log | Serilog (console + sink PostgreSQL) |
| Testes | xUnit, NSubstitute, EF Core InMemory |

## Arquitetura

Camadas no estilo clean/onion, com referências num sentido só:

```
Api → Application → Infrastructure → Domain
```

| Projeto | Responsabilidade |
|---|---|
| `Prumo.Domain` | Entidades, enums, nomes das roles canônicas — sem dependências externas |
| `Prumo.Application` | Serviços, DTOs, validators do FluentValidation |
| `Prumo.Infrastructure` | `ApplicationDbContext`, configurações do EF, migrations, seeders, contexto de tenant |
| `Prumo.Api` | Controllers finos, o portão `[TenantModule]`, middleware, configuração |
| `Prumo.Tests` | Testes unitários e de arquitetura, espelhando os projetos acima |

O `Program.cs` é pequeno de propósito: chama um extension method `Add…Configuration` por
assunto, de `Prumo.Api/Configuration/` (banco, autenticação, rate limiting, CORS, log e
assim por diante). Os serviços injetam o `ApplicationDbContext` direto; não há repositório
genérico.

## Rodando localmente

Precisa do [.NET 10 SDK](https://dotnet.microsoft.com/download) e de Docker.

```bash
# PostgreSQL 17, com os dois papéis do banco criados num volume novo
docker compose up -d

# Segredos de desenvolvimento (a chave JWT precisa ter pelo menos 32 caracteres)
dotnet user-secrets set "Jwt:Key" "<uma string aleatória de 32+ caracteres>" -p Prumo.Api
dotnet user-secrets set "Seed:AdminPassword" "<uma senha para o admin semeado>" -p Prumo.Api

# Aplicar as migrations (conecta como prumo_migrator)
dotnet ef database update -p Prumo.Infrastructure -s Prumo.Api

# Subir a API em http://localhost:5201
dotnet run --project Prumo.Api
```

Na primeira subida a API cria as roles canônicas, um tenant `default` e um master admin,
`admin@SBP.com`, com a senha definida acima.

Não há provedor de e-mail. Os e-mails de confirmação de cadastro, redefinição de senha e
convite vão para o log, e em Development a linha de log traz os dados do link, para dar
para seguir os fluxos de ponta a ponta.

A seção [Running it locally](README.md#running-it-locally) do README em inglês tem os
detalhes dos papéis do banco e da configuração.

## Licença

[MIT](LICENSE)

# Prumo — Backend API

[![ci](https://github.com/nickolascheidt/Prumo/actions/workflows/ci.yml/badge.svg)](https://github.com/nickolascheidt/Prumo/actions/workflows/ci.yml)

*[Leia em português](README.pt-BR.md)*

A multi-tenant ERP backend built with ASP.NET Core 10 and PostgreSQL: HR (employees, work
logs, payments, payment periods), accounts payable, and a small financial core (chart of
accounts and general ledger), with per-tenant role-based access control.

This is a portfolio project. It is not running in production anywhere; the point is the
engineering around tenant isolation and access control, which is where a multi-tenant
system usually goes wrong.

Related repositories:
[Prumo-Angular](https://github.com/nickolascheidt/Prumo-Angular) (the SPA) ·
[Prumo-DevOps](https://github.com/nickolascheidt/Prumo-DevOps) (Terraform and deploy scripts for AWS)

## What is worth looking at

- **Tenant isolation that fails closed.** Every tenant-owned entity sits behind a global EF
  Core query filter. With no tenant resolved, queries return *nothing*, not everything.
  Entities without a `TenantId` column (work logs, payments, journal lines) are filtered
  through their parent. Bypassing the filter with `IgnoreQueryFilters` needs a comment
  explaining why the read is cross-tenant, and an architecture test fails the build without it.
- **One gate for tenant routes.** Tenant data lives under `api/tenants/{tenantId}/…`, and
  `[TenantModule("<resource>")]` proves the caller's membership against the tenant *in the
  route*, rejects a token selected for another tenant, and only then checks the resource
  permission. The required level is inferred from the HTTP verb. Another architecture test
  fails the build if any action under `{tenantId}` is left ungated.
- **Per-tenant RBAC with audited changes.** Access is `role × resource → None / Read / Write
  / Full`. Tenants can create their own roles (same-named roles in different tenants are
  allowed, enforced by a custom Identity role validator and a `NULLS NOT DISTINCT` index).
  Every grant and revocation is written to an audit log, and master-admin support access
  to a tenant is an audited membership, not a bypass.
- **Least-privilege database access.** The API connects as `prumo_app`, which can only run
  DML. Migrations run as a separate `prumo_migrator` role. On startup the API checks that
  the schema matches its migrations and refuses to serve if it does not.
- **Account lifecycle without enumeration.** Sign-up returns no session until the e-mail is
  confirmed, and the forgot-password and resend-confirmation endpoints answer the same way
  whether or not the address exists. Admins add members by invitation, so nobody's
  password passes through them.

## Tech stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core |
| Data | Entity Framework Core 10 (code-first) on PostgreSQL 17 |
| Auth | ASP.NET Core Identity + JWT bearer tokens |
| Validation | FluentValidation |
| Logging | Serilog (console + PostgreSQL sink) |
| Tests | xUnit, NSubstitute, EF Core InMemory |

## Architecture

Clean/onion layout with one-way references:

```
Api → Application → Infrastructure → Domain
```

| Project | Responsibility |
|---|---|
| `Prumo.Domain` | Entities, enums, canonical role names — no external dependencies |
| `Prumo.Application` | Services, DTOs, FluentValidation validators |
| `Prumo.Infrastructure` | `ApplicationDbContext`, EF configurations, migrations, seeders, tenant context |
| `Prumo.Api` | Thin controllers, the `[TenantModule]` gate, middleware, configuration |
| `Prumo.Tests` | Unit and architecture tests, mirroring the projects above |

`Program.cs` is deliberately small: it calls one `Add…Configuration` extension per concern
from `Prumo.Api/Configuration/` (database, authentication, rate limiting, CORS, logging and
so on). Services inject `ApplicationDbContext` directly; there is no generic repository.

## Running it locally

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker.

```bash
# PostgreSQL 17, with the two database roles created on a fresh volume
docker compose up -d

# Secrets for development (the JWT key must be at least 32 characters)
dotnet user-secrets set "Jwt:Key" "<a random string of 32+ characters>" -p Prumo.Api
dotnet user-secrets set "Seed:AdminPassword" "<a password for the seeded admin>" -p Prumo.Api

# Apply the migrations (connects as prumo_migrator)
dotnet ef database update -p Prumo.Infrastructure -s Prumo.Api

# Run the API on http://localhost:5201
dotnet run --project Prumo.Api
```

On first start the API seeds the canonical roles, a `default` tenant and a master admin,
`admin@SBP.com`, with the password you set above.

There is no e-mail provider. Sign-up confirmation, password reset and invitation e-mails
are written to the log instead, and in Development the log line carries the link data, so
the flows can be followed end to end.

### Database roles

`db/roles.sql` creates the two Postgres roles and is idempotent:

| Role | Rights | Used by |
|---|---|---|
| `prumo_migrator` | owns everything in `public`, can run DDL | `dotnet ef` |
| `prumo_app` | `SELECT / INSERT / UPDATE / DELETE` only | the running API |

`docker compose up -d` applies it automatically to a fresh volume. An existing database
takes it by hand:

```bash
docker exec -i saasbase-postgres psql -U postgres -d SaaSBasePlatformDb < db/roles.sql
```

### Configuration

`appsettings.json` is the base, with `appsettings.Development.json`,
`appsettings.Production.json` and `appsettings.Demo.json` as overlays. No overlay commits a
secret: the JWT key and the admin password come from user secrets in development and from
environment variables (`Jwt__Key`, `Seed__AdminPassword`) elsewhere. In Production the
connection string is a placeholder that breaks startup on purpose if
`ConnectionStrings__DefaultConnection` is not set.

## Authorization model

Three role concepts, deliberately separate:

| Concept | Where it lives | What it means |
|---|---|---|
| Administrative position | `TenantUsers.Role` (`Owner / Admin / Member`) | Your position within one tenant |
| Feature roles (`HR`, `Finance`, `AccountsPayable`, …) | `TenantUserRoles` | Which modules you can open, per tenant |
| Master admin | Identity role `Administrator` | Global, across tenants |

A feature role opens modules through `ResourcePermission` rows. `Read` on a resource opens
the screen and returns 403 on any write, which is how "can see but not edit" is expressed.

## API overview

Everything requires a bearer token except login, sign-up, e-mail confirmation and password
reset.

| Area | Route |
|---|---|
| Auth | `/api/auth` |
| Tenants, members, invitations | `/api/tenants` |
| Tenant roles | `/api/tenants/{tenantId}/roles` |
| Resources and permissions | `/api/resources` |
| Employees, work logs, payment periods | `/api/tenants/{tenantId}/employees` |
| Payments | `/api/tenants/{tenantId}/payments` |
| Accounts payable | `/api/tenants/{tenantId}/accounts-payable` |
| Chart of accounts | `/api/tenants/{tenantId}/chart-of-accounts` |
| General ledger | `/api/tenants/{tenantId}/general-ledger` |

Public endpoints are rate-limited to 10 requests per minute per IP; authenticated ones to
100 per minute per user.

## Tests

```bash
dotnet test                                                         # everything
dotnet test --filter "FullyQualifiedName~TenantCoverageTests"       # one class
```

Besides unit tests for the services, `Prumo.Tests/Architecture` holds the tests that keep
the design honest: every tenant-routed action is gated, every `IgnoreQueryFilters` is
justified, master-only endpoints stay master-only, and the production overlay carries no
leftover placeholders.

## Adding a module

1. Entities in `Prumo.Domain/Entities/` (implement `ITenantScoped` if they carry a `TenantId`).
2. Service and DTOs in `Prumo.Application/`.
3. EF configuration in `Prumo.Infrastructure/Data/Configurations/`, then
   `dotnet ef migrations add <Name> -p Prumo.Infrastructure -s Prumo.Api`.
4. Declare the resource in `TenantBootstrapSeeder`.
5. A thin controller under `api/tenants/{tenantId:guid}/…` with `[TenantModule("<resource>")]`.

## License

[MIT](LICENSE)

# Prumo ERP — Backend API

ASP.NET Core 10 REST API for a multi-tenant SaaS platform with role-based permissions, resource-level access control, accounts payable, and a financial core (Chart of Accounts + General Ledger).

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core |
| ORM | Entity Framework Core 10 (Code-First) |
| Database | PostgreSQL |
| Cache | Redis (StackExchange.Redis) |
| Auth | JWT Bearer tokens |
| Logging | Serilog (Console + SQL Server sink) |
| Testing | xUnit + NSubstitute |
| Validation | FluentValidation (auto-registered) |

## Architecture

Clean/onion-style with one-way dependencies:

```
Api → Application → Infrastructure → Domain
```

| Project | Responsibility |
|---|---|
| `Prumo.Domain` | Entities, enums, permission constants — no external dependencies |
| `Prumo.Application` | Services, DTOs, FluentValidation validators |
| `Prumo.Infrastructure` | EF Core DbContext, migrations, repositories, Redis cache |
| `Prumo.Api` | Controllers, middleware, DI wiring, configuration extensions |
| `Prumo.Tests` | Unit tests mirroring the production project structure |

### Composition Root

`Program.cs` is intentionally minimal — it calls a chain of extension methods from `Api/Configuration/`:

- `DatabaseConfiguration` — EF Core + PostgreSQL
- `CacheConfiguration` — Redis
- `AuthenticationConfiguration` — JWT Bearer
- `AuthorizationConfiguration` — permission policies
- `DependencyInjectionConfiguration` — repositories, services, validator scanning
- `HealthChecksConfiguration`, `RateLimitingConfiguration`, `CorsConfiguration`, `LoggingConfiguration`, `MiddlewareConfiguration`

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL (default: `localhost:5432`, database `SaaSBasePlatformDb`)
- Redis (default: `localhost:6379`)

## Getting Started

```bash
# Postgres and Redis, with the versions this project expects
docker compose up -d

# Restore dependencies
dotnet restore

# Apply database migrations (connects as the migrator role)
dotnet ef database update -p Prumo.Infrastructure -s Prumo.Api

# Run the API (listens on http://localhost:5201)
dotnet run --project Prumo.Api
```

Migrations are a separate step on purpose. The API connects as `prumo_app`, a role with
no DDL rights, so it cannot migrate itself — see [Database roles](#database-roles). On
startup it only *checks* that the schema matches the migrations in the assembly, and
refuses to serve when it does not, naming the command to run.

`app.InitializeDatabaseAsync()` still seeds the canonical roles and the master admin,
which is plain DML.

### Database roles

`db/roles.sql` creates two Postgres roles and is idempotent:

| Role | Rights | Used by |
|---|---|---|
| `prumo_migrator` | owns everything in `public`, can DDL | `dotnet ef`, the deploy's migration step |
| `prumo_app` | `SELECT/INSERT/UPDATE/DELETE` only | the running API |

`docker compose up -d` applies the script automatically to a **fresh** volume. A database
that already exists takes it by hand:

```bash
docker exec -i saasbase-postgres psql -U postgres -d SaaSBasePlatformDb < db/roles.sql
```

Passwords come from `PRUMO_MIGRATOR_PASSWORD` and `PRUMO_APP_PASSWORD`, falling back to
development values that match `appsettings.json`.

Environments that need the API to migrate itself — the `Demo` overlay, for instance — set
`Database:MigrateOnStartup`. Even then the DDL runs over a separate connection built from
`ConnectionStrings:MigratorConnection`, never over the application's own.

## Configuration

Base config is in `appsettings.json`, including `ConnectionStrings:DefaultConnection`. Environment overlays: `appsettings.Development.json`, `appsettings.Production.json`, `appsettings.Demo.json`. In Production the connection string must come from the environment (`ConnectionStrings__DefaultConnection`) — the file ships a placeholder on purpose, so a missing variable fails at startup instead of silently falling back.

Key sections:

```json
{
  "Jwt": {
    "Issuer": "PrumoApi",
    "Audience": "PrumoClient",
    "ExpirationHours": 8
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=SaaSBasePlatformDb;..."
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

## API Overview

All endpoints require a `Bearer` token except `POST /api/auth/login` and `POST /api/auth/register`.

| Controller | Base Route | Description |
|---|---|---|
| Auth | `/api/auth` | Login, register, current user, tenant selection |
| Tenants | `/api/tenants` | Multi-tenant membership management |
| Permissions | `/api/permissions` | Permission catalog and role assignment |
| Resources | `/api/resources` | Resource-level access control |
| Accounts Payable | `/api/tenants/{id}/accounts-payable` | AP entries, categories, reports |
| Chart of Accounts | `/api/tenants/{id}/chart-of-accounts` | Account hierarchy (Owner/Admin only for writes) |
| General Ledger | `/api/tenants/{id}/general-ledger` | Journal entries and account statements |

### Rate Limits

- `public` policy (login/register): 10 requests/min
- `authenticated` policy: 100 requests/60 s

## Authorization Model

Two complementary authorization mechanisms:

**1. Role-based permission policies**
Permission strings use dot-notation with wildcard support (e.g., `employees.view`, `payments.*`). Enforced by `PermissionAuthorizationHandler`. The full catalog lives in `Domain/Authorization/Permissions.cs`. Permission sets are Redis-cached.

**2. Per-resource access**
`[RequireResourceAccess]` attribute on controllers/actions checks `ResourcePermission` against the requesting user. Access levels: `None / Read / Write / Full`. All access checks are audit-logged to `PermissionAuditLog`.

## Data Access

Use `IRepository<T>` / `IUnitOfWork` — do not inject `ApplicationDbContext` directly into Application-layer services. `QueryableExtensions` provides paging helpers that return `PagedResult<T>`.

## Adding a New Feature

1. Add domain entities to `Domain/Entities/`
2. Add service interface + implementation to `Application/Services/`
3. Add DTOs and validators to `Application/DTOs/`
4. Add EF configuration to `Infrastructure/Data/Configurations/`
5. Create migration: `dotnet ef migrations add <Name> -p Prumo.Infrastructure -s Prumo.Api`
6. Add a thin controller to `Api/Controllers/`
7. Register permissions in `Domain/Authorization/Permissions.cs` and wire the policy in `AuthorizationConfiguration`

## Running Tests

```bash
dotnet test                                                        # all tests
dotnet test --filter "FullyQualifiedName~MyServiceTests"           # single class
dotnet test --filter "FullyQualifiedName~MyServiceTests.MyMethod"  # single test
```

## Related Repository

Frontend: [SaaSBasePlatform-Angular](https://github.com/nickolascheidt/SaaSBasePlatform-Angular) — Angular 18 SPA that consumes this API.

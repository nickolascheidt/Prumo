# SaaS Base Platform — Backend API

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
| `SaaS_BasePlatform.Domain` | Entities, enums, permission constants — no external dependencies |
| `SaaS_BasePlatform.Application` | Services, DTOs, FluentValidation validators |
| `SaaS_BasePlatform.Infrastructure` | EF Core DbContext, migrations, repositories, Redis cache |
| `SaaS_BasePlatform.Api` | Controllers, middleware, DI wiring, configuration extensions |
| `SaaS_BasePlatform.Tests` | Unit tests mirroring the production project structure |

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
# Restore dependencies
dotnet restore

# Apply database migrations
dotnet ef database update -p SaaS_BasePlatform.Infrastructure -s SaaS_BasePlatform.Api

# Run the API (listens on http://localhost:5201)
dotnet run --project SaaS_BasePlatform.Api
```

The API runs migrations and seeds initial data automatically on startup via `app.InitializeDatabaseAsync()`.

## Configuration

Base config is in `appsettings.json`. Connection strings are kept in `appsettings.ConnectionStrings.json`. Environment overlays: `appsettings.Development.json`, `appsettings.Production.json`, `appsettings.Demo.json`.

Key sections:

```json
{
  "Jwt": {
    "Issuer": "SaaS_BasePlatformApi",
    "Audience": "SaaS_BasePlatformClient",
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
| API Keys | `/api/tenants/{id}/api-keys` | Tenant API key management |
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
5. Create migration: `dotnet ef migrations add <Name> -p SaaS_BasePlatform.Infrastructure -s SaaS_BasePlatform.Api`
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

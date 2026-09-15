# Integration tests (PostgreSQL)

MovieApp integration tests run against a **local/test PostgreSQL instance only**. They do not use staging or production databases.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker Desktop or Docker Engine with Compose v2
- Local PostgreSQL from `docker-compose.yml` (recommended)

Optional for a subset of tests:

- **Redis** — only required for `RedisHealthCheckApiTests` when Redis is configured. Most fixtures set `Redis:ConnectionString` empty and use in-memory cache.

## Start test PostgreSQL

1. Copy `.env.example` to `.env` and set a local password:

```bash
cp .env.example .env
# edit POSTGRES_PASSWORD and PostgreSql__Password to the same local value
```

2. Start infrastructure:

```bash
docker compose up -d postgres
docker compose ps
```

Default connection (from `tests/MovieApp.IntegrationTests/appsettings.IntegrationTests.json`):

| Setting | Default |
|---|---|
| Host | `localhost` |
| Port | `5432` |
| Username | `movieapp` |
| Password | from `PostgreSql__Password` or `POSTGRES_PASSWORD` |

PowerShell:

```powershell
$env:PostgreSql__Password = "your-local-development-password"
```

bash:

```bash
export PostgreSql__Password="your-local-development-password"
```

## Run commands

Release build (quality gate):

```bash
dotnet build tests/MovieApp.IntegrationTests/MovieApp.IntegrationTests.csproj -c Release
```

Full PostgreSQL integration suite:

```bash
dotnet test tests/MovieApp.IntegrationTests/MovieApp.IntegrationTests.csproj -c Release
```

## Database isolation

Each test area uses a **dedicated database name** (for example `movieapp_auth_integration_tests`, `movieapp_home_integration_tests`) resolved through `IntegrationTestDatabase`.

Fixtures typically:

1. `MigrateAsync()` on initialize
2. Reset/delete scoped data between tests
3. `EnsureDeletedAsync()` on fixture dispose

Tests must not depend on execution order. Do not point integration tests at staging/production connection strings.

## Cleanup

Fixture dispose drops the per-suite database (`EnsureDeletedAsync`). Stopping Docker keeps data in the `movieapp-postgres-data` volume until removed:

```bash
docker compose down
docker compose down -v   # also removes volumes
```

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `Set PostgreSql__Password ... for integration database tests` | Password env var missing | Export `PostgreSql__Password` matching Docker `.env` |
| `password authentication failed for user "postgres"` | Legacy hardcoded credentials | Use current `CollectionsIntegrationDatabase` / `IntegrationTestDatabase` helpers (not `postgres/postgres`) |
| `Connection refused` on `localhost:5432` | PostgreSQL container not running | `docker compose up -d postgres` |
| Hang/timeouts | Port conflict or unhealthy container | `docker compose ps`, check health, free port 5432 |

## CI

GitHub Actions builds IntegrationTests in Release on every push/PR. A PostgreSQL service-container job runs the suite when secrets are not required (see `.github/workflows/ci.yml`).

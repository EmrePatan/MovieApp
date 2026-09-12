# MovieApp — Production Database & Cache Runbook (Step 29E-2)

**Scope:** PostgreSQL (EF Core) and Redis (`ICacheService`) readiness for future managed production deployment.  
**Status:** Documentation and local verification only — no cloud provisioning, no production secrets in git.

---

## Configuration keys (source of truth)

MovieApp does **not** use `ConnectionStrings:DefaultConnection`. All persistence settings bind to existing options classes in `MovieApp.Infrastructure.Configuration`.

### PostgreSQL

| Config key | Environment variable | Purpose |
|------------|---------------------|---------|
| `PostgreSql:ConnectionString` | `PostgreSql__ConnectionString` | **Preferred for production** — full Npgsql connection string |
| `PostgreSql:Host` | `PostgreSql__Host` | Hostname (managed provider endpoint) |
| `PostgreSql:Port` | `PostgreSql__Port` | Port (default `5432`) |
| `PostgreSql:Database` | `PostgreSql__Database` | Database name |
| `PostgreSql:Username` | `PostgreSql__Username` | Database user |
| `PostgreSql:Password` | `PostgreSql__Password` | Database password (secret) |

Resolution order (`PostgreSqlOptions.ResolveConnectionString()`):

1. If `ConnectionString` is non-empty → use it.
2. Otherwise build: `Host=…;Port=…;Database=…;Username=…;Password=…`

Registration: `DependencyInjection.AddInfrastructure()` → `UseNpgsql(connectionString)`.

**Production must not rely on:**

- `localhost` or Docker service names in application code
- `.env` files on the server
- ASP.NET Core User Secrets
- Hard-coded credentials in `appsettings*.json`

### Redis

| Config key | Environment variable | Purpose |
|------------|---------------------|---------|
| `Redis:ConnectionString` | `Redis__ConnectionString` | StackExchange.Redis connection string |
| `Redis:InstanceName` | `Redis__InstanceName` | Key prefix (default `MovieApp:`) |

If `ConnectionString` is empty, the API falls back to `DistributedMemoryCache` (single-instance only).

---

## EF Core migrations

### Current migration history (8)

| Migration | Purpose |
|-----------|---------|
| `20260911114142_InitialMovieCatalog` | Movies, TV, genres, people |
| `20260911123034_AddUsersAndAuthentication` | Users |
| `20260911130446_AddFavoritesAndWatchlists` | Favorites, watchlists |
| `20260911131920_AddRatingsAndReviews` | Ratings, reviews |
| `20260911133042_AddWatchHistory` | Watch history |
| `20260911134310_AddSearchHistory` | Search history |
| `20260911135245_AddUserSecurityStamp` | JWT revocation stamp |
| `20260912135301_AddPasswordResetTokens` | Password reset tokens |

Project: `src/MovieApp.Infrastructure`  
Startup project: `src/MovieApp.Api`

### Automatic migration at startup

**Not enabled.** The API does not call `Database.Migrate()` or `MigrateAsync()` at startup.

Migrations run:

- In integration tests (per-fixture `MigrateAsync()` against isolated test DB)
- Manually via `dotnet ef database update` during deployment

**Recommendation:** Keep migrations **outside** API startup for production. Apply once per release via CI/CD or operator workflow before or immediately after deploying a new API version. Auto-migrate at startup is unsafe with multiple instances and complicates rollbacks.

### Initialize an empty production database

**Never copy the development database. Never restore dev data into production.**

1. Provision an **empty** managed PostgreSQL database (SSL required).
2. Store the connection string in the platform secret manager.
3. From a trusted machine or CI job with network access:

```bash
dotnet ef database update \
  --project src/MovieApp.Infrastructure \
  --startup-project src/MovieApp.Api \
  --connection "$PostgreSql__ConnectionString"
```

4. Verify schema (see [Verification](#verification)).
5. Deploy the API container with the same connection settings via environment variables.
6. Smoke test: `GET /health/ready` → `200`.

### Recommended production connection string parameters

Append to the managed provider connection string (example — adjust for your host):

```text
SSL Mode=Require;Trust Server Certificate=false;Pooling=true;Maximum Pool Size=50;Command Timeout=30
```

Npgsql retry (`EnableRetryOnFailure`) is **not** configured today — add in a future hardening step (29E-3+).

---

## Development vs production isolation

| Aspect | Development | Production |
|--------|-------------|------------|
| PostgreSQL | Local Docker Compose (`movieapp-postgres`) or `localhost:5432` | Managed PostgreSQL endpoint via env/secret |
| Redis | Local Docker Compose (`movieapp-redis`) or optional | Managed Redis via `Redis__ConnectionString` |
| Secrets | User Secrets + local `.env` (gitignored) | Platform secret manager only |
| TMDB | User Secrets (`MovieProviders:Tmdb:ReadAccessToken`) | Env var at deploy time |
| Data volume | `movieapp-postgres-data`, `movieapp-redis-data` | Provider-managed — separate from dev |
| Migrations | Applied manually or by integration tests | Controlled deploy step only |

**Rule:** Development and production databases must remain physically and logically separate. Do not pg_dump dev → prod.

Local workflow (`dotnet run`, User Secrets, TMDB token) is unchanged by this runbook.

---

## Fake provider production risk

`appsettings.json` defaults to `MovieProviders:Provider = Fake`.

Production **must** override:

```text
MovieProviders__Provider=Tmdb
MovieProviders__Tmdb__ReadAccessToken=<secret>
```

There is **no** startup guard that rejects `Fake` in Production today — this remains a **BLOCKER** for launch (see `docs/PRODUCTION_READINESS_AUDIT.md` §18). Catalog data is lazy-ingested from the configured provider; an empty production DB with `Tmdb` is valid at startup.

---

## Redis persistence expectations

| Question | Answer |
|----------|--------|
| Role | **Disposable cache** — search/home/recommendation/discovery TTL caches |
| Business data | **No** — user data lives in PostgreSQL |
| Required for startup | **No** — API starts without Redis (in-memory fallback) |
| Required for readiness | **Only when** `Redis__ConnectionString` is configured — then `/health/ready` checks Redis |
| Required for multi-instance | **Yes** — without shared Redis, each instance has its own memory cache |
| Backup | **Not required** for V1 — cache repopulates on demand |
| AOF/RDB on local Compose | Enabled for dev durability of cache only — not a production backup strategy |

**Production recommendation:** Provision managed Redis with AUTH/TLS. Treat outage as **degraded performance** (cache miss → PostgreSQL/TMDB), not data loss. For multiple API replicas, Redis is effectively **required**.

---

## Backup & recovery (PostgreSQL)

### What to back up

| Asset | Backup | Retention (V1 recommendation) |
|-------|--------|--------------------------------|
| Managed PostgreSQL | Provider automatic daily backups | Minimum 7 days; prefer 14–30 days |
| Point-in-time recovery | Enable if provider supports it | Match provider default |
| Redis cache | None required | N/A |
| Application secrets | Secret manager versioning | Per platform policy |

### What must never enter source control

- Connection strings with passwords
- `PostgreSql__Password`, `PostgreSql__ConnectionString` with credentials
- `Redis__ConnectionString` with passwords
- JWT signing keys, TMDB tokens, SMTP credentials
- `.env` files with real values
- Database dumps from dev or prod

### Restore procedure (high level)

1. Identify failure scope (API-only vs database corruption vs region outage).
2. **API-only:** roll back container image; database unchanged.
3. **Database:** restore from latest clean backup or PITR snapshot to a **new** instance or restored volume.
4. Verify schema migration history matches deployed API version (`__EFMigrationsHistory` table).
5. Run `GET /health/ready` and authenticated smoke tests.
6. Redis: empty cache is acceptable after restore — no restore required.
7. Document incident; schedule restore drill if first real recovery.

### Restore verification (recommended quarterly)

1. Restore backup to an isolated staging database (not production, not development).
2. Point a staging API instance at the restored DB.
3. Verify `/health/ready`, login, search, favorites.
4. Drop staging resources after verification.

**Do not run destructive restore operations against production without an approved change window.**

---

## Verification

### List pending migrations

```bash
dotnet ef migrations list \
  --project src/MovieApp.Infrastructure \
  --startup-project src/MovieApp.Api \
  --connection "$CONNECTION_STRING"
```

### Check core tables exist (psql)

```sql
SELECT table_name FROM information_schema.tables
WHERE table_schema = 'public'
ORDER BY table_name;
```

Expected tables include: `Movies`, `TvShows`, `Users`, `Favorites`, `Watchlists`, `Ratings`, `Reviews`, `WatchedMovies`, `WatchedEpisodes`, `SearchHistory`, `PasswordResetTokens`, `__EFMigrationsHistory`, and related join/lookup tables.

### Health endpoints

| Endpoint | Behavior |
|----------|----------|
| `GET /health` | Always `200` — process liveness |
| `GET /health/ready` | `200` when PostgreSQL (+ Redis if configured) are reachable |

---

## Local disposable migration test (operators)

Use a **separate** temporary container — do not attach to `movieapp-postgres-data`:

```bash
docker run -d --name movieapp-pg-migrate-test \
  -e POSTGRES_USER=migrate_test \
  -e POSTGRES_PASSWORD=migrate_test_local_only \
  -e POSTGRES_DB=movieapp_migrate_test \
  -p 5433:5432 postgres:17

# wait for readiness, then:
dotnet ef database update \
  --project src/MovieApp.Infrastructure \
  --startup-project src/MovieApp.Api \
  --connection "Host=localhost;Port=5433;Database=movieapp_migrate_test;Username=migrate_test;Password=migrate_test_local_only"

docker rm -f movieapp-pg-migrate-test
```

This verifies migrations apply to a fresh database without touching development volumes.

---

## Related documents

- `docs/PRODUCTION_READINESS_AUDIT.md` — full production audit (Step 29D)
- `README.md` — local Docker Compose and development configuration
- `.env.example` — local development variable template (no secrets)
- `docker-compose.yml` — local PostgreSQL + Redis only
- `docker-compose.api.yml` — optional local API container smoke test

---

*Step 29E-2 — readiness documentation only. No cloud deployment performed.*

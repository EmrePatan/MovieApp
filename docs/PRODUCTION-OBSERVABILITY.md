# Production Observability & Operations

Concise operator guide for MovieApp API production monitoring. No separate APM is assumed — logs and provider dashboards are the primary signals.

## Where to inspect API logs

| Environment | Location |
|-------------|----------|
| **Render** | Web service → **Logs** tab (stdout/stderr drain) |
| **Local / CI** | Console output |

**Production format:** JSON lines via Serilog `CompactJsonFormatter` (`appsettings.Production.json`). Each line is a single JSON object suitable for grep and log drains.

**Correlation:** Requests include `CorrelationId` (header `X-Correlation-Id`, Serilog property `CorrelationId`). Unhandled API errors log `TraceId` (ASP.NET `HttpContext.TraceIdentifier`). Match client-reported IDs to log lines.

**Do not log or paste:** passwords, JWTs, refresh tokens, email verification/reset tokens, OAuth secrets, API keys, full email bodies, or Expo push tokens.

## Structured log event catalog (key EventIds)

| EventId | Level | Area | Meaning |
|--------:|-------|------|---------|
| 5001 | Error | API | Mapped exception (known failure) |
| 5002 | Error | API | Unhandled exception |
| 5101 | Warning | Health | `/health/ready` unhealthy (check names only in message) |
| 6000 | Info | Jobs | Background job started |
| 6001–6015 | Info | Jobs | Per-job completion summaries |
| 6095 | Error | Jobs | **Terminal** Hangfire failure (retries exhausted) |
| 6097 | Warning | Jobs | Single job attempt failed (Hangfire will retry if configured) |
| 6001–6004 | Warning | Redis | Cache/rate-limit degradation |
| 7001–7003 | Warning/Error | TMDB | Request failure, retry, terminal failure (path logged without query/api_key) |
| 2201–2212 | Info/Warning | Resend | Email delivery accepted/failed (token **Id** only, not raw token) |
| 7301–7302 | Warning | Expo push | HTTP-level send/receipt failure (status + batch size, no tokens) |

Hangfire internal logs: `Hangfire` namespace at Information in production.

## Unhandled API errors

- **Handler:** `GlobalExceptionHandler` — all unhandled exceptions are logged once (5001/5002) with `TraceId`.
- **Client response:** RFC 7807 Problem Details. Production hides stack traces and internal exception text (`ProductionObservabilityApiTests`).
- **Request logging:** Serilog HTTP request logging logs 5xx at Error; aborted requests are Debug.

## Background jobs (Hangfire)

**Dashboard:** Not exposed publicly (intentional). Inspect failures via logs and PostgreSQL `hangfire` schema.

### Recurring jobs (when enabled)

| Job ID | Schedule | Retries (`AutomaticRetry`) |
|--------|----------|----------------------------|
| `tmdb-tv-changes` | Every 6h | 3 |
| `tmdb-movie-changes` | Every 6h | 3 |
| `hot-release` | Hourly | 1 |
| `movie-release` | Hourly | 1 |
| `release-fanout` | Every 5 min | 0 |
| `push-preparation` | Every 5 min | 0 |
| `push-dispatch` | Every 5 min | 0 |
| `push-receipts` | Every 15 min | 0 |
| `catalog-keyword-backfill` | Config cron | 0 |
| `tv-upcoming-episode-sync` | Hourly | 1 |
| `notification-inbox-cleanup` | Daily | 0 |
| `hot-this-week-trending` | Every 6h | 3 |
| Email verification / password reset delivery | On enqueue | 5 |

### Detecting terminal failures

1. **Logs:** Search for EventId **6095** — `Background job reached terminal failure`.
2. **Retries:** EventId **6097** (Warning) = attempt failed; Hangfire may still retry.
3. **Database (ops with DB access):**
   ```sql
   SELECT id, jobid, invocationdata, statename, createdat
   FROM hangfire.job
   WHERE statename = 'Failed'
   ORDER BY createdat DESC
   LIMIT 20;
   ```

**Render Free note:** sleeping instances may miss cron triggers. Absence of completion logs (6001–6015) after expected schedule is a signal.

## Health endpoints

| Endpoint | Use | Public? |
|----------|-----|---------|
| `GET /health/live` | **Render liveness probe** | Yes |
| `GET /health` | Alias of liveness | Yes |
| `GET /health/ready` | Post-deploy / ops smoke (PostgreSQL, migrations, Redis) | Yes, but descriptions redacted when unhealthy |

**Render:** Health check path = `/health/live` only. Do **not** point Render at `/health/ready` — dependency blips would restart the service.

Unhealthy readiness logs EventId **5101** with failed check **names** only (no connection strings).

See also: `docs/PRODUCTION-SECURITY.md`, `docs/PRODUCTION_DATABASE_RUNBOOK.md`.

## External dependencies — failure signals

| Dependency | In-app signals | Operator action |
|------------|----------------|-----------------|
| **PostgreSQL** | 5002 errors, `/health/ready` `postgresql` / `database-migrations` unhealthy | Render Postgres metrics, connection limits, slow queries |
| **Redis** | 6001–6004 warnings, readiness `redis` unhealthy, rate limit fallback | Render Redis memory/connections |
| **TMDB** | 7001–7003, job summaries with `failed=` counts | TMDB dashboard / rate limits (40 req/10s per IP typical) |
| **Resend** | 2201–2212 | Resend dashboard → Logs, bounce/complaint rates |
| **Expo push** | 7301–7302, job 6007–6008 `retryableFailures` / `permanentFailures` | Expo status page; review device token churn |
| **Google / Apple OAuth** | 2102, 7102–7104 | Provider console for key/credential expiry |

## Cost / usage risk monitoring

Configure alerts in **provider dashboards** (not in application code):

| Provider | What to alert on | Suggested action |
|----------|------------------|------------------|
| **Render** | CPU/memory sustained high, frequent restarts, bandwidth spikes | Scale instance; check job frequency |
| **Render PostgreSQL** | Storage >80%, connection count high | Vacuum/archival; connection pool review |
| **Render Redis** | Memory >80% | Eviction policy; cache TTL review |
| **TMDB** | Approaching rate limit (429 in logs 7001) | Reduce sync frequency; cache hits |
| **Resend** | Daily send volume vs plan | Review retry storms; verify `Enabled` flags |
| **Expo** | Unusual push volume | Check fanout loops; invalid token rate |
| **Gemini (AI recommendations)** | Quota errors in app logs | `AiRecommendation` quota config |

**High-frequency jobs:** push pipeline (5 min) and TMDB changes (6h) dominate background load. Sudden log spikes in 6007–6008 or 7003 warrant investigation.

## Common failure signals & first response

| Symptom | Likely cause | First steps |
|---------|--------------|-------------|
| Render health check failing | Process crash / deploy | Logs for startup exception; verify `/health/live` |
| 503 on `/health/ready` | DB, Redis, or pending migration | EventId 5101; run migration; verify env vars |
| Spike in 5002 | Unhandled bug or dependency down | TraceId from client; check PG/Redis status |
| 6095 terminal job errors | TMDB outage, DB lock, bad data | Read exception in 6095 log; check `hangfire.job` |
| 7003 TMDB terminal failure | API key invalid or sustained 5xx | Verify `Tmdb:ApiKey`; TMDB status |
| 2212 Resend failures | API key, domain, quota | Resend dashboard; `docs/PRODUCTION-EMAIL.md` |
| 7301 Expo HTTP failures | Expo outage or network | status.expo.dev; retry via push jobs |
| No job completion logs | Render sleep / `BackgroundJobs:Enabled=false` | Instance plan; config section |

## Manual external configuration still required

- [ ] Render health check path = `/health/live`
- [ ] Render log retention / export (if needed for compliance)
- [ ] Render PostgreSQL storage and connection alerts
- [ ] Render Redis memory alert
- [ ] Resend domain verification + send volume alerts (`docs/PRODUCTION-EMAIL.md`)
- [ ] TMDB API key rotation procedure documented in secret manager
- [ ] Optional: external uptime monitor (e.g. UptimeRobot free tier) pinging `GET /health/live` every 5 min
- [ ] Optional: log-based alert on Render when `EventId` 6095 or 5002 rate exceeds threshold (Render log alerts if available on plan)

## Related docs

- `docs/PRODUCTION-LAUNCH-CHECKLIST.md` — launch verification
- `docs/PRODUCTION-SECURITY.md` — health exposure policy
- `docs/PRODUCTION-EMAIL.md` — Resend setup
- `docs/PRODUCTION_DATABASE_RUNBOOK.md` — DB backup and readiness

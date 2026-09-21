# Movie Cave — Production Runbook

Practical operator guide for MovieApp API + mobile release. **Names and purposes only — never paste secret values into docs or tickets.**

Related deep dives:

- `docs/PRODUCTION_DATABASE_RUNBOOK.md` — PostgreSQL, Redis, backup/restore
- `docs/PRODUCTION-EMAIL.md` — Resend, bridge URLs, DNS
- `docs/PRODUCTION-OBSERVABILITY.md` — logs, Hangfire, health, incident signals
- `docs/PRODUCTION-SECURITY.md` — auth, rate limits, CORS, probes
- `docs/PRODUCTION-LAUNCH-CHECKLIST.md` — launch checklist (update when infra changes)
- `MovieApp.Mobile/docs/RELEASE_CHECKLIST.md` — mobile store preflight

---

## Architecture (operator mental model)

| Component | Role | Durable? |
|-----------|------|----------|
| **PostgreSQL** | System of record (users, watch history, metrics, Hangfire) | **Yes** — backup required |
| **Redis** | Cache + distributed rate limits | **No** — safe to lose; repopulates |
| **Render API** | Stateless containers | Redeploy/rollback via image |
| **Resend** | Transactional email | External provider |
| **Expo push** | Mobile notifications | External provider |
| **TMDB** | Catalog ingestion | External provider |
| **Cloudflare Pages** | `moviecaveapp.com` static site (privacy, auth bridges) | Git-deployed |

User data must never depend on Redis or ephemeral container filesystem storage.

---

## Backend deploy (Render)

1. Merge to `master`; Render auto-deploys from connected repo (or trigger manual deploy).
2. Confirm deploy SHA matches intended release.
3. **Before or immediately after deploy:** apply pending EF migrations (see [Database migrations](#database-migrations)).
4. Post-deploy smoke:
   - `GET /health/live` → `200`
   - `GET /health/ready` → `200` (checks PostgreSQL, migration currency, Redis)
5. Spot-check logs (JSON) for startup errors and EventId **5101** (unhealthy readiness).

**Rollback (API only):** redeploy previous known-good image/SHA on Render. Database is unchanged.

**Do not** roll back the database to match an older API unless executing an approved recovery procedure.

---

## Mobile release flow (no build in this doc)

Repository: `MovieApp.Mobile` (separate git repo).

1. Confirm `master` SHA, bump `ios.buildNumber` (and Android `versionCode` when needed) in `app.config.ts`.
2. Verify EAS production env vars in Expo dashboard (see [Environment variables](#environment-variables-by-name)).
3. Run local gate: `npm run typecheck`, `npm run config:validate`, `npm run validate:production`.
4. Build: `eas build --platform ios --profile production` (operator action — not automatic).
5. Submit to TestFlight: `eas submit --platform ios --profile production`.
6. Execute Final RC device matrix (see `docs/PRODUCTION-LAUNCH-CHECKLIST.md` / release gate report).

**Credentials:** `eas.json` uses `credentialsSource: "remote"` for all profiles. Do not regenerate signing assets unless rotating deliberately.

**Current versioning baseline:** marketing `1.0.0`, iOS `buildNumber` in `app.config.ts`, Android `versionCode` in `app.config.ts`.

---

## Environment variables by name

Set on Render (API) or Expo dashboard (mobile). Values live in secret managers only.

### PostgreSQL

| Name | Purpose |
|------|---------|
| `PostgreSql__ConnectionString` | Full Npgsql connection string (preferred in production) |
| `PostgreSql__Host` / `Port` / `Database` / `Username` / `Password` | Component-based alternative |

Production rejects localhost PostgreSQL. Use `SSL Mode=Require` (workflow auto-upgrades weak SSL modes).

### Redis

| Name | Purpose |
|------|---------|
| `Redis__ConnectionString` | StackExchange.Redis connection (required in production) |
| `Redis__InstanceName` | Key prefix (default `MovieApp:`) |

Empty Redis config **prevents production startup**. Cache loss is non-destructive; rate limits fail closed (429) when Redis is unavailable.

### Auth / OAuth

| Name | Purpose |
|------|---------|
| `Authentication__Jwt__SigningKey` | JWT signing secret |
| `Authentication__Social__Google__ClientIds__*` | Allowed Google OAuth client IDs |
| `Authentication__Social__Apple__ClientIds__0` | Apple Sign In bundle/service ID |

### Email (Resend)

| Name | Purpose |
|------|---------|
| `Authentication__Email__Resend__ApiKey` | Resend API key |
| `Authentication__Email__Resend__FromAddress` | Sender (`noreply@moviecaveapp.com`) |
| `Authentication__Email__Resend__FromName` | Display name (`Movie Cave`) |
| `Authentication__PasswordReset__EmailProvider` | Must be `Resend` in production |
| `Authentication__PasswordReset__BaseUrl` | HTTPS bridge for reset |
| `Authentication__EmailVerification__BaseUrl` | HTTPS bridge for verify |

### Catalog / AI / App

| Name | Purpose |
|------|---------|
| `MovieProviders__Provider` | Must be `Tmdb` in production (not `Fake`) |
| `MovieProviders__Tmdb__ReadAccessToken` | TMDB bearer token |
| `AiRecommendations__Gemini__ApiKey` | Gemini for AI recommendations |
| `App__PublicBaseUrl` | Public API base URL |
| `AllowedHosts` | Hostname allow-list |

### Mobile (Expo `EXPO_PUBLIC_*`)

| Name | Purpose |
|------|---------|
| `EXPO_PUBLIC_APP_ENV` | `production` for store builds |
| `EXPO_PUBLIC_API_URL` | HTTPS production API URL |
| `EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID` | Google Sign-In (web client) |
| `EXPO_PUBLIC_GOOGLE_IOS_CLIENT_ID` | Google Sign-In (iOS) |
| `EXPO_PUBLIC_IMAGE_BASE_URL` | TMDB image CDN base (recommended) |
| `EXPO_PUBLIC_EAS_PROJECT_ID` | EAS project linkage |

---

## Database migrations

**Not applied at API startup.** Multiple instances must not race `Database.Migrate()` at boot.

### Apply to production

GitHub Actions → **Production Database Migrate** → confirm input `production`.

Workflow: `.github/workflows/production-database-migrate.yml`

- Uses secret `MOVIEAPP_PRODUCTION_POSTGRES_CONNECTION`
- Concurrency group prevents parallel runs
- Runs connectivity diagnostics, then `dotnet ef database update`

### Before every release

1. Provider backup/snapshot (Render Postgres dashboard).
2. List migrations on release SHA:
   ```bash
   dotnet ef migrations list \
     --project src/MovieApp.Infrastructure \
     --startup-project src/MovieApp.Api
   ```
3. Compare `__EFMigrationsHistory` on production (read-only query).
4. Apply pending migrations via workflow.
5. Verify `/health/ready` (includes `database-migrations` check).

### Failure behavior

- Migration workflow failure → production DB unchanged (transactional EF migrations).
- API with pending migrations → `/health/ready` unhealthy (EventId **5101**).
- Render liveness probe uses `/health/live` only — service stays up but ops should block traffic promotion.

### Rollback strategy

| Scenario | Action |
|----------|--------|
| Bad API deploy | Roll back Render to previous image |
| Bad migration already applied | **Forward fix** preferred (new migration). DB down-migration is not automated. |
| Corrupt database | Restore PostgreSQL from backup/PITR to new instance; repoint API |

**Compatibility:** rolling API backward after a forward-only schema migration may break the old binary. Coordinate app + API versions.

---

## PostgreSQL backup / restore

See `docs/PRODUCTION_DATABASE_RUNBOOK.md`.

**Operator expectations (Render — verify in dashboard):**

- [ ] Automatic daily backups enabled
- [ ] Retention ≥ 7 days (prefer 14–30)
- [ ] PITR enabled if available
- [ ] Quarterly restore drill to isolated staging DB

**Restore (high level):** restore to isolated instance → verify schema history → point staging API → smoke test → never destructive restore on prod without change window.

---

## Redis / cache recovery

- No backup required for V1.
- After Redis restart/outage: caches repopulate on demand; expect elevated PostgreSQL/TMDB load briefly.
- Multi-instance API **requires** shared Redis for coherent rate limits and cache.

---

## Health endpoints

| Endpoint | Use |
|----------|-----|
| `GET /health/live` | Render liveness probe (always prefer this for platform health checks) |
| `GET /health` | Alias of live |
| `GET /health/ready` | Post-deploy ops smoke (PostgreSQL + migrations + Redis) |

Unhealthy readiness logs EventId **5101** with check names only.

---

## Monitoring / logging

- **Render → Logs:** JSON stdout (Serilog CompactJsonFormatter).
- **Correlation:** header `X-Correlation-Id`; unhandled errors include `TraceId`.
- **Hangfire:** not public; failures via logs EventId **6095** (terminal) / **6097** (retry).
- **Email:** Resend EventIds **2201–2212**; bounces in Resend dashboard only.
- **Push:** Expo EventIds **7301–7302**; job summaries **6007–6008**.

Full catalog: `docs/PRODUCTION-OBSERVABILITY.md`.

### Temporary #52 watch-history perf logging

`WatchHistoryPerf` events (`TvShowHydration`, `TvShowProgress`) log at **Debug** for namespace `MovieApp.Application`. Production default is Information — they are **suppressed** unless overridden.

**Enable (diagnosis only):**

1. Render → API service → Environment → add:
   - `Serilog__MinimumLevel__Override__MovieApp.Application` = `Debug`
2. Redeploy or restart service.
3. Reproduce issue; grep logs for `WatchHistoryPerf`.
4. **Remove override immediately after diagnosis** and redeploy.

Do not leave Debug override in production long term (noise + cost).

---

## Failed Hangfire jobs

1. Search logs for EventId **6095**.
2. Optional DB (ops access):
   ```sql
   SELECT id, jobid, statename, createdat
   FROM hangfire.job
   WHERE statename = 'Failed'
   ORDER BY createdat DESC
   LIMIT 20;
   ```
3. Fix root cause (TMDB, Resend, push tokens, etc.); re-enqueue or wait for next schedule as appropriate.

Recurring job schedule reference: `docs/PRODUCTION-OBSERVABILITY.md`.

---

## Resend / email operations

Production sender target: `Movie Cave <noreply@moviecaveapp.com>`.

Bridge pages (open app via `movieapp://`):

- `https://moviecaveapp.com/auth/verify-email`
- `https://moviecaveapp.com/auth/reset-password`

**Manual verification before major release:**

1. Resend dashboard → domain `moviecaveapp.com` still **Verified**.
2. DNS SPF/DKIM/DMARC unchanged since last verification.
3. Render env uses production sender (not `onboarding@resend.dev`).
4. Send one verification + one password-reset email to operator inbox; confirm sender, links, and app open behavior.
5. Check Resend → Logs for bounces.

Details: `docs/PRODUCTION-EMAIL.md`.

---

## OAuth operations

| Provider | Check |
|----------|-------|
| **Google** | OAuth client IDs match mobile bundle IDs; consent screen valid |
| **Apple** | Sign in with Apple enabled for `com.movieapp.mobile`; keys not expired |

Log signals: EventIds **2102**, **7102–7104** (see observability doc).

---

## Push notification operations

- Mobile uses `expo-notifications`; tokens stored server-side.
- Pipeline jobs: `push-preparation`, `push-dispatch`, `push-receipts` (every 5–15 min).
- Monitor **6007–6008** job summaries and **7301–7302** for HTTP failures.
- Invalid tokens are pruned over time; spikes may follow app releases.

---

## Deep links

| Mechanism | Value |
|-----------|-------|
| Custom scheme | `movieapp://` |
| In-app routes | `/movie/{id}`, `/tv/{id}`, auth callbacks under `/auth/...` |
| Email bridges | HTTPS pages on `moviecaveapp.com` → scheme links |

Universal Links / Associated Domains are **not** configured yet (custom scheme only).

**Smoke:** cold-start app from email bridge, push notification tap, and manual `movieapp://movie/{id}`.

---

## Anonymous product metrics (#56)

**Storage:** PostgreSQL table `product_metric_daily` (`date`, `metric_name`, `count`, `updated_at_utc`).

**Ingestion:** mobile `POST /api/product-metrics/increment` (allowlisted names only; no user identity).

**Allowlisted metrics (Final RC smoke):**

- `ai_recommendations_opened`, `ai_recommendations_generated`
- `pick_something_opened`, `pick_something_generated`
- `search_submitted`
- `watchlist_created`, `review_created`, `rating_created`

Legacy keys `pick_something_used`, `ai_recommendations_used` still accepted.

### Example SQL (read-only)

Daily totals for today (UTC date):

```sql
SELECT metric_name, count
FROM product_metric_daily
WHERE date = CURRENT_DATE
  AND metric_name IN (
    'ai_recommendations_opened',
    'ai_recommendations_generated',
    'pick_something_opened',
    'pick_something_generated',
    'search_submitted',
    'watchlist_created',
    'review_created',
    'rating_created'
  )
ORDER BY metric_name;
```

Lifetime aggregates:

```sql
SELECT metric_name, SUM(count) AS total
FROM product_metric_daily
WHERE metric_name IN (
  'ai_recommendations_opened',
  'ai_recommendations_generated',
  'pick_something_opened',
  'pick_something_generated',
  'search_submitted',
  'watchlist_created',
  'review_created',
  'rating_created'
)
GROUP BY metric_name
ORDER BY metric_name;
```

After Final RC device testing, compare counts before/after each smoke action (allow ~1 minute for upsert visibility).

---

## Incident triage (first 15 minutes)

1. **User reports outage** → check Render service status + `/health/live`.
2. **Degraded features** → `/health/ready` + Redis/PostgreSQL metrics in Render.
3. **5xx spike** → grep logs for **5002**, note `TraceId`.
4. **Email not arriving** → Resend dashboard + Hangfire **6095** for delivery jobs.
5. **Auth failures** → JWT key rotation? OAuth provider status?
6. **Search slow** → Redis up? TMDB **7001–7003** rate limits?

Escalation data to capture: time (UTC), deploy SHA, correlation/trace IDs, affected endpoints.

---

## Final release checklist (condensed)

### Backend

- [ ] Backup/snapshot taken
- [ ] Migrations applied (workflow + `/health/ready`)
- [ ] `MovieProviders__Provider=Tmdb` confirmed on Render
- [ ] Resend sender = `noreply@moviecaveapp.com`
- [ ] No temporary Debug logging overrides
- [ ] Smoke: login, search, watch history, email job enqueue

### Mobile (Final RC)

- [ ] `ios.buildNumber` incremented from last shipped TestFlight build
- [ ] EAS production env vars set (HTTPS API URL)
- [ ] `validate:production` passes
- [ ] EAS build + TestFlight upload
- [ ] Execute full device matrix on installed Final RC

### Post-deploy smoke (API)

- [ ] `GET /health/live` and `/health/ready`
- [ ] Register/login (password + Apple/Google if enabled)
- [ ] Search + detail + watchlist mutation
- [ ] Push notification to test device
- [ ] Product metric increment visible in SQL

---

## Secrets — never commit

Connection strings, JWT keys, TMDB tokens, Resend keys, Gemini keys, OAuth secrets, `.env` with real values, database dumps.

If leaked: rotate in provider dashboard, update Render/Expo env, redeploy.

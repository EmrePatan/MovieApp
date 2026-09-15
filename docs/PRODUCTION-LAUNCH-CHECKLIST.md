# MovieApp — Production Launch Checklist

**Baseline release SHA (when this document was created):** `62963819ca6ff400a5cf5ba6756b22f36f520d57`

**Maintenance rule:** Update this checklist whenever a change introduces an EF migration, recurring/background job, production secret/config, external provider dependency, production datastore, notification delivery change, deployment workflow change, or store-release requirement. Do not let launch knowledge live only in chat history.

---

## Environment separation (non-negotiable)

Staging and production are **separate environments**.

Production must have its own:

- API service
- PostgreSQL database
- Redis instance
- environment variables / secrets
- deployment configuration

Do **not** assume:

- staging PostgreSQL data will be copied to production
- staging keyword coverage exists in production
- staging credentials are safe to reuse in production

Record resource identifiers here (names/URLs only — **never secrets**):

| Resource | Staging (example) | Production (fill in) |
|---|---|---|
| API URL | `https://movieapp-fpkg.onrender.com` | |
| PostgreSQL instance | | |
| Redis instance | | |
| API service name | | |
| Region | | |

---

## 1. Pre-production infrastructure

- [ ] Production API service created
- [ ] Production PostgreSQL created
- [ ] Production Redis created
- [ ] Region selected intentionally (latency, provider limits, data residency)
- [ ] Production API connected to **production** PostgreSQL (`PostgreSql__ConnectionString` or host/user/db/password)
- [ ] Production API connected to **production** Redis (`Redis__ConnectionString`)
- [ ] Production API does **not** reference staging PostgreSQL, Redis, or URLs
- [ ] Production HTTPS URL confirmed
- [ ] `GET /health` returns 200 (liveness)
- [ ] `GET /health/ready` returns 200 (PostgreSQL + Redis readiness when configured)

---

## 2. Database / EF migrations

Migrations are **not** applied automatically at API startup. Apply them deliberately per release.

### Before every production release

- [ ] Production database backup/snapshot taken (where provider supports it)
- [ ] Production connection verified (operator workflow points to **production**, not staging)
- [ ] Release candidate SHA frozen and recorded
- [ ] `dotnet ef migrations has-pending-model-changes` is clean on the release SHA
- [ ] All EF migrations through the release SHA applied to production
- [ ] `__EFMigrationsHistory` reviewed on production
- [ ] API starts successfully after migrations
- [ ] No manual `CREATE TABLE` / `ALTER TABLE` used as a substitute for EF migrations

### Migration process (pattern)

Project: `src/MovieApp.Infrastructure`  
Startup project: `src/MovieApp.Api`

```bash
dotnet ef database update \
  --project src/MovieApp.Infrastructure \
  --startup-project src/MovieApp.Api \
  --configuration Release \
  --connection "<PRODUCTION_CONNECTION_STRING>"
```

**Staging workflow reference:** `.github/workflows/staging-database-migrate.yml` (uses secret `MOVIEAPP_STAGING_POSTGRES_CONNECTION`). Create an equivalent **production** workflow or runbook before first production deploy — do not point staging workflows at production.

### Launch-critical example: keyword schema

Migration `20260915095315_AddCatalogKeywords` adds:

- `keywords`, `movie_keywords`, `tv_show_keywords`
- `movies.KeywordsSyncedAtUtc`, `tv_shows.KeywordsSyncedAtUtc`

Verify on production:

```sql
SELECT "MigrationId"
FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260915095315_AddCatalogKeywords';
```

**All migrations up to the release SHA must be applied**, not only this one.

### Launch-critical example: regional release schema

Migration `20260915110524_AddMovieRegionalReleases` adds:

- `movie_regional_releases` (`MovieId` + `Region` composite PK)
- `EffectiveReleaseDate`, `EffectiveReleaseType`, `Certification`, `IsFallbackGlobal`, `SyncedAtUtc`
- index `IX_movie_regional_releases_Region_EffectiveReleaseDate`

Verify on production:

```sql
SELECT "MigrationId"
FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260915110524_AddMovieRegionalReleases';
```

---

## 3. Required secrets / configuration

Use `[ ] configured` / `[ ] validated` — **never record actual values in this document.**

### Credential rotation reminder

Any credential exposed during development or staging must **not** be reused in production. Rotate before launch.

| Category | Config section / env var pattern | Configured | Validated |
|---|---|:---:|:---:|
| PostgreSQL | `PostgreSql:ConnectionString` → `PostgreSql__ConnectionString` (preferred), or `PostgreSql__Host` / `Port` / `Database` / `Username` / `Password` | [ ] | [ ] |
| Redis | `Redis:ConnectionString` → `Redis__ConnectionString`; `Redis:InstanceName` → `Redis__InstanceName` | [ ] | [ ] |
| JWT | `Authentication:Jwt:SigningKey` → `Authentication__Jwt__SigningKey`; also `Issuer`, `Audience`, `AccessTokenMinutes` | [ ] | [ ] |
| TMDB | `MovieProviders:Provider` = `Tmdb`; `MovieProviders:Tmdb:ApiKey` / `ReadAccessToken` / `BaseUrl` | [ ] | [ ] |
| Release region | `ReleaseRegion:DefaultRegion` → `ReleaseRegion__DefaultRegion` (non-secret; default `TR`) | [ ] | [ ] |
| Push notifications | `PushNotifications:Enabled` → `PushNotifications__Enabled`; `MaxAttempts`, `DispatchBatchSize` | [ ] | [ ] |
| Public URL | `App:PublicBaseUrl` → `App__PublicBaseUrl` | [ ] | [ ] |
| CORS | `Cors:Enabled`, `Cors:AllowedOrigins` → `Cors__Enabled`, `Cors__AllowedOrigins` | [ ] | [ ] |
| Forwarded headers | `ForwardedHeaders:Enabled` (if behind reverse proxy) | [ ] | [ ] |
| ASP.NET environment | `ASPNETCORE_ENVIRONMENT` (typically `Production`) | [ ] | [ ] |

### Background jobs master switch

| Setting | Default in `appsettings.json` | Notes |
|---|---|---|
| `BackgroundJobs:Enabled` → `BackgroundJobs__Enabled` | `false` | **Must be `true` for Hangfire.** When `false`, no recurring jobs run and `ICatalogKeywordBackfillJobEnqueuer` is not registered. |
| `BackgroundJobs:TmdbChangesEnabled` | `true` | |
| `BackgroundJobs:HotReleaseEnabled` | `true` | |
| `BackgroundJobs:MovieReleaseEnabled` | `true` (class default) | |
| `BackgroundJobs:NotificationFanoutEnabled` | `true` | |
| `BackgroundJobs:PushDeliveryEnabled` | `true` | Requires `PushNotifications:Enabled` for push jobs |

---

## 4. Hangfire / recurring jobs

Hangfire storage: PostgreSQL schema `hangfire`.  
Registration: `HangfireRecurringBackgroundJobRegistrar` (only when `BackgroundJobs:Enabled = true`).  
**Hangfire dashboard is not mapped** in `ApplicationBootstrap` today — decide intentionally before exposing one.

Master gate: `BackgroundJobs:Enabled`. If false, **all** recurring jobs are removed.

| Job class | Recurring job ID | Cadence (UTC) | Registration gate | Purpose |
|---|---|---|---|---|
| `TmdbTvChangesSyncJob` | `movieapp:tmdb-tv-changes` | Every 6 hours | `BackgroundJobs:TmdbChangesEnabled` | TMDB TV changes discovery / catalog refresh signal |
| `HotReleaseCheckJob` | `movieapp:hot-release` | Hourly | `BackgroundJobs:HotReleaseEnabled` | Imminent/airing TV release boundary checks |
| `MovieReleaseCheckJob` | `movieapp:movie-release` | Hourly | `BackgroundJobs:MovieReleaseEnabled` | Movie release detection |
| `ReleaseNotificationFanoutJob` | `movieapp:release-fanout` | Every 5 minutes | `BackgroundJobs:NotificationFanoutEnabled` | Fan out release events to user notifications |
| `PushDeliveryPreparationJob` | `movieapp:push-preparation` | Every 5 minutes | `PushDeliveryEnabled` **and** `PushNotifications:Enabled` | Prepare push deliveries |
| `PushDispatchJob` | `movieapp:push-dispatch` | Every 5 minutes | same | Dispatch to Expo |
| `PushReceiptJob` | `movieapp:push-receipts` | Every 15 minutes | same | Process Expo receipts |
| `CatalogKeywordBackfillJob` | `movieapp:catalog-keyword-backfill` | `CatalogKeywordBackfill:RecurringCron` (default `0 * * * *`) | `CatalogKeywordBackfill:Enabled` | Bounded keyword metadata backfill |

Per-job launch checklist:

- [ ] Intentionally enabled or disabled
- [ ] Cadence verified
- [ ] Dependencies configured (PostgreSQL, Redis, TMDB, Expo as applicable)
- [ ] First execution observed in logs / Hangfire storage
- [ ] Failure logs reviewed

**Render Free note:** sleeping instances may miss scheduled runs. Do not assume hourly cron guarantees hourly execution.

---

## 5. Catalog keyword backfill (production)

`CatalogKeywordBackfillJob` is **catalog maintenance**, not a migration. Production starts with its own keyword coverage (typically near zero).

### Prerequisites

- [ ] `20260915095315_AddCatalogKeywords` applied on production
- [ ] TMDB configured (`MovieProviders:Provider = Tmdb`)
- [ ] `BackgroundJobs:Enabled = true` (Hangfire operational)
- [ ] `CatalogKeywordBackfill:Enabled` left **`false`** until deliberate validation

### Coverage definition (same as code)

- **Eligible:** `TmdbId > 0`
- **Synced:** `KeywordsSyncedAtUtc IS NOT NULL`
- **Successful-empty:** TMDB returns zero keywords but sync marker is set — counts as synced, may add zero relationship rows

### Initial conservative config (recommended)

| Setting | Env var | Recommended value |
|---|---|---|
| `CatalogKeywordBackfill:Enabled` | `CatalogKeywordBackfill__Enabled` | `false` until validated |
| `CatalogKeywordBackfill:BatchSize` | `CatalogKeywordBackfill__BatchSize` | `25` (valid range 1–200) |
| `CatalogKeywordBackfill:MaxConcurrency` | `CatalogKeywordBackfill__MaxConcurrency` | `2` (valid range 1–8) |
| `CatalogKeywordBackfill:RecurringCron` | `CatalogKeywordBackfill__RecurringCron` | `0 * * * *` |

### Bounded validation (before enabling recurring)

**Staging gate — CLOSED 2026-09-15** (production checklist items below still apply at first prod deploy):

- [x] BEFORE Movie coverage recorded (eligible / synced / unsynced / %)
- [x] BEFORE TV coverage recorded
- [x] BEFORE Overall coverage recorded
- [x] One bounded execution performed (max 25 titles; no drain loop)
- [x] `selected` / `succeeded` / `failed` / `skipped` reviewed in structured logs (events 6010/6011)
- [x] Provider errors reviewed (transient vs permanent) — none in validated run
- [x] AFTER coverage recorded
- [x] Sample persistence validated (`KeywordsSyncedAtUtc` set; relationships exist OR successful-empty)

**Staging validated run:** `selected=25` `succeeded=25` `failed=0` `skipped=0` · movies +12 / TV +13 · coverage **4.22% → 8.28%** (51 / 616 synced). Concurrency fixes **c295896** (scoped `DbContext`) and **a90f2a5** (`ON CONFLICT` keyword upsert) confirmed on real staging.

Manual trigger: `ICatalogKeywordBackfillJobEnqueuer.EnqueueOneExecution()` (DI/Hangfire only — **no public HTTP endpoint**).

### Controlled staging coverage growth (post-validation, pre–Recommendation 2.1)

Staging only — grow from **8.28%** toward **~25%** (~103 additional titles, ~4× `BatchSize=25` batches). Keep `MaxConcurrency=2`. Run batches **sequentially**; stop on any failure, `DbContext` concurrency error, or `23505` / `IX_keywords_TmdbKeywordId`. Full catalog drain **not** required before production launch rehearsal.

### After bounded validation only

- [ ] Recurring backfill intentionally enabled (`CatalogKeywordBackfill__Enabled=true`)
- [ ] Coverage trend monitored over time
- [ ] Do **not** drain the full catalog during deploy or startup

### Recommendation invariant

Recommendation execution must remain **0 TMDB keyword provider calls**. Backfill improves coverage asynchronously; recommendation cache expires naturally (personalized cache version `v3`).

---

## 6. Recommendation 2.1 production invariants

Verified from `appsettings.json` and `RecommendationAlgorithmVersion`:

| Invariant | Value / behavior |
|---|---|
| Data source | Local persisted keywords only |
| Genre weight (primary semantic) | `Recommendations:PersonalizedGenreWeight` = **0.45** |
| Keyword weight (secondary) | `Recommendations:PersonalizedKeywordWeight` = **0.15** |
| Personalized cache version | **`v3`** (`RecommendationAlgorithmVersion.Personalized`) |
| Provider calls at recommendation time | **0** keyword calls |
| Missing keyword metadata | Neutral (no penalty) |
| Backfill role | Improves coverage asynchronously; does not change scoring formula |

- [ ] Production config preserves accepted weights above (unless intentionally changed in a new release)
- [ ] No recommendation-time keyword provider enrichment introduced

---

## 7. Regional movie release (v1)

`Movie.ReleaseDate` remains TMDB global/primary. Movie Follow / `MovieReleased` / Upcoming movies use configured regional effective dates when successfully synced.

### Prerequisites

- [ ] `20260915110524_AddMovieRegionalReleases` applied on target environment
- [ ] `ReleaseRegion:DefaultRegion` intentionally configured (production default expected `TR`)
- [ ] TMDB connectivity validated for `movie/{id}/release_dates`
- [ ] Do **not** assume staging regional rows exist in production

### Staging / pre-production validation

- [ ] Followed future movie syncs regional metadata via `MovieReleaseCheckJob` (one bounded `release_dates` call per unique movie)
- [ ] Moved-date safety: stale due date does **not** emit early `MovieReleased` without verification refresh
- [ ] Provider failure preserves last successful `movie_regional_releases` row and does not emit unverified events
- [ ] Successful no-TR response persists global fallback (`IsFallbackGlobal=true`) without treating it as TR-confirmed availability
- [ ] Movie Follow eligibility smoke: global past + regional future allowed; global future + regional past/today rejected
- [ ] Upcoming smoke: regional future overrides global past; regional released excluded even if global future
- [ ] Notification semantics validated for configured region (theatrical/digital consumer rule; premiere/limited excluded)
- [ ] Certification strings observed for data quality; UI display remains deferred

### Invariants

- [ ] No per-user / per-follower / recommendation / Upcoming / Discover-card TMDB release-date calls
- [ ] Recommendation 2.1 still uses global `Movie.ReleaseDate` for future filtering (provider-free)
- [ ] Search / Discover / Explore / Home / collections unchanged in v1

---

## 8. Release notification pipeline

Validation chain:

```
catalog refresh / release detection
  → CatalogReleaseEvent
  → user notification fanout
  → delivery preparation
  → Expo push dispatch
  → Expo ticket
  → receipt processing
  → Delivered
```

- [ ] Recurring jobs in §4 running as intended
- [ ] Duplicate/idempotency behavior validated on reprocessing
- [ ] Notification center receives event
- [ ] Push delivery row persisted
- [ ] Expo ticket recorded
- [ ] Receipt reaches `Delivered` status

Physical-device E2E is a separate hard gate (§9).

---

## 9. Physical push E2E — hard launch gate

**Not complete as of this document.** Physical iPhone push E2E must pass before push notifications are declared production-ready.

- [ ] Production or staging-equivalent iOS build installed on **physical device**
- [ ] User logs in
- [ ] Notification permission granted
- [ ] Expo push token registered
- [ ] `PushDevice` active in backend
- [ ] Title followed
- [ ] Controlled release event created/detected
- [ ] Fanout created
- [ ] Delivery created
- [ ] Expo ticket received
- [ ] Physical notification observed on device
- [ ] Expo receipt status = `Delivered`
- [ ] Notification tap routes to owned notification/content safely

---

## 10. Mobile production configuration (separate repo)

Mobile repo: `MovieApp.Mobile` — not modified by backend launch checklist, but must be verified before store release.

- [ ] Production API base URL configured (`EXPO_PUBLIC_API_URL` — no staging URL in production build)
- [ ] EAS production profile configured
- [ ] Bundle / package identifiers correct
- [ ] App version / build number set for release
- [ ] Production Expo project and push credentials configured
- [ ] Release build installed on physical devices

Smoke on physical device:

- [ ] Login / logout
- [ ] Home
- [ ] Search / Explore
- [ ] Discover
- [ ] Movie / TV details
- [ ] Follow
- [ ] Favorites
- [ ] Watchlists
- [ ] Watch History
- [ ] Following
- [ ] Notifications
- [ ] Trailer
- [ ] Person / Cast
- [ ] Collections

---

## 11. TMDB / provider checks

- [ ] Production TMDB credentials configured
- [ ] TMDB connectivity validated (search/detail smoke)
- [ ] Provider failures do not leak secrets in logs or API responses
- [ ] Provider request rates observed during initial keyword backfill
- [ ] Recommendation path remains provider-free for keywords
- [ ] Summary ingestion paths retain intended provider-call budgets (`Search:MaxProviderDetailFetchesPerContentType`, etc.)

**Regional release note:** v1 adds bounded `release_dates` calls from `MovieReleaseCheckJob` only. Verify provider-call budgets after deploy.

---

## 12. Observability

### Currently available

- `GET /health` — liveness (no dependency probe)
- `GET /health/ready` — PostgreSQL + Redis readiness
- Structured Serilog console logging
- Hangfire job structured log events (including keyword backfill 6010/6011)
- Provider/job warning logs in application output

### Required before production

- [ ] Log access path defined (hosting provider dashboard or log drain)
- [ ] On-call / operator knows where to inspect API logs
- [ ] Unexpected 5xx rate monitored manually at minimum during launch window
- [ ] Hangfire / job failure review process defined
- [ ] TMDB provider failure review process defined
- [ ] Keyword coverage review process defined (SQL or internal service — no public coverage endpoint today)
- [ ] Notification fanout failure review process defined
- [ ] Push delivery / Expo receipt failure review process defined

Do not assume dashboards exist unless provisioned.

---

## 13. Security

- [ ] No secrets committed to git
- [ ] Production credentials differ from staging
- [ ] Exposed or old credentials rotated
- [ ] JWT signing key is production-only
- [ ] Database does not use leaked development/staging credentials
- [ ] No anonymous operational/admin backfill trigger exposed
- [ ] CORS / allowed origins reviewed for production mobile origin(s)
- [ ] Swagger/OpenAPI exposure intentionally decided (`ApplicationBootstrap` serves Swagger only in Development)
- [ ] Hangfire dashboard exposure intentionally decided (not mapped today)
- [ ] Logs do not contain connection strings, tokens, or passwords

---

## 14. Staging release rehearsal (before production)

- [ ] Deploy release candidate to staging
- [ ] Apply migrations using real workflow (`Staging Database Migrate` or equivalent)
- [ ] API health passes (`/health`, `/health/ready`)
- [ ] Smoke test critical API flows
- [ ] Recurring jobs observed (intentional set only)
- [x] One keyword backfill batch observed (staging — 2026-09-15: 25/25 succeeded, 4.22% → 8.28%)
- [x] Keyword coverage measured before/after (staging)
- [ ] Notification pipeline observed
- [ ] No unexpected provider-call regression (recommendation = 0 keyword calls)
- [ ] Logs reviewed

---

## 15. Production deployment (ordered)

1. [ ] Freeze release SHA
2. [ ] Verify CI green on release SHA (`ci.yml`: build, unit tests, Docker image)
3. [ ] Create / verify production infrastructure (§1)
4. [ ] Configure production secrets (§3)
5. [ ] Take production DB backup/snapshot
6. [ ] Apply EF migrations to production (§2)
7. [ ] Deploy API image to production
8. [ ] `GET /health` and `GET /health/ready` pass
9. [ ] Verify Redis connectivity (cache + distributed locks behave as expected)
10. [ ] Verify Hangfire (`BackgroundJobs__Enabled=true`)
11. [ ] Validate recurring jobs intentionally enabled/disabled (§4)
12. [ ] Measure keyword baseline coverage on production
13. [ ] Run **one** bounded keyword backfill batch; review results (§5)
14. [ ] Validate notification pipeline (§7)
15. [ ] Mobile production build (§9)
16. [ ] Physical-device smoke (§9)
17. [ ] TestFlight / internal testing
18. [ ] App Store / Google Play submission
19. [ ] Post-release monitoring (§11)

---

## 16. Rollback / incident notes

Know before launch:

| Topic | Reality |
|---|---|
| Previous API release SHA | Record before deploy: `________________` |
| EF migration rollback | EF does not auto-rollback. Prefer forward-fix migration or restore from backup. |
| Database restore | Use provider backup/restore to isolated instance; verify migrations after restore |
| Disable all recurring jobs | `BackgroundJobs__Enabled=false` (removes all recurring registrations) |
| Disable keyword backfill only | `CatalogKeywordBackfill__Enabled=false` |
| Disable push delivery only | `PushNotifications__Enabled=false` or `BackgroundJobs__PushDeliveryEnabled=false` |
| Disable individual jobs | Per-flag: `BackgroundJobs__TmdbChangesEnabled`, `HotReleaseEnabled`, etc. |
| Provider outage | Jobs fail soft per title/batch; keyword backfill retries on later runs |
| Logs | Hosting provider log dashboard (e.g. Render logs) — no separate APM assumed |

There is **no** automatic full-stack rollback. Plan forward fixes and backup restore paths.

---

## 17. Current known blockers / open items

**Status date:** 2026-09-15

These are current launch validation gaps, not permanent architecture.

| Item | Status |
|---|---|
| `CatalogKeywordBackfillJob` code | Implemented at `6296381` |
| Staging keyword backfill execution validation | **DONE / PASS** (2026-09-15 — bounded batch 25/25, `failed=0`) |
| Staging keyword migration `20260915095315_AddCatalogKeywords` | **Manually confirmed present** on staging |
| Staging keyword coverage (measured) | **51 / 616 synced (8.28%)** after validated run · Movies: 25/332 · TV: 26/284 · 565 unsynced |
| Controlled staging coverage growth (~25%) | **In progress / next operator step** |
| Recommendation 2.1 real-data validation | **Next** after ~25% staging coverage |
| Physical iPhone push E2E | **Not complete** |
| PostgreSQL integration test project | Pre-existing analyzer build issues (CA1707, CA1822, etc.) — must be clean before production rehearsal |
| Production migration workflow | Staging workflow exists; **production equivalent must be created** before first prod deploy |
| Hangfire manual keyword trigger | No HTTP endpoint; requires DI enqueuer or recurring enable + log observation |

Update this section as blockers close.

---

## Quick reference: disable switches

```
BackgroundJobs__Enabled=false                    # all Hangfire recurring jobs off
CatalogKeywordBackfill__Enabled=false            # keyword backfill recurring off
PushNotifications__Enabled=false                 # push pipeline off
BackgroundJobs__PushDeliveryEnabled=false        # push jobs off (keep fanout if desired)
BackgroundJobs__NotificationFanoutEnabled=false   # release fanout off
```

---

*End of checklist.*

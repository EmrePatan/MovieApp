# Movie Cave load & capacity tests (#60)

Repeatable [k6](https://k6.io/) tooling to measure **realistic concurrent user behavior** and a separate **read-only request-capacity** profile. This package does **not** prove production capacity until staged runs are executed and server-side metrics are analyzed.

## Architecture assumptions (from backend inspection)

| Area | Current behavior relevant to load testing |
|------|---------------------------------------------|
| Runtime | ASP.NET Core, Serilog request logging with correlation ID + user id |
| Data | EF Core `ApplicationDbContext` (scoped), Npgsql with **retry on failure (3)** |
| Cache | Redis/Valkey when configured; in-memory cache also registered |
| Auth | JWT bearer + **security stamp** validation on requests |
| Rate limits | **Per client IP** for unified/movie/TV search (`Search:RateLimit` defaults: 30/20/20 per minute) |
| Hangfire | Background jobs (releases, external ratings refresh, email) — not driven by this suite |
| Health | `GET /health`, `/health/live`, `/health/ready` (Postgres + Redis checks on ready) |

### Performance risk map (test design inputs)

- **Personalized home** (`/api/home`, `/api/home/personalized`, `/api/recommendations/home`) — user-specific DB + recommendation work.
- **Detail status fan-out** — multiple authorized reads per detail screen (favorites, watchlist membership, ratings/me, watch-history/me, follow).
- **Library** — paginated user library queries.
- **Insights v3** — heavier analytics read path.
- **Discovery lists** — mostly catalog/DB; still watch for payload size and pagination.
- **Unified search** — IP rate limits; keep low share in user-concurrency runs; prefer autocomplete.
- **External ratings** — can enqueue MDBList refresh on cold snapshot; production runs must use **warm snapshot IDs only** (optional low-weight tag `external-ratings-warm`).
- **Connection pool** — default Npgsql pool per Render instance; high VU × fan-out increases concurrent DB usage.
- **Load generator** — single-machine k6 can saturate CPU/sockets before the API; use multiple generators or distributed k6 for high stages.

## Prerequisites

- [k6](https://grafana.com/docs/k6/latest/set-up/install-k6/) **or** Docker (`grafana/k6:latest`)
- Operator-prepared `data/tokens.json` (gitignored) and content ID pools
- **Do not** commit JWTs, passwords, or API keys

## Quick start (smoke — not production)

```powershell
cd tests\load
# Prepare data\tokens.json and data\hot-content.json from the *.example.json templates

$env:LOAD_TEST_BASE_URL = "https://your-api-host"
$env:LOAD_TEST_TOKENS_FILE = "$PWD\data\tokens.json"

.\scripts\Invoke-K6.ps1 -Scenario user-concurrency -Preset smoke -StageTarget 5 -UseDocker
.\scripts\Summarize-Report.ps1 -ReportPath .\reports\<latest>.json
```

Validate tooling only (no real API required for parse smoke):

```powershell
.\scripts\Validate-LoadTests.ps1 -RunK6ParseSmoke
```

## Scenarios

| File | Purpose |
|------|---------|
| `scenarios/user-concurrency.js` | Realistic users with think time; weighted journeys |
| `scenarios/request-capacity.js` | Read-only saturation (health + discovery + catalog GETs) |
| `scenarios/benchmark-tv-bulk-watch.js` | **Staging only** — TV bulk episode mutation worst case |

## Traffic model — session weights (each iteration)

| Session | Weight | Representative endpoints |
|---------|--------|---------------------------|
| `home_feed` | 32% | `GET /api/home`, `GET /api/home/personalized`, sometimes `GET /api/recommendations/home` |
| `detail_open` | 28% | `GET /api/movies|tvshows/{id}` + status fan-out |
| `discover_browse` | 14% | `GET /api/discovery/trending|popular|explore-preview` |
| `search` | 6% | Mostly `GET /api/search/autocomplete`; rare unified `GET /api/search` |
| `library` | 6% | `GET /api/library?...` |
| `insights` | 3% | `GET /api/insights/v3` |
| `reviews_surface` | 5% | `GET /api/reviews/movies/{id}` |
| `home_split` | 6% | `GET /api/home/browse` + `GET /api/home/personalized` |

Detail fan-out (authorized) uses: favorites status, watchlist membership, ratings/me, watch-history/me (movies), follow, optional credits/reviews/ratings summary.

## Think-time model

Random uniform sleeps (seconds):

| Helper | Range | Use |
|--------|-------|-----|
| `thinkScrollFeed` | 3–12 | Home / discover scrolling |
| `thinkDetailPage` | 5–25 | Reading detail |
| `thinkShortGlance` | 1–4 | Between parallel UI calls |
| `thinkSearchTyping` | 2–8 | Before search |
| `thinkLibraryBrowse` | 4–15 | Library |
| `thinkInsights` | 6–20 | Insights |
| `thinkBetweenIterations` | 2–6 | Between journey cycles |

## Content datasets

| Dataset | Env | Purpose |
|---------|-----|---------|
| **HOT** | `LOAD_TEST_CONTENT_DATASET=hot` | Small pool — steady-state cache behavior |
| **VARIED** | `LOAD_TEST_CONTENT_DATASET=varied` | Large pool — reduced hot-key skew |

Configure `data/hot-content.json` and `data/varied-content.json` (from examples). Include `externalRatingsWarm*` IDs only when `LOAD_TEST_INCLUDE_EXTERNAL_RATINGS=true`.

## Auth / identities

- Pre-generated bearer tokens in `data/tokens.json` or `LOAD_TEST_TOKEN_001`… env vars.
- VUs round-robin across the pool: `identity = identities[(vu-1) % N]`.
- **Recommendation:** at least **50–100** dedicated load-test users for 1,000 VUs (≈10–20 VUs per identity) to avoid unrealistic personalization cache contention. More identities are better if preparation cost is acceptable.

## Write traffic

Production **user-concurrency** scenario is **read-only**. Mutations are isolated to `benchmark-tv-bulk-watch.js` (staging, explicit env guard).

## Third-party protection

| Dependency | Protection |
|------------|------------|
| TMDB | Only use ingested catalog IDs (no random TMDB ids / search that triggers ingestion) |
| MDBList | External ratings only with warm snapshot IDs; off by default |
| Azure translation | No review translation endpoints in mix |
| Email / push | No auth register/login; no notification fan-out endpoints |

## Staged concurrency (manual gates)

Run **separate** k6 executions per target VU: **50 → 100 → 250 → 500 → 750 → 1000**, with cooldown and inspection between stages. See [RUNBOOK.md](./RUNBOOK.md).

```powershell
.\scripts\Invoke-K6.ps1 -Scenario user-concurrency -Preset capacity -StageTarget 50 -UseDocker
# inspect metrics + Render/Postgres/Redis — cooldown — repeat
```

Presets in `config/presets.json`: `smoke` vs `capacity` (12m hold; 15m hold for stage 1000).

## Metrics & reports

k6 collects global HTTP metrics, group-tagged latency (`group` tag), checks, iterations, dropped iterations, VUs.

Machine-readable JSON written via `handleSummary` to `tests/load/reports/*.json`. Human summary:

```powershell
.\scripts\Summarize-Report.ps1 -ReportPath .\reports\<file>.json
```

Thresholds: `config/thresholds.json` (test-health, not SLA).

## Load-generator saturation

Watch during runs:

- k6 `dropped_iterations`, `vus_max` vs target
- Host CPU/memory/network (Task Manager / `Get-Counter`)
- Rising client-side timeouts with flat server metrics → generator bottleneck

## Endpoint groups (tags)

`home`, `personalized`, `discover`, `search`, `movie-detail`, `tv-detail`, `detail-status`, `ratings-me`, `reviews`, `library`, `insights`, `external-ratings-warm`, plus saturation groups `saturation-health`, `saturation-discovery`, `saturation-catalog`.

## Files

```
tests/load/
  README.md
  RUNBOOK.md
  config/
  data/
  lib/
  scenarios/
  scripts/
  reports/
```

# Movie Cave production capacity runbook (#60)

**This runbook is for manual, gated production execution after review.** The implementation task does not run production stress tests automatically.

## #60 status (2026-09-25)

| Track | Status |
|-------|--------|
| **Release readiness (100-VU sustained baseline)** | **PASS / complete** — see [`docs/ISSUE-60-CAPACITY-OUTCOME.md`](docs/ISSUE-60-CAPACITY-OUTCOME.md) |
| **Post-release scale (150+ VU, resilience, sizing)** | **Open** — not a Friends & Family Beta blocker |

**Canonical report:** `tests/load/reports/user-concurrency-capacity-100-2026-09-25T00-47-53Z.json` (operator archive; gitignored).

Grafana run **8625084** is documented as **anomalous transient degradation**, not the normal 100-VU baseline.

---

## Capacity interpretation (after real runs)

Classify each stage using **absolute thresholds**, **baseline-relative regression**, and **server-side evidence**. These are not product SLAs.

| Class | Indicators |
|-------|------------|
| **HEALTHY** | `unexpected_status` stable and low; `semantic_success` high; latency vs **5 VU baseline** and vs **previous healthy stage** within ~1.5×; no pool/resource exhaustion; no real-user impact |
| **DEGRADED** | Material latency increase (e.g. **≥2×** baseline p95 on critical groups) or brief `unexpected_status` spikes that recover; operator may stop before next stage |
| **UNHEALTHY** | Sustained `unexpected_status` **>5%**, timeouts, 5xx, **≥2.5×** baseline p99 on key groups, connection/Redis failure, restarts, or customer impact — **stop** |

The first **unhealthy** stage is valid capacity evidence. Do not force 1,000 VUs if an earlier stage fails criteria.

**Do not continue** only because hard emergency caps (e.g. p99 &lt; 15s) have not been breached — sustained **multiplier** growth vs baseline/previous stage is sufficient to stop.

---

## Preparation

1. **Verify deployed backend SHA** matches the intended baseline (`ApplicationSourceVersion` / Render deploy / `GET /health` payload `sourceVersion` if exposed).
2. **Record** load-test tooling commit SHA (`git rev-parse HEAD` in MovieApp repo).
3. **Confirm** no deployment, migration, or catalog sync job is in progress.
4. **Prepare identities**
   - Dedicated load-test accounts (not real users).
   - Valid JWTs in local `data/tokens.json` (never commit).
   - Target **≥50 identities** (minimum); **100+** for 1,000 VUs (see `docs/token-preparation.md` for JWT lifetime).
5. **Prepare content pools**
   - `data/hot-content.json` — small warm set.
   - `data/varied-content.json` — broad IDs already in PostgreSQL.
   - **External ratings:** populate `externalRatingsWarm*` only with IDs known to have PostgreSQL snapshots; set `LOAD_TEST_INCLUDE_EXTERNAL_RATINGS=true` only if needed at low weight.
6. **Search terms** — `data/search-terms.json` (benign, varied).
7. **Open dashboards** (see checklists below) and note **UTC start timestamp**.
8. **Idle baseline** — capture 10–15 minutes of server idle metrics (no load).
9. **Preflight** — `Invoke-K6.ps1 -Scenario preflight-health` (control-plane only; not counted in app RPS).
10. **Measured baseline (5 VU)** — run user-concurrency smoke and **archive the report JSON** as `baseline-5vu.json`. Record per-group p50/p95/p99, `unexpected_status` rate, application-scoped RPS (`workload_scope=application`), and timeouts.
11. **Smoke** — confirm authenticated home returns 2xx with a spot-check token (outside k6 if preferred).

### Environment variables (operator)

```
LOAD_TEST_BASE_URL=https://<production-api>
LOAD_TEST_ENVIRONMENT=production
LOAD_TEST_TOKENS_FILE=<path>\tokens.json
LOAD_TEST_CONTENT_DATASET=hot   # then repeat key stages with varied
LOAD_TEST_INCLUDE_EXTERNAL_RATINGS=false
LOAD_TEST_SEARCH_PROFILE=autocomplete-only   # default below 250 VUs; off at >=250 (see README)
```

### Baseline-relative comparison (each stage after 5 VU baseline)

Compare the stage report against **baseline-5vu.json** and the **previous healthy stage** report:

| Signal | Stop / investigate when |
|--------|-------------------------|
| `unexpected_status` rate | Sustained **>2×** baseline or **>5%** absolute |
| p95 / p99 (home, detail-status, personalized) | Sustained **≥2×** baseline or **≥2×** previous healthy stage |
| Application RPS | Flat or falling while VUs increase (saturation) |
| `rate_limited` | Rising in **user-concurrency** — likely search/IP distortion; set `LOAD_TEST_SEARCH_PROFILE=off` |
| Server connections / CPU | Trending to limits while latency multiplies |

---

## Staged execution (stop between stages)

**Release baseline (complete):** sustained **100 VU** under `capacity` / `hot` passed on 2026-09-25 (local generator, production API). Friends & Family launch is **not** blocked pending 150+ stages.

**Post-release ladder:** continue with **150 → 250 → 500 → 750 → 1000** when higher concurrency evidence is required. Optional **50 VU** remains useful for regression vs `baseline-5vu.json`.

For each target stage:

1. Run user-concurrency:

```powershell
cd tests\load
.\scripts\Invoke-K6.ps1 -Scenario user-concurrency -Preset capacity -StageTarget <N> -UseDocker
```

2. Optionally run request-capacity (read-only **application** GETs only — no `/health`) on a separate window:

```powershell
.\scripts\Invoke-K6.ps1 -Scenario request-capacity -Preset capacity -StageTarget <N> -UseDocker
```

3. Search rate limits (optional, **low VU**, separate window): `search-rate-limit` scenario — interpret `rate_limited` separately from capacity.

4. **Inspect** k6 summary (`semantic_success`, `unexpected_status`, `rate_limited`) + `Summarize-Report.ps1` + server dashboards.
5. **Cooldown** ≥10 minutes (`config/presets.json` recommends 10).
6. Proceed to next stage **only if** stage is HEALTHY or explicitly accepted as DEGRADED with documented risk.

**Stage 1,000** — use `capacity` preset (15m hold via internal `capacityStage1000` when `LOAD_TEST_STAGE_TARGET=1000`).

---

## Abort / stop conditions

Stop the stage and **do not increase VUs** when any of the following persist:

- `unexpected_status` rate **>5%** or sharp sustained increase (not benign state-absent 404 on ratings/me)
- `rate_limited` dominant in user-concurrency (fix search profile / topology, not backend capacity)
- Timeout rate climbing with load
- p99 **>15s** sustained (see `thresholds.json` abortSignals)
- Render **CPU pegged**, memory near limit, or **instance restart/crash**
- PostgreSQL **active connections** near `max_connections` or pool acquisition timeouts in logs
- **Blocking/lock** storms or multi-second slow queries attributable to test window
- Redis/Valkey **errors**, timeouts, or dangerous memory pressure/evictions
- Unexpected **TMDB/MDBList/email** traffic spikes in provider logs
- k6 **dropped_iterations** or generator CPU **>85%** sustained (client bottleneck)
- Support tickets / real-user latency complaints

---

## Render monitoring checklist

During each stage window, correlate **UTC timestamps** with:

- [ ] Instance CPU (%)
- [ ] Instance memory (%)
- [ ] HTTP request rate and 5xx rate (if available)
- [ ] P95/P99 latency (if available)
- [ ] Instance restarts / deploy events
- [ ] Outbound network (spikes may indicate external provider calls — investigate)

---

## PostgreSQL monitoring checklist

- [ ] Active connections vs max
- [ ] Connection wait / pool timeout errors in app logs
- [ ] Long-running queries (>1s) during hold period
- [ ] Lock waits / blocking sessions
- [ ] CPU and IOPS (provider metrics)
- [ ] Slow query log samples tied to test start/end

---

## Redis / Valkey monitoring checklist

- [ ] Memory usage and eviction counters (if policy allows eviction)
- [ ] Connected clients
- [ ] Command latency / error rate
- [ ] Cache hit patterns (optional — compare hot vs varied datasets)

---

## Hangfire / ASP.NET monitoring checklist

- [ ] Serilog request duration — rising tail latencies by route
- [ ] 5xx, timeouts, `OperationCanceledException`
- [ ] Npgsql / connection pool exceptions
- [ ] Hangfire queue depth growth (release checks, external ratings refresh — should not spike from read-only user test)
- [ ] JWT/auth failures (401 spikes → token prep issue, not capacity)

---

## Load-generator checklist

- [ ] k6 `dropped_iterations` ≈ 0
- [ ] `vus_max` matches target
- [ ] Generator CPU/RAM headroom
- [ ] For 500+ VUs: plan **multiple generators** or distributed k6 if single host saturates

---

## Post-run artifacts

Record for each stage:

- Report JSON path under `tests/load/reports/`
- Backend SHA, tooling SHA, dataset, identity count
- k6 p50/p90/p95/p99, RPS, failure/timeout rates
- Endpoint group breakdown (from k6 stdout or report `root_group`)
- Threshold pass/fail
- Operator notes from Render/Postgres/Redis

---

## Local production capacity (150+ VUs)

Use when Grafana Cloud project VU limits block the same stage (e.g. 100-VU Cloud cap). **Same** `user-concurrency` scenario, `capacity` preset, `hot` dataset, thresholds, and identity mapping as Cloud — identities come from **local `tokens.json`** via `k6 run` (not `k6 cloud run`). Grafana Secrets env vars are cleared; transport is **local**.

**Before hold:** confirm `LOAD_TEST_IDENTITY_PROOF transport=local identityCount=…` in k6 setup logs (once per run).

**During the run — distinguish load-generator saturation from MovieApp saturation:**

| Signal | Where |
|--------|--------|
| Local CPU pegged, high RAM, NIC maxed | Task Manager / `perfmon` on k6 host |
| k6 `dropped_iterations` / `interrupted_iterations` rising | k6 stdout + report JSON `iterations` section |
| Render CPU/RAM stable but k6 errors/timeouts | Likely generator or network bound |
| Render CPU/RAM climbing with flat generator | Likely API/backend bound |

**Gates (local + production):** `-ConfirmProductionCloudRun`, type `RUN` at prompt; `>= 250` VU also requires `-ConfirmHighScaleCloudRun`; `>= 500` requires `-ConfirmVeryHighScaleCloudRun`. Token preflight (`Test-LoadTokens.ps1`) runs automatically before k6 starts.

## 100-VU diagnostic correlation run (completed 2026-09-25)

**Outcome:** PASS — full write-up in [`docs/ISSUE-60-CAPACITY-OUTCOME.md`](docs/ISSUE-60-CAPACITY-OUTCOME.md). Cache-expiry collapse was **not** reproduced; discovery reloads during the hold stayed fast; Render retained CPU/memory headroom.

**Repeat only** after material code/infra change or if transient degradation (e.g. 8625084-like) recurs.

Original goal: determine whether **cache-miss waves** precede Render CPU saturation / k6 timeouts, or CPU saturation grows independently.

**Before hold:** note **UTC start**; enable app log level **Debug** for `MovieApp.Application` (or equivalent) so `HomePerf`, `RecHomePerf`, and `InsightsPerf` HIT/MISS lines are retained alongside `DiscoveryPerf` (Information).

**Same-clock timeline — collect in parallel:**

| Signal | Source |
|--------|--------|
| p95 / p99 over time | Grafana k6 run (trends), not only end summary |
| `http_req_failed`, semantic success | k6 |
| status=0 / transport timeout rate | k6 `http_outcome_transport_timeout` |
| CPU %, memory % | Render instance metrics |
| Home cache MISS | Render logs: `HomePerf` + `Cache=MISS` |
| Recommendation home MISS | `RecHomePerf` + `Cache=MISS` |
| Insights V3 / Summary MISS | `InsightsPerf V3` / `InsightsPerf Summary` + `Cache=MISS` |
| Discovery cache miss load | `DiscoveryPerf Cache=LOAD_COMPLETED` or `Cache=LOAD_FAILED` (per `Operation`, `CacheKey`, `CanonicalLoadMs`, `TotalLoadMs`, `FailurePhase`) |
| Redis infrastructure failures | `RedisCacheService` failure logs |

**Render log search strings:** `DiscoveryPerf`, `HomePerf Cache=MISS`, `RecHomePerf Cache=MISS`, `InsightsPerf V3 Cache=MISS`, `InsightsPerf Summary Cache=MISS`, Redis cache get/set failure messages.

**Interpretation:** align minute buckets of `DiscoveryPerf` / `HomePerf` MISS rates with Render CPU and k6 p95 spikes. Shared keys (e.g. `discovery-trending:`) should show clustered `LOAD_COMPLETED` if thundering herd is active.

---

## Cold-cache experiments

`LOAD_TEST_CONTENT_DATASET` supports only **`hot`** and **`varied`** (content ID pools for detail/reviews). There is **no** `cold` dataset mode. **Hot** = warm/steady-state small pool; **varied** = broader ID spread — neither forces backend cache misses.

Do **not** flush production Redis/Postgres to simulate cold cache. Document cold-cache experiments for **non-production** environments only (new empty Redis, fresh deploy, controlled catalog import).

---

## TV bulk watch benchmark

`scenarios/benchmark-tv-bulk-watch.js` — **staging only**, requires:

```
LOAD_TEST_ALLOW_BULK_WATCH=true
LOAD_TEST_BULK_TV_SHOW_ID=<guid>
LOAD_TEST_BULK_EPISODE_IDS=["<ep-guid>",...]
```

Never include in production user-concurrency mix.

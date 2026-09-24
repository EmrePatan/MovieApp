# Grafana Cloud k6 (secondary execution mode)

Movie Cave uses **one** k6 scenario implementation (`tests/load/scenarios/**`). Grafana Cloud runs the same scripts, weights, think times, thresholds, HOT content, and identity mapping as local `k6 run`.

Local execution remains the default and is unchanged when `-ExecutionMode Local` (or omitted).

---

## One-time setup

1. Install native k6 (v2.x; this repo was validated with v2.2.0). Cloud mode does **not** use Docker.
2. Authenticate (pick one):
   - **Interactive:** `k6 cloud login`
   - **CI/automation:** set `K6_CLOUD_TOKEN` for your Grafana Cloud stack ([tokens docs](https://grafana.com/docs/grafana-cloud/testing/k6/author-run/tokens-and-cli-authentication/))
3. Confirm load zones: `k6 cloud load-zone list` (requires auth).
4. Default EU zone for diagnostic parity: `amazon:de:frankfurt` (override with `-CloudLoadZone` or `LOAD_TEST_CLOUD_LOAD_ZONE`).
5. Configure **identity JWT pool** in Grafana Cloud (see below) — never commit `data/tokens.json`.

---

## Identity JWT strategy (production)

| Location | Mechanism |
|----------|-----------|
| Local `k6 run` | Gitignored `data/tokens.json` or `LOAD_TEST_TOKENS_FILE` |
| Grafana Cloud | Encrypted org env var **`LOAD_TEST_IDENTITIES_JSON`** (recommended) |

### What leaves your machine

When you run `Export-LoadTestIdentitiesForGrafanaCloud.ps1` and paste/upload the result into Grafana Cloud:

- **Sent:** `id` + `bearerToken` for each LOAD60 identity (minimum required for the harness).
- **Not sent by tooling:** campaign password, PostgreSQL credentials, JWT signing keys, Grafana tokens.

### What Grafana Cloud receives during a test

- Bundled **non-secret** files via `open()` (e.g. `hot-content.json`, presets, thresholds).
- **Environment variables** you pass with `k6 cloud run -e` (avoid secrets here — CLI values are stored in the archive in plain text).
- **`LOAD_TEST_IDENTITIES_JSON`** from Grafana Cloud environment variables (encrypted at rest; injected on workers).
- **Target URL** (`LOAD_TEST_BASE_URL`) — production API hostname.
- **No** `LOAD_TEST_TOKENS_FILE` in cloud mode (prevents JWTs being embedded in the archive).

Harness load order in `lib/identities.js`:

1. `LOAD_TEST_IDENTITIES_JSON`
2. `LOAD_TEST_TOKENS_FILE` (local only)
3. `LOAD_TEST_TOKEN_*` env vars
4. `data/tokens.json` fallback

### Operator steps for JWT pool

```powershell
cd tests\load
# Prepare gitignored data\tokens.json first (Mint-LoadTestTokens.ps1 / Test-LoadTokens.ps1)
.\scripts\Export-LoadTestIdentitiesForGrafanaCloud.ps1
```

Copy the generated payload into Grafana Cloud → **Testing & synthetics → Performance → Settings → Environment variables** → `LOAD_TEST_IDENTITIES_JSON`.

Delete the local `data\grafana-cloud-identities.payload.json` when done.

**Explicit confirmation:** Cloud production runs require `-ConfirmProductionCloudRun` and typing `RUN` at the prompt.

---

## Token preparation (unchanged)

Grafana Cloud does **not** fix serial login rate limiting (~25 minutes for 100 identities). Continue using:

- `Mint-LoadTestTokens.ps1` / `Test-LoadTokens.ps1`
- Resumable mint, reuse valid JWTs, stage-aware minimum lifetime

### Identity reuse for staged capacity (100 LOAD60 identities)

| Stage VUs | Identities | Reuse ratio |
|-----------|------------|-------------|
| 150 | 100 | **1.5:1** |
| 250 | 100 | **2.5:1** |

Later 500/750/1000 stages still use the **same 100-identity pool** unless you explicitly expand provisioning — do not auto-provision one identity per VU.

---

## Wrapper UX

### Local (backward compatible)

```powershell
.\scripts\Invoke-K6.ps1 `
  -Scenario user-concurrency `
  -Preset capacity `
  -StageTarget 100 `
  -ContentDataset hot `
  -BaseUrl https://movieapp-fpkg.onrender.com `
  -Environment production
```

### Cloud validate only (no production load)

```powershell
.\scripts\Invoke-K6.ps1 `
  -ExecutionMode Cloud `
  -CloudValidateOnly `
  -Scenario user-concurrency `
  -Preset smoke `
  -StageTarget 5 `
  -ContentDataset hot `
  -BaseUrl https://movieapp-fpkg.onrender.com `
  -Environment production `
  -ConfirmProductionCloudRun
```

Runs `k6 archive` + `k6 cloud upload` — inspect the test in Grafana Cloud UI; **does not execute** VUs.

### Cloud 5-VU smoke (operator-approved; not auto-run)

```powershell
.\scripts\Invoke-K6.ps1 `
  -ExecutionMode Cloud `
  -Scenario user-concurrency `
  -Preset smoke `
  -StageTarget 5 `
  -ContentDataset hot `
  -BaseUrl https://movieapp-fpkg.onrender.com `
  -Environment production `
  -ConfirmProductionCloudRun
```

### Cloud 150-VU capacity (future; do not run without approval)

```powershell
.\scripts\Invoke-K6.ps1 `
  -ExecutionMode Cloud `
  -Scenario user-concurrency `
  -Preset capacity `
  -StageTarget 150 `
  -ContentDataset hot `
  -BaseUrl https://movieapp-fpkg.onrender.com `
  -Environment production `
  -ConfirmProductionCloudRun
```

Stages **≥ 250** additionally require `-ConfirmHighScaleCloudRun`. **≥ 500** require `-ConfirmVeryHighScaleCloudRun`.

Optional deployed backend SHA:

```powershell
$env:LOAD_TEST_DEPLOYED_BACKEND_SHA = '<render-deploy-sha>'
```

---

## Reporting

Enhanced report schema **v2** is preserved for **local** runs (`tests/load/reports/*.json`).

Cloud runs:

- Primary analysis: **Grafana Cloud k6** dashboards (HTTP metrics, `group` tags, checks).
- `handleSummary` may not write a local JSON file on cloud workers; metadata still includes `executionMode: grafana-cloud`, identity count, reuse ratio, search profile, and external-ratings flag when the summary path is available.

Compare baselines by recording:

- 100 VU local (healthy)
- 250 VU local (unhealthy)
- Future 150 VU cloud — same semantic counters and group tags in Grafana.

---

## Free-tier / quota safety

Before Cloud execution, `Invoke-K6.ps1` prints:

- target VUs, identity count, reuse ratio
- preset duration
- **approximate VU-hours** (not billing-accurate; does not query Grafana APIs)

Warns when VU-hours ≥ 50. High stages need explicit confirmation switches (see above). Stages are **never** chained automatically.

---

## Load zone

Single-zone diagnostic default: `amazon:de:frankfurt`. Configure per run:

```powershell
-CloudLoadZone amazon:ie:dublin
# or
$env:LOAD_TEST_CLOUD_LOAD_ZONE = 'amazon:ie:dublin'
```

Verify codes with `k6 cloud load-zone list` for your account.

---

## Cloud compatibility notes (audit summary)

| Area | Local | Cloud |
|------|-------|-------|
| Scenario JS | `k6 run` | `k6 cloud run` (same file) |
| `open()` data files | `LOAD_TEST_DATA_DIR` | Bundled in archive |
| JWTs | `tokens.json` | `LOAD_TEST_IDENTITIES_JSON` in Grafana Cloud only |
| `handleSummary` file | Written under `reports/` | Use Grafana UI; optional path may not persist |
| Load zone | N/A | `options.cloud.distribution` via `LOAD_TEST_EXECUTION_MODE=grafana-cloud` |
| 30s HTTP timeout | `LOAD_TEST_HTTP_TIMEOUT_MS` | Same env |
| Search profile | `LOAD_TEST_SEARCH_PROFILE` / stage defaults | Same env |

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
| Grafana Cloud (smoke / small pool) | Single org env var **`LOAD_TEST_IDENTITIES_JSON`** (when export fits ≤ 4500 chars) |
| Grafana Cloud (full 100 LOAD60 pool, **recommended**) | **Grafana Secrets** (`transport: grafana-secrets`) synced via API; workers read with **`k6/secrets`** |
| Grafana Cloud (legacy fallback) | **Sharded** org env vars **`LOAD_TEST_IDENTITIES_JSON_001`…** + **`LOAD_TEST_IDENTITIES_SHARD_COUNT`** (manual paste; use `-IncludeEnvShardArtifacts` on export) |

Grafana’s Performance **environment variable UI** limits a single value to about **5000 characters**. A 100-identity minimal JSON payload is ~48 KB, so the full pool cannot live in one env var. **Secrets Management** allows up to **24 KiB per secure value**; export splits on identity boundaries at **≤ 22 KiB UTF-8 bytes** per part (`movie-cave-load-identities-001`, …). JWT material must **never** be passed via `k6 cloud run -e` and must **never** be bundled into the uploaded k6 archive.

### What leaves your machine

When you run `Export-LoadTestIdentitiesForGrafanaCloud.ps1`:

- **Operator copies into Grafana (encrypted at rest):** `id` + `bearerToken` per identity, split across shards when needed.
- **Written locally (gitignored):** `data/grafana-cloud-identities.manifest.json` (counts/transport only — **no JWTs**), plus `grafana-cloud-identities.payload.json` (single mode), `grafana-cloud-identities.secret-*.txt` (grafana-secrets mode), and optionally `grafana-cloud-identities.shard-*.txt` (legacy env-shard fallback).
- **Not sent by tooling:** campaign password, PostgreSQL credentials, JWT signing keys, Grafana tokens.

### What Grafana Cloud receives during a test

- Bundled **non-secret** files via `open()` (e.g. `hot-content.json`, presets, thresholds).
- **Harness env** from `k6 cloud run -e` (non-secret only — CLI `-e` values are stored in the archive as plain text).
- **Identity JWTs** from Grafana **Secrets** (`k6-cloud` decrypter) when `LOAD_TEST_IDENTITIES_TRANSPORT=grafana-secrets`, or from org environment variables (single var or legacy shards).
- **Target URL** (`LOAD_TEST_BASE_URL`) — production API hostname.
- **No** `LOAD_TEST_TOKENS_FILE` in cloud mode (prevents JWTs being embedded in the archive).

Harness load order in `lib/identities.js` / `user-concurrency.js` `setup()`:

1. **`LOAD_TEST_IDENTITIES_TRANSPORT=grafana-secrets`** — async `secrets.get` per manifest name (Cloud full pool; **not** `SharedArray` env load)
2. `LOAD_TEST_IDENTITIES_JSON` (if set — **takes precedence** over shards; use for 5-VU smoke only when intentionally small)
3. Else `LOAD_TEST_IDENTITIES_SHARD_COUNT` + ordered `LOAD_TEST_IDENTITIES_JSON_001`, `_002`, …
4. `LOAD_TEST_TOKENS_FILE` (local only)
5. `LOAD_TEST_TOKEN_*` env vars
6. `data/tokens.json` fallback (local)

VU mapping is unchanged: `(vuId - 1) % identityPool.length`.

### Operator flow (mint → export → sync secrets → verify)

```powershell
cd tests\load
# 1) Mint / validate gitignored pool
.\scripts\Test-LoadTokens.ps1
# 2) Export for Grafana (writes manifest + secret part files for large pools)
.\scripts\Export-LoadTestIdentitiesForGrafanaCloud.ps1
# 3) Sync to Grafana Secrets (operator machine — requires GRAFANA_URL + GRAFANA_SA_TOKEN in .env)
.\scripts\Sync-LoadTestIdentitiesToGrafanaSecrets.ps1 -DryRun
.\scripts\Sync-LoadTestIdentitiesToGrafanaSecrets.ps1
# Optional: remove stale movie-cave-load-identities-* after rotation
.\scripts\Sync-LoadTestIdentitiesToGrafanaSecrets.ps1 -PruneStaleManagedSecrets
```

**Grafana service account (sync only, never commit):**

- Set in gitignored `.env`: `GRAFANA_URL` (stack root, e.g. `https://your-stack.grafana.net`) and `GRAFANA_SA_TOKEN`.
- Token needs **Secrets Management** permissions on the stack (`secret.securevalues:create`, `get`, `update`, and `delete` if pruning).
- Sync uses `decrypters: ["k6-cloud"]` so Cloud workers can read values during the test.

**Cloud harness env (set by `Invoke-K6.ps1`, non-secret):**

| Variable | Purpose |
|----------|---------|
| `LOAD_TEST_IDENTITIES_TRANSPORT` | `grafana-secrets` when manifest transport is grafana-secrets |
| `LOAD_TEST_IDENTITIES_SECRET_COUNT` | Part count from manifest (`secretPartCount`) |
| `LOAD_TEST_IDENTITIES_SECRET_NAMES` | Comma-separated secret names (same order as export parts) |
| `LOAD_TEST_EXPECTED_IDENTITY_COUNT` | Manifest `identityCount` for runtime validation |

**Grafana Cloud → Testing & synthetics → Performance → Settings → Environment variables**

**Grafana-secrets mode (100 identities, typical):** no JWT paste — run sync script above. Clear legacy `LOAD_TEST_IDENTITIES_JSON` / shard vars if still set.

**Legacy sharded env mode (manual paste):**

| Variable | Value |
|----------|--------|
| `LOAD_TEST_IDENTITIES_SHARD_COUNT` | `N` from manifest (`shardCount`) |
| `LOAD_TEST_IDENTITIES_JSON_001` | Paste `data/grafana-cloud-identities.shard-001.txt` |
| `LOAD_TEST_IDENTITIES_JSON_002` | Paste shard-002 … through `_00N` |

- **Clear** `LOAD_TEST_IDENTITIES_JSON` when switching from old single-var smoke to sharded full pool (single var wins if still set).
- Shard names are **three-digit, 1-based, zero-padded** (`_001`, not `_1`).
- Shards concatenate in numeric order into one JSON document; splits occur only on **identity boundaries** (max **4500** chars per shard).

**Single-var mode (5-VU smoke):**

| Variable | Value |
|----------|--------|
| `LOAD_TEST_IDENTITIES_JSON` | Paste `data/grafana-cloud-identities.payload.json` |
| *(optional)* | Remove `LOAD_TEST_IDENTITIES_SHARD_COUNT` and all `LOAD_TEST_IDENTITIES_JSON_*` |

### Manifest (`data/grafana-cloud-identities.manifest.json`)

- Records **operator export** (`identityCount`, `transport`, `schemaVersion`, and for grafana-secrets: `secretPartCount`, `secretNames`, `secretPartFiles`, `maxPartUtf8Bytes`) — **not runtime proof** that Grafana has every secret or shard configured.
- `Invoke-K6.ps1` **Cloud** mode requires this file and uses it for preflight reuse math (`Cloud identity pool (manifest)`).
- Local `tokens.json` count is shown separately for token lifetime checks; mismatch vs manifest emits a **warning**.

### Token rotation

1. Incremental refresh `data/tokens.json` (`Mint-LoadTestTokens.ps1 -MinMinutesUntilExpiry 30`; use `-ForceFull` only when rotating every identity). Validate with `Test-LoadTokens.ps1`.
2. Re-run `Export-LoadTestIdentitiesForGrafanaCloud.ps1`.
3. **Grafana-secrets:** `Sync-LoadTestIdentitiesToGrafanaSecrets.ps1` (optionally `-PruneStaleManagedSecrets` after verifying part count unchanged or names rotated).
4. **Legacy shards:** update **all** Grafana shard vars (or single var) from new export files.
5. Delete local payload/secret/shard files when done.

**Rollback:** re-export previous `tokens.json` snapshot, sync again, or restore legacy env shards from `-IncludeEnvShardArtifacts` export; set manifest transport back to `sharded` only if you intentionally revert the harness (not recommended for 100-pool).

### Verify before 150-VU Cloud

1. `.\scripts\Validate-LoadTests.ps1` and `LoadTestHarness.Tests.ps1` (operator machine).
2. Optional: `k6 run scenarios/identities-shard-selfcheck.js` and `k6 run scenarios/identities-grafana-secrets-selfcheck.js` (fake tokens; no production).
3. Cloud **validate-only** upload (`-CloudValidateOnly`) after Grafana env update.
4. Small Cloud smoke (5 VU) with **single** var if desired.
5. Before 150 VU: confirm Grafana run summary metadata **`identityCount`** matches manifest (e.g. 100) — that is runtime proof on workers.

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
| JWTs | `tokens.json` | Grafana Secrets (`grafana-secrets` transport) or org env: `LOAD_TEST_IDENTITIES_JSON` (small) or legacy sharded `LOAD_TEST_IDENTITIES_JSON_*` |
| `handleSummary` file | Written under `reports/` | Use Grafana UI; optional path may not persist |
| Load zone | N/A | `options.cloud.distribution` via `LOAD_TEST_EXECUTION_MODE=grafana-cloud` |
| 30s HTTP timeout | `LOAD_TEST_HTTP_TIMEOUT_MS` | Same env |
| Search profile | `LOAD_TEST_SEARCH_PROFILE` / stage defaults | Same env |

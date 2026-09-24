# #60 Production load test — operator inputs

**Inspection/preparation only.** Does not create users, run load tests, or mutate production.

Baseline: `origin/master` load harness under `tests/load/**`.

---

## 1. Identity provisioning (current code)

### What exists today

| Path | Endpoint / component | Notes |
|------|----------------------|--------|
| Registration | `POST /api/auth/register` | Creates user; **email verification required** before login; sends verification email |
| Verify + token | `POST /api/auth/verify-email` | Returns `AuthResponse` with `AccessToken` + `ExpiresAt` |
| Login | `POST /api/auth/login` | Requires verified email; returns `AuthResponse` |
| JWT | `JwtTokenService` | **Access token only**; lifetime `Authentication:Jwt:AccessTokenMinutes` (default **60**) |
| Refresh tokens | **None** | No refresh-token API in current codebase |
| Security stamp | `JwtSecurityStampValidator` | Token invalid after stamp change (password reset, etc.) |
| Rate limits | `Authentication:RateLimit` | Per **client IP**: Register **5 / 10 min**, Login **5 / 1 min**, Verify **5 / 15 min** |

There is **no** production admin API, seed CLI, or bulk test-user utility in the repository. Integration tests use `AuthIntegrationHelpers` with `CapturingEmailSender` — **not available in production**.

### Recommended method (safest on current code)

**Out-of-band API provisioning (no k6 auth traffic):**

1. Use a **dedicated email domain** (catch-all or mailbox API) e.g. `loadtest+001@yourdomain.com`.
2. For each identity (throttled to respect rate limits):
   - `POST /api/auth/register` (local script, **not** k6).
   - Complete `POST /api/auth/verify-email` (link from mailbox).
   - Optionally `POST /api/auth/login` before each campaign segment to refresh `AccessToken` / `ExpiresAt`.
3. Or run locally (interactive, no JWT echoed): `.\scripts\Get-LoadTestToken.ps1 -BaseUrl https://movieapp-fpkg.onrender.com` — calls `POST /api/auth/login` with `{ "email", "password" }`, writes `data/tokens.json` from `AuthResponse.accessToken` / `expiresAt`.

4. Or write tokens manually to gitignored `data/tokens.json`:

```json
{
  "identities": [
    {
      "id": "loadtest-001",
      "bearerToken": "<JWT>",
      "expiresAtUtc": "2026-09-24T15:00:00Z"
    }
  ]
}
```

Record `expiresAtUtc` from `AuthResponse.ExpiresAt` at issuance time.

### Why this beats alternatives

| Alternative | Why not preferred |
|-------------|-------------------|
| k6 login/register in scenario | Measures auth/rate limits, not app capacity; pollutes metrics |
| Social auth for synthetic users | Same; 10/min IP limit; real IdP coupling |
| Raw SQL user insert | No supported ops playbook; must mirror `User` + password hash + `EmailVerifiedAtUtc`; audit/review burden |
| Local JWT mint with signing key | Requires production **SigningKey** on operator machine; higher secret exposure than login |
| Auth bypass / longer-lived JWT in app | **Forbidden** — no production code changes |

**SQL insert** may be acceptable as a **one-time DBA procedure** under change control if API registration cannot meet 50–100 users in time — but it is **not** implemented or documented in app code; prefer API register+verify when possible.

### Identity counts by stage

| Target VUs | Minimum identities | Recommended | VUs per identity (approx.) |
|------------|-------------------|-------------|----------------------------|
| 50 | 50 | 50 | 1 |
| 100 | 50 | 100 | 1–2 |
| 250 | 50 | 100 | 2–5 |
| 500 | 50 | 100 | 5–10 |
| 750 | 50 | 100 | 7–15 |
| 1000 | 50 | **100+** | ≤10 preferred |

50 identities at 1000 VUs increases personalized-cache contention; treat as **heavier** profile.

### 60-minute JWT handling

- Full campaign (**2+ hours** with holds/cooldowns) **exceeds** default token life.
- **Before each stage** (and after any 60+ minute gap): re-run **pre-stage token check**; if any token expires within the next stage window, **re-login out of band** and update `tokens.json`.
- During k6: **401/403** on home/library → treat as **token failure**, abort stage, **not** capacity regression (see `unexpected_status` vs auth).

### Pre-stage token validation

Run (does **not** count toward load test):

```powershell
cd tests\load
$env:LOAD_TEST_BASE_URL = "https://<production-api>"
$env:LOAD_TEST_TOKENS_FILE = "$PWD\data\tokens.json"
.\scripts\Test-LoadTokens.ps1 -SampleCount 10 -FailOnAuthError
```

- Probes `GET /api/home?type=all&sectionSize=5` per sampled token.
- Decodes JWT `exp` (no signature verification) and warns if expiry is before stage end.
- Exit code **1** on any 401/403 → refresh tokens before k6.

Optional: `.\scripts\preflight-health.ps1` then token test, then k6.

---

## 2. Content datasets (read-only SQL)

Run against **production PostgreSQL read replica** or read-only role. **SELECT only.**

Full queries: [`docs/sql/catalog-id-queries.sql`](sql/catalog-id-queries.sql).

### HOT (≈10–20 movies, 10–20 TV)

- High `vote_count` + ingested catalog (`tmdb_id IS NOT NULL`).
- Export GUIDs into **local** `data/hot-content.json` (production IDs should stay **local** — see `data/README.md`).

### VARIED (≈200–500+ each)

- Stratified sample across popularity deciles / genres — avoid single-title skew.
- File: `data/varied-content.json` (local).

### Search terms

- For **`search-rate-limit`** scenario only: `data/search-terms.json` (benign short terms).
- Main user-concurrency at ≥250 VUs: `LOAD_TEST_SEARCH_PROFILE=off` (harness default).

### External ratings (if ever enabled)

Only IDs from **warm snapshot** query (join `external_rating_snapshots`). Keep `LOAD_TEST_INCLUDE_EXTERNAL_RATINGS=false` for first smoke and main capacity runs.

---

## 3. First production smoke (5 VU) — do not run until inputs ready

** Preconditions:** `tokens.json` filled; `hot-content.json` filled from SQL; external ratings **off**; search **autocomplete-only** (5 VUs &lt; 250).

```powershell
cd C:\Users\User\Projects\MovieApp\tests\load

$env:LOAD_TEST_BASE_URL = "https://<production-api-host>"
$env:LOAD_TEST_ENVIRONMENT = "production"
$env:LOAD_TEST_TOKENS_FILE = "$PWD\data\tokens.json"
$env:LOAD_TEST_CONTENT_DATASET = "hot"
$env:LOAD_TEST_INCLUDE_EXTERNAL_RATINGS = "false"
$env:LOAD_TEST_SEARCH_PROFILE = "autocomplete-only"
$env:LOAD_TEST_BACKEND_SHA = "<deployed-api-sha>"
$env:LOAD_TEST_DATA_DIR = "$PWD\data"
$env:LOAD_TEST_THRESHOLDS_FILE = "$PWD\config\thresholds.json"

# 1) Control plane (not app throughput)
.\scripts\Invoke-K6.ps1 -Scenario preflight-health -Preset smoke -BaseUrl $env:LOAD_TEST_BASE_URL

# 2) Validate tokens + content (no k6 load)
.\scripts\Test-LoadTokens.ps1 -SampleCount 5 -FailOnAuthError
.\scripts\Test-ContentIds.ps1 -Dataset hot -FailOnError

# 3) Measured smoke — 5 VU user-concurrency
.\scripts\Invoke-K6.ps1 -Scenario user-concurrency -Preset smoke -StageTarget 5 -BaseUrl $env:LOAD_TEST_BASE_URL

# 4) Summarize
.\scripts\Summarize-Report.ps1 -ReportPath (Get-ChildItem .\reports\user-concurrency-smoke-5-*.json | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
```

**Success criteria for smoke:**

- `unexpected_status` near 0%; no 401/403 on authenticated routes.
- `semantic_success` high; ratings/me **404** allowed (state absent).
- `rate_limited` ≈ 0 on user-concurrency (search profile).
- Report JSON written; groups (home, detail-status, ratings-me, …) present in summary.
- No MDBList/TMDB spike in provider dashboards during window.

---

## 4. Local files operators must fill

| File | Git | Contents |
|------|-----|----------|
| `data/tokens.json` | **Ignored** | JWTs + optional `expiresAtUtc` |
| `data/hot-content.json` | Local prod copy **ignored** (`*-production.json` pattern) | Movie/TV GUID pools |
| `data/varied-content.json` | Local prod copy ignored | Large pools |
| `data/search-terms.json` | Optional commit (terms only) | Search probe terms |
| `.env` | Ignored | Convenience copy of env vars |

Template placeholders in repo: `*.example.json`, `data/hot-content.json` (dev GUIDs only).

---

## 5. Report / secret safety

- k6 `http.js` sets `Authorization` header but does **not** log it; summary JSON does **not** include headers or tokens.
- Do not paste k6 stdout into tickets if debug logging is enabled.
- `tokens.json` and passwords stay gitignored.

# MovieApp — Production Security (#27)

Operational security notes for the MovieApp API. Complements `PRODUCTION-LAUNCH-CHECKLIST.md` and `PRODUCTION_DATABASE_RUNBOOK.md`.

**Completed in #27:** H1, H2, M1, M2, M4 (client IP / forwarded headers), M7 (social OAuth startup validation). M3 reviewed — no change.

---

## Health endpoints

| Endpoint | Render / public role | Notes |
|----------|----------------------|-------|
| `GET /health/live` | **Platform liveness probe** | Configure Render health check to this path. Returns `200` when Kestrel is running; no dependency probes. |
| `GET /health` | Legacy alias of liveness | Same behavior as `/health/live`. |
| `GET /health/ready` | **Ops / post-deploy smoke only** | Probes PostgreSQL, pending EF migrations, and Redis. Not for Render's recurring health check. Response includes check names and sanitized status (no secrets). |

Readiness may be called manually after deploy or from an internal smoke-test step. Do not add WAF rules in application code; restrict exposure at the edge only if your threat model requires it.

---

## AllowedHosts

Set the production API hostname explicitly so ASP.NET host filtering rejects unexpected `Host` headers.

**Staging example host:** `movieapp-fpkg.onrender.com` (from `App:PublicBaseUrl` = `https://movieapp-fpkg.onrender.com`)

```bash
# Render env — use the host from App__PublicBaseUrl (no scheme)
AllowedHosts=movieapp-fpkg.onrender.com
```

For a custom domain, set `AllowedHosts` to that hostname (for example `api.yourdomain.com`). Multiple hosts: semicolon-separated (`api.example.com;movieapp-fpkg.onrender.com`).

Development keeps `AllowedHosts: "*"` in `appsettings.json`.

---

## Redis and distributed rate limiting

In Production, distributed rate limits (auth, search, account mutations) use Redis via `CompositeRateLimitCounterStore`.

**Intentional fail-closed behavior:** if Redis is unavailable at request time, affected endpoints return **429** rather than falling back to per-instance in-memory counters. This prevents rate-limit bypass across replicas during a Redis outage.

- **Cache data:** Redis cache misses degrade to PostgreSQL/TMDB — acceptable.
- **Rate limits:** monitor Redis availability; treat outage as limited API availability for rate-limited routes, not a security incident.

Auth rate limits are also Redis-backed in Production (not in-memory only).

---

## #27 accepted decisions (no further action)

| ID | Decision | Rationale |
|----|----------|-----------|
| **M6** — No server logout API | **Accepted** | JWT + `security_stamp` invalidates all outstanding tokens on password/email change. Mobile logout discards the token locally. Per-device revocation would require a `jti` blocklist (out of scope). |
| **L1** — No global authorization fallback | **Accepted (V1)** | API is intentionally mixed public/private (catalog, search, auth public; user data `[Authorize]`). Fallback policy deferred; new endpoints must declare auth explicitly. |
| **L2** — Liveness exposes env + version | **Accepted** | Minor recon value; useful for ops and deploy verification on `/health/live`. |
| **L3** — Password length-only (8–128) | **Accepted** | Product choice; auth endpoints are rate-limited. |
| **L4** — `jti` not checked | **Accepted** | `security_stamp` covers credential-change revocation; token blocklist deferred. |
| **L5** — Email in JWT | **Accepted** | Documented API contract (`docs/MOBILE_API_CONTEXT.md`). |
| **L7** — Public product-metrics endpoint | **Accepted** | Allowlisted metric names + IP rate limit (120/min). |

Do not implement token blocklists, fallback authorization, password complexity rules, or JWT claim changes without a new security review.

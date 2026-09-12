# MovieApp — Production Readiness Audit (Step 29D)

**Date:** 2026-09-12  
**Scope:** Backend (`MovieApp`) + mobile production configuration (`MovieApp.Mobile`)  
**Status:** Audit only — no deployment, no code changes, no secrets

This document evaluates readiness for the target V1 production path:

```text
MovieApp Mobile (Android / iOS)
      ↓ HTTPS
Production ASP.NET Core API
      ↓
PostgreSQL
      ↓
Redis
      ↓
TMDB
```

---

## Classification Legend

| Label | Meaning |
|-------|---------|
| **BLOCKER** | Must be resolved before production launch |
| **HIGH** | Strongly recommended before launch; significant risk if skipped |
| **MEDIUM** | Should be planned early post-launch or during hardening |
| **LOW** | Nice-to-have; can defer |
| **READY** | Already acceptable for V1 with correct env configuration |

---

## 1. Current Architecture

**Classification: READY (structure) / HIGH (operations gaps)**

### What exists today

| Layer | Current state |
|-------|---------------|
| **API** | ASP.NET Core (.NET 10), Clean Architecture (`Api` → `Application` → `Domain` ← `Infrastructure`) |
| **Database** | PostgreSQL via EF Core + Npgsql; 8 migrations; indexes and FKs defined in entity configurations |
| **Cache** | Redis via `StackExchangeRedisCache` when configured; otherwise `DistributedMemoryCache` fallback |
| **Auth** | JWT Bearer (HMAC-SHA256) + `SecurityStamp` revocation on credential changes |
| **Catalog** | Lazy TMDB upsert on search/detail; no static seed data |
| **Email** | Password reset via SMTP (production) or Development sender (local) |
| **Local infra** | `docker-compose.yml` — PostgreSQL 17 + Redis 7 only |
| **Mobile** | Expo / React Native; `EXPO_PUBLIC_*` env vars; EAS build profiles |

### Current request flow (development)

```text
Mobile app → http://localhost:5027 (or LAN IP)
         → ASP.NET Core API (dotnet run)
         → PostgreSQL (docker-compose or local)
         → Redis (docker-compose or local, optional)
         → Fake or TMDB provider
```

### Gaps vs production target

| Gap | Severity |
|-----|----------|
| No API Dockerfile or container service | BLOCKER |
| No CI/CD pipeline | BLOCKER |
| No managed HTTPS / public API endpoint | BLOCKER |
| Secrets not validated at startup (JWT, PostgreSQL) | BLOCKER |
| Default `MovieProviders:Provider=Fake` in base config | BLOCKER |
| Release build fails (`TreatWarningsAsErrors` + CA1848/CA1873) | HIGH |
| No global exception handler | HIGH |
| Redis optional with in-memory fallback | HIGH (multi-instance) |
| Console-only logging | MEDIUM |
| No automated migration step in deploy | HIGH |

---

## 2. Recommended Production Architecture

**Classification: HIGH (design target)**

Minimum V1 architecture — secure, inexpensive, operable, horizontally scalable later:

```text
Internet
   ↓
DNS: api.example.com
   ↓
Managed TLS termination (reverse proxy / platform ingress)
   ↓
ASP.NET Core API (1–2 instances initially)
   ↓                    ↓
Managed PostgreSQL    Managed Redis
   ↓
TMDB API (external)
```

### Design principles

- **Single monolithic API** — no microservices, no Kubernetes for V1
- **Managed PostgreSQL and Redis** — avoid self-operating databases on day one
- **HTTPS everywhere** — mobile requires `https://` in production builds
- **Secrets via environment / secret manager** — never in git
- **Explicit proxy trust** — forwarded headers only for known load balancer IPs
- **Deploy migrations as a controlled step** — not at API startup
- **Stateless API instances** — shared Redis required when scaling beyond one instance

### What we deliberately exclude for V1

- Kubernetes, service mesh, Elasticsearch
- Dedicated image CDN/proxy (use TMDB image CDN directly)
- Complex observability stack (Seq/Datadog optional later)

---

## 3. Hosting Recommendation

**Classification: HIGH**

### Options compared

| Option | Complexity | HTTPS | Backups | PG/Redis | Logging | Scaling | Est. V1 cost | Fit |
|--------|------------|-------|---------|----------|---------|---------|--------------|-----|
| **Single VPS** (Hetzner, DigitalOcean, Linode) | Medium | Manual (Caddy/nginx + Let's Encrypt) | Manual | Self-hosted on VPS or separate managed DB | Self-configured | Vertical first | ~$15–40/mo | Good if comfortable with ops |
| **Managed container** (Fly.io, Render, Railway) | Low–Medium | Platform-managed | Platform-dependent | Add-on managed PG/Redis | Platform logs | Easy horizontal | ~$25–60/mo | **Best balance for V1** |
| **Cloud PaaS** (Azure App Service, AWS ECS/Fargate) | Medium–High | Platform-managed | Rich options | RDS/ElastiCache | CloudWatch/etc. | Strong | ~$50–120/mo | Overkill for initial launch |
| **Serverless** (Azure Functions, Lambda) | High for this app | Yes | N/A | Separate services | Cloud-native | Complex for ASP.NET long-running | Variable | Not recommended |

### Recommendation: **Managed container platform + managed PostgreSQL + managed Redis**

**Primary recommendation:** [Fly.io](https://fly.io) or [Render](https://render.com)

**Rationale:**
- Simple Docker-based deploy path (once Dockerfile exists)
- Built-in HTTPS and automatic certificates
- Managed PostgreSQL and Redis add-ons with backups
- Low operational overhead for a solo/small team
- Easy to add a second API instance later
- Cost-effective for a small commercial mobile app

**Alternative:** Single VPS (e.g. Hetzner CX22 ~€4–6/mo) + managed PostgreSQL (Neon/Supabase free tier or paid) + Upstash Redis if budget is tight and ops skills exist.

**Do not purchase or create accounts in this audit step.**

---

## 4. Domain + HTTPS

**Classification: BLOCKER**

### Production requirement

```text
https://api.example.com
```

Mobile production validation (`MovieApp.Mobile/src/api/environment.ts`):
- `EXPO_PUBLIC_APP_ENV=production`
- `EXPO_PUBLIC_API_URL` must use **`https:`**
- Hostname must **not** be localhost, emulator (`10.0.2.2`), or private LAN (`192.168.x.x`, `10.x`, `172.16–31.x`)

### Backend HTTPS

- `UseHttpsRedirection()` is enabled in `ApplicationBootstrap.ConfigurePipeline`
- Production TLS termination expected at **reverse proxy / platform ingress**
- API container/process may listen HTTP internally behind TLS-terminating proxy

### Required actions

| Item | Priority |
|------|----------|
| Register domain (e.g. `movieapp.example.com`) | BLOCKER |
| Create DNS A/AAAA or CNAME for `api.<domain>` | BLOCKER |
| Enable managed TLS (Let's Encrypt or platform cert) | BLOCKER |
| Set `EXPO_PUBLIC_API_URL=https://api.<domain>` in EAS production env | BLOCKER |
| Configure HTTP → HTTPS redirect at edge | HIGH |
| Restrict `AllowedHosts` to `api.<domain>` in production | HIGH |
| Add HSTS at reverse proxy | MEDIUM |

### Mobile deep links (password reset)

Production `Authentication:PasswordReset:BaseUrl` should remain:

```text
movieapp://reset-password
```

Universal links (`https://app.example.com/reset-password`) are optional V1.1.

---

## 5. PostgreSQL

**Classification: HIGH (schema READY, ops BLOCKER)**

### Current state

- **8 EF Core migrations** in `src/MovieApp.Infrastructure/Persistence/Migrations/`
- **Indexes:** comprehensive — user email, favorites, watchlists, ratings, reviews, watch history, search history, external IDs (TMDB/TVDB/IMDB), composite uniqueness constraints
- **Connection resolution:** `PostgreSqlOptions.ResolveConnectionString()` — full connection string OR host/port/database/username/password
- **Registration:** `UseNpgsql(connectionString)` — no retry, no explicit SSL, no pool tuning
- **`IsConfigured()`** returns true without password — misconfiguration can produce invalid connection strings silently

### Production migration procedure (empty database)

**Do NOT copy development database. Do NOT auto-migrate at API startup.**

Recommended one-time initialization:

```bash
# 1. Provision empty managed PostgreSQL database
# 2. Set connection string via secret (SSL required)
export ConnectionStrings__Default=""  # OR PostgreSql__ConnectionString
# Example format:
# Host=xxx.postgres.provider.com;Port=5432;Database=movieapp;Username=movieapp;Password=***;SSL Mode=Require;Trust Server Certificate=false

# 3. From CI/CD or operator workstation with network access to prod DB:
dotnet ef database update \
  --project src/MovieApp.Infrastructure \
  --startup-project src/MovieApp.Api \
  --connection "$PRODUCTION_CONNECTION_STRING"

# 4. Verify schema
# 5. Deploy API
# 6. Smoke test: GET /health/ready → 200
```

### Recommended production connection string parameters

```text
SSL Mode=Require
Pooling=true
Maximum Pool Size=50        # tune per instance count
Command Timeout=30
```

Consider `EnableRetryOnFailure` on Npgsql for transient errors (implementation step, not this audit).

### Classification

| Item | Status |
|------|--------|
| Schema / migrations | READY |
| Indexes / constraints | READY |
| Startup connection validation | BLOCKER |
| SSL enforcement | HIGH |
| Retry on transient failure | MEDIUM |
| Migration automation in deploy pipeline | HIGH |
| Connection pooling tuning | MEDIUM |

---

## 6. Redis

**Classification: HIGH**

### Current state

- Configured via `Redis:ConnectionString` and `Redis:InstanceName` (default `MovieApp:`)
- If connection string empty → **`DistributedMemoryCache`** fallback
- `RedisCacheService` wraps `IDistributedCache` — no circuit breaker
- Health check registered only when connection string configured
- Cache used for: home, search, discovery, recommendations, movie/TV detail/search — **not required for core auth/user data**

### Production requirements

| Requirement | Priority |
|-------------|----------|
| Use managed Redis in production | HIGH |
| Require Redis when running >1 API instance | BLOCKER (if scaled) |
| Enable Redis AUTH password | HIGH |
| Use TLS (`rediss://`) if provider supports | HIGH |
| Document graceful degradation: cache miss → DB/provider fetch | READY |

### Failure behavior

- **Redis down:** cache reads return null → services fall through to PostgreSQL/TMDB (slower, not fatal)
- **Risk without Redis at scale:** inconsistent cache across instances, higher TMDB/DB load
- **Recommendation:** treat Redis as **required in production** even for single instance (consistent behavior, reduced TMDB load)

### Do not introduce Redis Cluster for V1.

---

## 7. JWT Security

**Classification: BLOCKER (validation gap) / READY (algorithm design)**

### Current configuration

| Setting | Value |
|---------|-------|
| Algorithm | HMAC-SHA256 (symmetric) |
| Issuer | `MovieApp` |
| Audience | `MovieApp.Mobile` |
| Access token lifetime | 60 minutes |
| Signing key source | `Authentication:Jwt:SigningKey` (env/secret) |
| Revocation | `SecurityStamp` claim validated on every request via `JwtSecurityStampValidator` |

### Production requirements

| Requirement | Current | Status |
|-------------|---------|--------|
| Key not in source control | Empty in appsettings ✅ | READY |
| Key from env/secret manager | Supported ✅ | READY |
| Dev/prod keys different | Operator responsibility | HIGH |
| Key length ≥ 256 bits (32+ bytes random) | Not enforced | BLOCKER |
| Startup fails if key missing in Production | **No — validation skipped silently** | BLOCKER |
| SecurityStamp validation | Enabled ✅ | READY |
| Key rotation mechanism | None | MEDIUM |

### Critical finding

In `JwtAuthenticationExtensions.cs`, if `SigningKey` is empty, `TokenValidationParameters` are **not configured**. The API starts but JWT validation is effectively broken. **`JwtTokenService` throws on token creation**, but existing tokens would not be validated correctly.

**Implementation plan item:** Add `ValidateOnStart` for JWT options in non-Development environments.

---

## 8. TMDB Configuration

**Classification: BLOCKER (production config) / READY (implementation)**

### Configuration keys

```text
MovieProviders__Provider=Tmdb
MovieProviders__Tmdb__ReadAccessToken=<secret>   # preferred
MovieProviders__Tmdb__ApiKey=<secret>            # fallback
```

### Current behavior

- Default in `appsettings.json`: **`Provider=Fake`**
- When `Provider=Tmdb`: `ValidateOnStart` ensures credentials exist ✅
- **No validation preventing `Fake` in Production** ❌
- `TmdbApiClient`: 3 retries, backoff, honors `Retry-After`, 30s timeout ✅
- README warns: **TMDB free tier may not permit commercial use** — verify licensing before monetization

### Production requirements

| Item | Priority |
|------|----------|
| Set `Provider=Tmdb` via production env | BLOCKER |
| Store credentials in secret manager | BLOCKER |
| Add production startup validation blocking `Fake` provider | BLOCKER |
| Document TMDB attribution in mobile app / store listing | HIGH |
| Monitor TMDB rate limits | MEDIUM |

### TMDB attribution (mobile / store)

Per TMDB terms, production apps must include attribution such as:

> *This product uses the TMDB API but is not endorsed or certified by TMDB.*

Display TMDB logo where required. Add to Settings/About screen before store submission.

**Do not place TMDB credentials in mobile app or `EXPO_PUBLIC_*` variables.**

---

## 9. SMTP / Password Reset Email

**Classification: HIGH (config READY) / BLOCKER (SMTP failure behavior)**

### Current production validation ✅

`PasswordResetOptionsValidator` with `ValidateOnStart` (non-Development/Testing):
- Requires `EmailProvider=Smtp`
- Requires SMTP `Host` + `FromAddress` configured
- Requires `BaseUrl` set
- Rejects Development sender in production

### Email sender resolution

| Environment | Sender |
|-------------|--------|
| Testing | `CapturingEmailSender` |
| Development + `EmailProvider=Development` | `DevelopmentEmailSender` (logs only) |
| Production + `EmailProvider=Smtp` | `SmtpEmailSender` |
| Other | Startup exception |

`DevelopmentEmailSender` returns immediately (no-op) if `IsProduction()` — defense in depth ✅

### SMTP hardening issue (fix before production)

**Current behavior in `ForgotPasswordService`:**
1. For existing active user: create token in DB, then call `SendPasswordResetEmailAsync`
2. Return generic success message **only if no exception**

**Problem:** If SMTP fails for an **existing** email → exception propagates → **HTTP 500**.  
For **non-existing** email → generic **HTTP 200** (no email sent).

This creates observable behavior difference and poor UX under SMTP outage.

**Recommendation (implementation plan — Step 29E or pre-deploy hardening):**
- Catch SMTP failures after token persistence
- Log error server-side (no token/URL in logs)
- Return generic 200 to client regardless (same message as non-existent email)
- Optionally queue retry / alert ops
- Add integration test for SMTP failure path

**Classification:** BLOCKER for production password reset reliability

### Production SMTP checklist

| Item | Priority |
|------|----------|
| SMTP provider (SendGrid, Mailgun, Amazon SES, etc.) | BLOCKER |
| TLS enabled (`EnableSsl=true`) | BLOCKER |
| Credentials in secret manager | BLOCKER |
| Verified sender domain (SPF/DKIM) | HIGH |
| Fix SMTP runtime 500 vs 200 inconsistency | BLOCKER |
| Rate limit forgot-password (existing 3/15min) | READY |

---

## 10. CORS

**Classification: READY (for native mobile)**

### Current state

**No CORS configured** — no `AddCors` / `UseCors` in the solution.

### Analysis

| Client | CORS needed? |
|--------|--------------|
| Android native app | No |
| iOS native app | No |
| Expo native builds | No |
| Browser / web SPA | Yes |
| Swagger UI (Development) | Same-origin in dev ✅ |

### Recommendation

- **V1 native mobile only:** do **not** add permissive CORS; current state is acceptable ✅
- If a web client is added later: configure explicit allowed origins — never `AllowAnyOrigin` with credentials
- Do not weaken CORS for mobile convenience

---

## 11. Rate Limiting

**Classification: READY (auth) / MEDIUM (catalog)**

### Current auth policies (Step 29B)

| Endpoint | Limit | Window |
|----------|-------|--------|
| Login | 5 | 1 min |
| Register | 5 | 10 min |
| Forgot password | 3 | 15 min |
| Reset password | 5 | 15 min |

- Partition key: `{clientIp}:{endpointPath}`
- Rejection: HTTP 429 + `Retry-After` + JSON problem body ✅
- Applied only to `AuthController` actions

### Production considerations

| Item | Priority |
|------|----------|
| Enable `ForwardedHeaders` with KnownProxies when behind LB | HIGH |
| Without forwarded headers, all clients appear as LB IP | HIGH |
| NAT/shared IP grouping (documented trade-off) | MEDIUM — acceptable for V1 |
| Additional limits on search/catalog | LOW for V1 — monitor first |
| Global API rate limit | LOW — defer unless abuse observed |

### Recommendation

Auth rate limiting is **READY** for V1. Enable forwarded headers in production. Do not over-engineer catalog rate limits until traffic patterns are known.

---

## 12. Forwarded Headers / Proxy Security

**Classification: HIGH**

### Current state

`ForwardedHeaders:Enabled=false` by default.

When enabled:
- Processes `X-Forwarded-For` and `X-Forwarded-Proto`
- **Clears** default known networks/proxies
- Only trusts explicitly configured `KnownProxies` (IP list) and `KnownNetworks` (CIDR)

Middleware order: forwarded headers → Serilog → HTTPS redirect → rate limiter ✅

### Production requirement

When API sits behind load balancer / platform proxy:

```json
"ForwardedHeaders": {
  "Enabled": true,
  "KnownProxies": ["<load-balancer-ip>"],
  "KnownNetworks": ["10.0.0.0/8"]
}
```

Exact values depend on hosting provider documentation.

### Security rule

**Never enable forwarded headers without explicit KnownProxies/Networks.** Blind trust allows IP spoofing and rate-limit bypass.

---

## 13. Logging

**Classification: MEDIUM**

### Current state

- Serilog with console sink only
- Config-driven levels: Information (Production), Debug (Development)
- `UseSerilogRequestLogging()` enabled
- No correlation/request ID enrichment
- No persistent or centralized log sink

### Production log requirements

Logs must **never** contain:
- Passwords (plain or hashed)
- JWT access tokens
- Password-reset raw tokens or reset URLs with tokens
- SMTP passwords
- TMDB credentials
- Full connection strings

Current code logs email address on SMTP failure (`SmtpEmailSender`) — acceptable for ops but review GDPR retention.

### Recommendations

| Item | Priority |
|------|----------|
| Console logging to platform log drain | READY (Fly/Render/Azure capture stdout) |
| Structured JSON logging in production | MEDIUM |
| Request correlation ID middleware | MEDIUM |
| External sink (Seq free tier, Logtail, CloudWatch) | LOW for V1 |
| Log retention policy (30–90 days) | MEDIUM |
| PII redaction review | MEDIUM |

Do not introduce expensive observability stack for V1 unless justified.

---

## 14. Error Handling

**Classification: HIGH**

### Current state

- Per-controller `try/catch` returning manual `ProblemDetails`
- Application exceptions: `ValidationException`, `AuthenticationException`, `NotFoundException`, `ConflictException`
- No global `UseExceptionHandler` / `IExceptionHandler`
- Unhandled exceptions: ASP.NET Core default (may expose details in Development)
- Rate limiter 429: anonymous JSON (not full RFC 7807 ProblemDetails type)

### Recommended production behavior

| Status | Production response |
|--------|---------------------|
| 400 | Validation message (safe, user-facing) |
| 401 | Generic auth failure |
| 403 | Forbidden (if used) |
| 404 | Resource not found |
| 409 | Conflict (duplicate email, etc.) |
| 429 | Too many requests + Retry-After ✅ |
| 500 | Generic "An unexpected error occurred" — **no stack trace** |
| TMDB unavailable | 503 or 502 with safe message; log provider error server-side |
| PostgreSQL unavailable | 503; readiness probe fails |
| Redis unavailable | Degrade silently (cache miss); log warning |

### Implementation plan item

Add global exception handler middleware returning consistent ProblemDetails in Production/Staging. Keep detailed errors in Development only.

---

## 15. Health Checks

**Classification: MEDIUM**

### Current endpoints

| Endpoint | Behavior | Auth |
|----------|----------|------|
| `GET /health` | Always returns `"Healthy"` | Public |
| `GET /health/ready` | ASP.NET health checks — PostgreSQL + Redis when configured | Public |

### Assessment

- `/health` is **liveness theater** — never reports unhealthy
- `/health/ready` is suitable for **readiness** probe
- Exposes `EnvironmentName` on `/health` — minor information disclosure

### Recommendation

| Item | Priority |
|------|----------|
| Use `/health/ready` for load balancer readiness | HIGH |
| Add `/health/live` returning 200 if process alive (or repurpose `/health`) | MEDIUM |
| Restrict health endpoints to internal network or strip env name | LOW |
| Do not expose connection strings or dependency credentials | READY ✅ |

Existing setup is **acceptable for V1** with `/health/ready` as primary probe.

---

## 16. Swagger

**Classification: READY**

- Swagger + SwaggerUI registered with JWT Bearer auth scheme
- **`IsDevelopment()` gate** — disabled in Production ✅

### Recommendation

- Keep Swagger disabled in production for V1 ✅
- If enabled later for internal use: protect with auth or IP allowlist
- Do not expose internal API documentation publicly

---

## 17. Catalog Ingestion / Data Seeding

**Classification: READY (lazy model) / HIGH (production config)**

### How content enters PostgreSQL

- **No static seed data** — no `HasData()`, no startup seeders
- **Lazy ingestion:** search and detail requests fetch from provider (Fake/TMDB), upsert to PostgreSQL, cache in Redis
- **Empty production DB is valid** — API starts; catalog populates as users search/browse
- **Duplicate prevention:** unique indexes on external IDs; upsert logic in repositories

### Production rules

| Rule | Priority |
|------|----------|
| Do NOT copy development database to production | BLOCKER |
| Do NOT migrate Fake provider catalog as production content | BLOCKER |
| Use `Provider=Tmdb` in production | BLOCKER |
| Legacy Fake rows (if any) remain addressable by GUID but won't resolve via TMDB | MEDIUM — document |
| Re-search TV/movie titles to ingest real TMDB data | HIGH |
| Optional: warm-cache script post-deploy (search popular titles) | LOW |

### Fake → TMDB switch

Switching provider does **not** delete existing rows. Fake TV/movie records with synthetic external IDs won't hydrate from TMDB. Production should start with **empty catalog** under TMDB provider.

---

## 18. Fake Provider Safety

**Classification: BLOCKER**

### Current selection

- `MovieProviders:Provider` in config — default **`Fake`** in `appsettings.json`
- `FakeMovieDataProvider` / `FakeTvShowDataProvider` return deterministic synthetic data
- TMDB credentials validated only when `Provider=Tmdb`

### Risk

Production deployment with missing env override → **Fake catalog in production**.

### Recommended startup validation (implementation plan)

In non-Development/Testing environments:
```text
IF Provider != Tmdb → startup FAIL with clear error message
```

Keep `Fake` available for Development and Testing environments only.

**Classification:** BLOCKER — add production provider validation before deploy

---

## 19. Secrets Inventory

**Do not commit values. Do not log values.**

| Secret | Required in production | Storage recommendation | Status |
|--------|-------------------------|------------------------|--------|
| PostgreSQL password | Yes | Platform secret manager / env var | BLOCKER to provision |
| PostgreSQL connection string | Yes (alternative) | Secret manager | BLOCKER |
| Redis password | Yes (if provider requires) | Secret manager | HIGH |
| Redis connection string | Yes | Secret manager | HIGH |
| JWT signing key | Yes | Secret manager (≥32 random bytes) | BLOCKER |
| TMDB ReadAccessToken | Yes (preferred) | Secret manager | BLOCKER |
| TMDB ApiKey | Optional fallback | Secret manager | HIGH |
| SMTP username | Yes | Secret manager | BLOCKER |
| SMTP password | Yes | Secret manager | BLOCKER |
| SMTP host/port/from address | Yes (non-secret host OK) | Config + secrets | BLOCKER |

### Additional configuration (non-secret but required)

| Config | Example | Storage |
|--------|---------|---------|
| `Authentication:PasswordReset:BaseUrl` | `movieapp://reset-password` | appsettings / env |
| `Authentication:Jwt:Issuer` | `MovieApp` | config |
| `Authentication:Jwt:Audience` | `MovieApp.Mobile` | config |
| `ForwardedHeaders:KnownProxies` | LB IP addresses | config |
| `AllowedHosts` | `api.example.com` | config |

### Mobile public env (NOT secrets)

| Variable | Example | Notes |
|----------|---------|-------|
| `EXPO_PUBLIC_APP_ENV` | `production` | Public ✅ |
| `EXPO_PUBLIC_API_URL` | `https://api.example.com` | Public ✅ |
| `EXPO_PUBLIC_IMAGE_BASE_URL` | `https://image.tmdb.org/t/p/w500` | Public ✅ |

**Never put JWT, TMDB, SMTP, or DB credentials in `EXPO_PUBLIC_*`.**

---

## 20. Mobile Production Configuration

**Classification: BLOCKER (EAS env) / READY (validation logic)**

### Required variables

| Variable | Required | Production rule |
|----------|----------|-----------------|
| `EXPO_PUBLIC_APP_ENV` | Yes | `production` for store builds |
| `EXPO_PUBLIC_API_URL` | Yes | HTTPS, public hostname |
| `EXPO_PUBLIC_IMAGE_BASE_URL` | Strongly recommended | TMDB image CDN base |

### Current EAS configuration (`eas.json`)

Sets `EXPO_PUBLIC_APP_ENV` per profile only:
- `development` → `development`
- `preview` → `preview`
- `production` → `production`

**Does NOT set `EXPO_PUBLIC_API_URL` or `EXPO_PUBLIC_IMAGE_BASE_URL`.**

### Required before store release

Configure in **EAS project environment variables** (production profile):

```text
EXPO_PUBLIC_APP_ENV=production
EXPO_PUBLIC_API_URL=https://api.<your-domain>
EXPO_PUBLIC_IMAGE_BASE_URL=https://image.tmdb.org/t/p/w500
```

Validation enforced at runtime by `validateApiBaseUrl()` in `src/api/environment.ts`.

### Files inspected

- `app.config.ts` — reads `EXPO_PUBLIC_APP_ENV`
- `src/api/config.ts` — throws if `EXPO_PUBLIC_API_URL` missing
- `.env.example` — documents local vs production expectations
- `docs/RELEASE_CHECKLIST.md` — store submission TODOs

**Do not modify mobile config in this audit step.**

---

## 21. Image Hosting

**Classification: HIGH**

### Current architecture

- API returns **relative poster/backdrop paths** (e.g. `/abc123.jpg` — TMDB-style paths)
- Mobile `resolveImageUri()` in `src/utils/image-url.ts`:
  - Absolute `http(s)://` URLs returned unchanged
  - Relative paths require `EXPO_PUBLIC_IMAGE_BASE_URL` prefix
  - If unset → `null` → UI shows placeholders

### Recommended V1 approach

**Use TMDB image CDN directly — no backend image proxy needed.**

```text
EXPO_PUBLIC_IMAGE_BASE_URL=https://image.tmdb.org/t/p/w500
```

| Size | TMDB path | Use case |
|------|-----------|----------|
| w500 | `/t/p/w500` | List cards, posters |
| w780 | `/t/p/w780` | Detail hero (optional future) |
| original | `/t/p/original` | Avoid for mobile bandwidth |

### Legal / licensing

- TMDB attribution required (see Section 8)
- Verify TMDB commercial licensing before monetization
- No need for dedicated CDN/image proxy in V1

### Do not introduce image proxy unless:
- Backend starts returning non-TMDB paths, or
- Licensing requires self-hosting (not current state)

---

## 22. Deployment Process

**Classification: BLOCKER (process design) / HIGH (implementation)**

### Recommended repeatable deployment

```text
1. git push to main (or release tag)
        ↓
2. CI: dotnet build (Release) + unit tests
        ↓
3. CI: docker build → push image (once Dockerfile exists)
        ↓
4. Deploy new API version (rolling/blue-green)
        ↓
5. Run EF migration (manual approval gate in CI)
   dotnet ef database update --connection $PROD_CONNECTION
        ↓
6. Health check: GET /health/ready → 200
        ↓
7. Smoke tests:
   - GET /api/movies/search?query=test (no auth)
   - POST /api/auth/register + login (staging user)
   - Authenticated favorites/watchlist round-trip
        ↓
8. Monitor logs for 15–30 minutes
```

### Rollback strategy

| Failure point | Rollback action |
|---------------|-----------------|
| Migration fails | Do not deploy new API; fix migration offline |
| API deploy fails health check | Revert to previous container/image version |
| Post-deploy errors | Roll back API image; DB rollback only if migration was backward-compatible |
| SMTP/TMDB misconfig | Fix env vars; redeploy — no DB rollback needed |

**Rule:** keep migrations backward-compatible when possible; avoid destructive migrations without maintenance window.

### Do NOT

- Auto-run migrations at API startup (risky with multiple instances)
- Copy dev database to production
- Deploy without smoke tests

---

## 23. CI/CD

**Classification: READY (CI foundation) / BLOCKER (CD + deployment)**

### Current state (29E-8)

GitHub Actions CI is configured in `.github/workflows/ci.yml` for every push and pull request to `main`/`master`.

**CI stages:**

1. Restore (`dotnet restore MovieApp.sln`)
2. Release build of backend projects via `MovieApp.UnitTests` project graph
3. Release unit tests (`tests/MovieApp.UnitTests`)
4. Release publish of `MovieApp.Api`
5. Docker image build only (`docker build -f Dockerfile -t movieapp-api:ci .`)

**CI does not:**

- Deploy to any environment
- Push images to Docker Hub/GHCR
- Require PostgreSQL, Redis, TMDB, SMTP, or production JWT secrets
- Run EF Core migrations
- Modify local development behavior

Production deployment, cloud hosting, image registry push, and CD remain intentional later steps. Production secrets remain outside Git. EF migrations remain manual/operator/CI-controlled outside API startup.

### Integration test Release build note

Full-solution `dotnet build -c Release` still fails on pre-existing `CA1822` in `tests/MovieApp.IntegrationTests/Auth/AuthRateLimitApiTests.cs` (`ResetAsync`). CI intentionally builds the unit-test project graph only so Release validation is not blocked by that unrelated integration-test analyzer issue. Integration tests should continue to run locally when PostgreSQL test infrastructure is available.

### Minimum remaining V1 pipeline (post-CI)

```yaml
# Future CD (not part of 29E-8)
docker:
  - docker push registry/movieapp-api:$GIT_SHA

deploy:
  - deploy image to hosting platform
  - manual approval: run migrations
  - smoke test /health/ready
```

### Mobile CI (separate repo)

- `npm test`, `npm run lint`, `npx tsc --noEmit` (known pre-existing TS failure)
- EAS Build for production profiles

Recommend **GitHub Actions** for backend CI — now in place for build/test/publish/Docker validation.

---

## 24. Backup / Recovery

**Classification: HIGH**

### PostgreSQL backup strategy (V1)

| Item | Recommendation |
|------|----------------|
| Automatic backups | Enable on managed PostgreSQL (daily minimum) |
| Retention | 7–30 days for V1 |
| Point-in-time recovery | Enable if provider offers (Neon, RDS, Supabase Pro, etc.) |
| Backup encryption | Provider-managed at rest ✅ |
| Credentials | Separate from application secrets; ops-only access |
| Restore testing | Quarterly restore to staging + verify migrations |

### Redis backup

- Cache is **disposable** — no backup required for V1
- Redis AOF/RDB optional for faster warm-cache recovery — LOW priority

### Recovery procedure (document for ops)

1. Provision new PostgreSQL instance OR restore from backup snapshot
2. Update connection string secret
3. If fresh restore: verify schema version matches API migration history
4. Redeploy API with updated secret
5. Verify `/health/ready` and smoke tests
6. Redis: empty cache acceptable — repopulates on demand

**Do not run destructive backup/restore operations during this audit.**

---

## 25. Estimated V1 Cost

**Classification: MEDIUM (planning)**

> **Note:** Prices change frequently. Verify against official provider pricing before purchasing.

### Infrastructure (monthly, USD approximate)

| Item | Low estimate | Mid estimate | Notes |
|------|-------------|--------------|-------|
| Domain | $1/mo | $1/mo | ~$12/year (.com) |
| API hosting (Fly/Render) | $5 | $15 | 1 small instance |
| Managed PostgreSQL | $0–7 | $15 | Free tiers exist (Neon, Supabase) |
| Managed Redis | $0 | $10 | Upstash free tier; paid for reliability |
| Email (SMTP) | $0 | $15 | SendGrid/Mailgun free tiers limited |
| Image hosting | $0 | $0 | TMDB CDN (no direct cost) |
| Logging/monitoring | $0 | $10 | Platform logs included; optional Seq |
| **Infra subtotal** | **~$6–15** | **~$50–70** | |

### Store / licensing (annual or one-time)

| Item | Cost | Notes |
|------|------|-------|
| Apple Developer Program | $99/year | Required for iOS App Store |
| Google Play Console | $25 one-time | Required for Android |
| TMDB commercial licensing | TBD | Required before monetization; verify with TMDB |
| EAS Build (Expo) | $0–29/mo | Free tier may suffice initially |

### Realistic V1 total

- **Minimum viable:** ~$15–25/month infra + $99/year Apple + $25 Google one-time
- **Comfortable production:** ~$50–80/month infra + store fees

Do not assume enterprise-scale infrastructure costs.

---

## 26. Production Blockers

Summary of items that **must** be resolved before production launch:

| # | Blocker | Area |
|---|---------|------|
| 1 | Provision production hosting + HTTPS API endpoint | Infrastructure |
| 2 | Create API Dockerfile + deploy path | Infrastructure |
| 3 | Set up CI/CD (Release build must pass) | CI/CD |
| 4 | Configure production secrets (JWT, PostgreSQL, TMDB, SMTP) | Secrets |
| 5 | Add startup validation: JWT key required in Production | JWT |
| 6 | Add startup validation: PostgreSQL connection required | Database |
| 7 | Add startup validation: block `Provider=Fake` in Production | TMDB |
| 8 | Set `MovieProviders__Provider=Tmdb` + licensed credentials | TMDB |
| 9 | Configure EAS production env (`EXPO_PUBLIC_API_URL`, image base) | Mobile |
| 10 | Fix SMTP failure → HTTP 500 for existing emails | Email |
| 11 | Run EF migrations on empty production DB (controlled process) | Database |
| 12 | TMDB attribution in mobile app / store listing | Compliance |
| 13 | Fix Release build analyzer errors (CA1848/CA1873) | CI/CD |

---

## 27. Recommended Implementation Order

Suggested sequence for Step 29E+ (implementation, not this audit):

| Phase | Tasks | Priority |
|-------|-------|----------|
| **1. Hardening** | JWT ValidateOnStart; PostgreSQL ValidateOnStart; Fake provider blocked in Production; fix Release build warnings; global exception handler; SMTP failure → generic 200 | BLOCKER |
| **2. Containerize** | Add Dockerfile for `MovieApp.Api`; local docker-compose with API service for integration testing | BLOCKER |
| **3. CI/CD** | GitHub Actions: Release build, unit tests, Docker push | BLOCKER |
| **4. Provision infra** | Domain, managed PostgreSQL, managed Redis, container hosting | BLOCKER |
| **5. Secrets** | Configure all production secrets on hosting platform | BLOCKER |
| **6. Database** | Run migrations on empty production DB | BLOCKER |
| **7. Deploy API** | First deploy; verify `/health/ready`; smoke tests | BLOCKER |
| **8. Mobile config** | EAS production env vars; TMDB attribution screen | BLOCKER |
| **9. Email** | Configure SMTP provider; test forgot-password end-to-end | BLOCKER |
| **10. Observability** | Forwarded headers; AllowedHosts restriction; log drain review | HIGH |
| **11. Resilience** | Npgsql retry + SSL; require Redis in production config | HIGH |
| **12. Backups** | Enable automated PostgreSQL backups; document restore procedure | HIGH |
| **13. Store prep** | Screenshots with real TMDB images; privacy policy; store listings | HIGH |
| **14. Post-launch** | Monitor TMDB rate limits; optional catalog warm-cache; catalog rate limits if abused | MEDIUM |

---

## Appendix A — Test & Build Status (Audit Date)

Verified during this audit (no code changes):

| Check | Result |
|-------|--------|
| Debug `dotnet build` | ✅ Success (4 warnings in email senders) |
| Release `dotnet build` | ❌ Fails — CA1848/CA1873 treated as errors |
| Unit tests (`MovieApp.UnitTests`) | ✅ 300 passed |
| Integration tests | ❌ Require `PostgreSql__Password` / `POSTGRES_PASSWORD` |
| Mobile `npm test` | 302/303 (pre-existing forgot-password timeout) |
| Mobile `npx tsc --noEmit` | ❌ Pre-existing `process` / `@types/node` issue |
| Mobile lint | ✅ Passes |

---

## Appendix B — Key File Reference

| Concern | Path |
|---------|------|
| Pipeline | `src/MovieApp.Api/ApplicationBootstrap.cs` |
| Infrastructure DI | `src/MovieApp.Infrastructure/DependencyInjection.cs` |
| JWT auth | `src/MovieApp.Api/Authentication/JwtAuthenticationExtensions.cs` |
| Rate limiting | `src/MovieApp.Api/RateLimiting/AuthRateLimitExtensions.cs` |
| Forwarded headers | `src/MovieApp.Api/ForwardedHeaders/ForwardedHeadersExtensions.cs` |
| Password reset validation | `src/MovieApp.Infrastructure/Configuration/PasswordResetOptionsValidator.cs` |
| Forgot password service | `src/MovieApp.Application/Services/Identity/ForgotPasswordService.cs` |
| SMTP sender | `src/MovieApp.Infrastructure/Email/SmtpEmailSender.cs` |
| TMDB provider registration | `src/MovieApp.Infrastructure/Providers/MovieDataProviderServiceCollectionExtensions.cs` |
| Health controller | `src/MovieApp.Api/Controllers/HealthController.cs` |
| Docker compose | `docker-compose.yml` |
| Env template | `.env.example` |
| Mobile env validation | `MovieApp.Mobile/src/api/environment.ts` |
| Mobile image URLs | `MovieApp.Mobile/src/utils/image-url.ts` |
| EAS config | `MovieApp.Mobile/eas.json` |

---

*End of Production Readiness Audit — Step 29D. No deployment performed. No secrets included.*

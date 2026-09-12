# MovieApp V1 Feature Audit

**Date:** September 2026  
**Scope:** Backend (`MovieApp`) + Mobile (`MovieApp.Mobile`)  
**Purpose:** Determine what must be implemented before the first public commercial release.  
**Status:** Audit / planning only — no implementation in this step.

---

## 1. Executive Summary

MovieApp is a **movie and TV tracking application** with a mature feature set: authentication, personalized home, search, discovery, detail pages, favorites, watchlists, ratings, reviews, watch history with TV progress, recommendations, profile management, account deletion, a mobile design system, and EAS production configuration scaffolding.

**The product is closer to launch-ready than a typical greenfield app**, but several gaps would block a credible public release:

| Category | Assessment |
|----------|------------|
| **Core tracking flows** | Largely complete on backend and mobile |
| **Production catalog quality** | **Partially addressed (Step 29A):** TMDB TV provider implemented; production still requires `MovieProviders:Provider=Tmdb` + credentials |
| **Account recovery** | **Implemented:** Forgot-password + reset-password flow with mobile screens |
| **Store / legal compliance** | **Blocker:** Privacy policy URL, support contact, store assets, production API URL not in repo |
| **Operational readiness** | **Partial:** Auth rate limiting implemented; crash/error monitoring and production analytics baseline still missing |
| **Deferred roadmap items** | Mostly correctly deferrable (social, notifications, Elasticsearch, monetization, where-to-watch) |

**Recommended V1 posture:** Ship a focused **authenticated tracking app** with real TMDB-backed catalog data (movies + TV), password recovery, basic operational monitoring, store compliance artifacts, and a small set of mobile UX gaps (favorites list, advanced search UI, continue-watching polish). Do **not** attempt to implement the full deferred roadmap before launch.

**Bottom line:** ~85% of core product functionality exists. The remaining ~15% is concentrated in **production infrastructure, account security/recovery, real TV catalog ingestion, store compliance, and a handful of mobile UX completeness items**.

---

## 2. Current Product Capability

### Backend (72 API endpoints)

| Area | Status | Notes |
|------|--------|-------|
| Auth (register/login/me) | ✅ Implemented | JWT 60 min, PBKDF2 passwords, security stamp invalidation |
| Profile & account deletion | ✅ Implemented | Email/password change, statistics, cascade delete |
| Movies catalog | ✅ Implemented | TMDB on-demand ingestion + PostgreSQL persistence |
| TV catalog | ✅ Implemented (TMDB when `Provider=Tmdb`; Fake for dev/tests) | TMDB TV provider added in Step 29A; TVDB not implemented |
| Search (unified) | ✅ Implemented | Genre, year, rating, sort filters in API |
| Provider search | ✅ Implemented | `/api/movies/search`, `/api/tvshows/search` |
| Search history | ✅ Implemented | Authenticated |
| Discovery (popular/trending) | ✅ Implemented | Public endpoints |
| Home feed | ✅ Implemented | Personalized sections, continue watching, discovery |
| Recommendations | ✅ Implemented | Rule-based v1, similar content, personalized |
| Favorites | ✅ Implemented | Add/remove/list |
| Watchlists | ✅ Implemented | Full CRUD + items |
| Ratings | ✅ Implemented | Movie + TV show level (1–10) |
| Reviews | ✅ Implemented | CRUD, public lists, one per user per title |
| Watch history | ✅ Implemented | Movies, episodes, recent, TV/season progress |
| Continue watching | ⚠️ Partial | TV partial progress only; no movies; home-only |
| Redis caching | ✅ Implemented | With in-memory fallback |
| Health checks | ✅ Implemented | PostgreSQL + Redis |
| Tests | ✅ Strong | Unit + integration coverage for implemented areas |

### Mobile (265 tests, 73 suites)

| Area | Status | Notes |
|------|--------|-------|
| Auth (login/register/logout) | ✅ Implemented | SecureStore JWT, route guard, 401 handling |
| Home | ✅ Implemented | Sections, type filter, refresh |
| Search (basic) | ✅ Implemented | Query, autocomplete, type filter, history |
| Advanced search filters | ❌ Missing UI | Backend supports; mobile passes `sort` only |
| Discover | ✅ Implemented | Trending/popular + recommendation sections |
| Movie/TV/season/episode details | ✅ Implemented | Actions: favorite, rating, review, watchlist, watched |
| Favorites toggle | ✅ Implemented | On detail pages only |
| Favorites list | ✅ Implemented | Profile → Favorites; paginated list with remove |
| Watchlists | ✅ Implemented | Tab + CRUD + picker |
| Ratings | ✅ Implemented | 1–10 on detail pages |
| Reviews | ✅ Implemented | List + composer on detail pages |
| Watch history | ✅ Implemented | Recent list + progress sections |
| Continue watching | ⚠️ Partial | Home section only; no progress/next-episode UI |
| Recommendations | ⚠️ Partial | Similar + discover/home sections; no paginated feed screen |
| Profile & account deletion | ✅ Implemented | Edit, email, password, delete with confirmation |
| Design system | ✅ Implemented | Theme tokens, loading/empty/error states |
| Deep linking (custom scheme) | ✅ Implemented | `movieapp://` + file routes |
| Universal links | ❌ Missing | Requires owned domain |
| Production config | ⚠️ Partial | EAS profiles, env validation; credentials/URLs TODO |
| Guest browse | ❌ Not supported | Global auth guard despite public API endpoints |

### Documentation

| Document | Location | Status |
|----------|----------|--------|
| Backend README | `MovieApp/README.md` | Comprehensive |
| Mobile API context | `docs/MOBILE_API_CONTEXT.md` (both repos) | 72 endpoints documented |
| Release checklist | `MovieApp.Mobile/docs/RELEASE_CHECKLIST.md` | Store TODOs tracked |
| Versioning | `MovieApp.Mobile/docs/VERSIONING.md` | Semver + build numbers |

---

## 3. V1 Required Features

Features the app **should not go public without**.

### 3.1 Production backend deployment + HTTPS API URL

| Field | Detail |
|-------|--------|
| **Current status** | Backend runs locally; mobile production URL is a placeholder in `.env.example`; EAS env var required |
| **Classification** | 🟢 V1 REQUIRED |
| **Why** | Mobile production builds reject localhost/LAN; stores require a working production backend |
| **User/business value** | App functions for real users |
| **Backend impact** | Deploy API, PostgreSQL, Redis; configure JWT signing key, TMDB key, CORS |
| **Mobile impact** | Set `EXPO_PUBLIC_API_URL` in EAS production env |
| **Dependencies** | Hosting, TLS certificate, database migration strategy |
| **Complexity** | Medium |
| **Store importance** | Critical |
| **Risks if postponed** | Cannot ship |
| **Recommendation** | Deploy backend first; validate with preview EAS builds before store submission |

### 3.2 Real TV catalog ingestion (TMDB TV)

| Field | Detail |
|-------|--------|
| **Current status** | ✅ **Implemented (Step 29A)** — `TmdbTvShowDataProvider` registered when `MovieProviders:Provider=Tmdb` |
| **Classification** | 🟢 V1 REQUIRED |
| **Why** | A public movie/TV app cannot ship with synthetic TV catalog data |
| **User/business value** | Trust, discoverability, accurate metadata |
| **Backend impact** | Complete — uses shared `TmdbApiClient`, lazy upsert, existing TV services unchanged |
| **Mobile impact** | None (API contracts unchanged) |
| **Dependencies** | TMDB API key / read access token in production configuration |
| **Complexity** | High (completed) |
| **Store importance** | Critical (product credibility) |
| **Risks if postponed** | App Store / Play rejection risk; user churn |
| **Recommendation** | Enable `Provider=Tmdb` with licensed TMDB credentials in production |

### 3.3 Password reset (forgot password)

| Field | Detail |
|-------|--------|
| **Current status** | ✅ **IMPLEMENTED** — `POST /api/auth/forgot-password`, `POST /api/auth/reset-password`, SHA-256 token storage, SecurityStamp invalidation, mobile screens |
| **Classification** | 🟢 V1 REQUIRED |
| **Why** | Standard consumer expectation; store reviewers and users expect account recovery |
| **User/business value** | Account recovery without support tickets |
| **Backend impact** | Reset token entity, email sender, `POST /forgot-password`, `POST /reset-password` |
| **Mobile impact** | Forgot-password screen + deep link or token entry |
| **Dependencies** | Email delivery (SMTP/SendGrid/etc.) |
| **Complexity** | Medium |
| **Store importance** | High |
| **Risks if postponed** | Support burden, bad reviews, perceived insecurity |
| **Recommendation** | Implement minimal email-link reset flow |

### 3.4 Auth endpoint rate limiting

| Field | Detail |
|-------|--------|
| **Current status** | ✅ **IMPLEMENTED** — ASP.NET Core fixed-window rate limiting on auth endpoints with configurable limits and `429` responses |
| **Classification** | 🟢 V1 REQUIRED |
| **Why** | Public login/register endpoints are abuse vectors without throttling |
| **User/business value** | Protects service availability |
| **Backend impact** | ASP.NET rate limiter on auth routes |
| **Mobile impact** | Handle 429 responses gracefully |
| **Dependencies** | None |
| **Complexity** | Low |
| **Store importance** | Medium (security posture) |
| **Risks if postponed** | Brute-force attacks, credential stuffing, API cost spikes |
| **Recommendation** | Add per-IP limits on register/login before public launch |

### 3.5 Privacy policy URL + support contact

| Field | Detail |
|-------|--------|
| **Current status** | Not in repo (correct — legal text should not be invented here) |
| **Classification** | 🟢 V1 REQUIRED |
| **Why** | Mandatory for Google Play and App Store |
| **User/business value** | Legal compliance, user trust |
| **Backend impact** | Optional hosted page or external URL |
| **Mobile impact** | Link from profile/settings; App Store Connect / Play Console fields |
| **Dependencies** | Legal review, hosted webpage |
| **Complexity** | Low (non-engineering) |
| **Store importance** | Critical |
| **Risks if postponed** | Store submission blocked |
| **Recommendation** | Prepare externally; add in-app link only after URL exists |

### 3.6 Store signing credentials + listing assets

| Field | Detail |
|-------|--------|
| **Current status** | EAS profiles ready; credentials and screenshots TODO |
| **Classification** | 🟢 V1 REQUIRED |
| **Why** | Cannot submit without signing and store metadata |
| **Backend impact** | None |
| **Mobile impact** | `eas credentials`, screenshots, descriptions |
| **Dependencies** | Apple Developer + Google Play accounts |
| **Complexity** | Medium (operational) |
| **Store importance** | Critical |
| **Risks if postponed** | Cannot publish |
| **Recommendation** | Follow `RELEASE_CHECKLIST.md` |

### 3.7 Image base URL for production posters

| Field | Detail |
|-------|--------|
| **Current status** | Optional `EXPO_PUBLIC_IMAGE_BASE_URL`; placeholders without it |
| **Classification** | 🟢 V1 REQUIRED |
| **Why** | Poster-less catalog UI is not shippable quality |
| **User/business value** | Visual discovery and recognition |
| **Backend impact** | May need CDN or TMDB image base documented |
| **Mobile impact** | Set EAS env var |
| **Dependencies** | Image hosting strategy (TMDB CDN URLs or backend proxy) |
| **Complexity** | Low |
| **Store importance** | High (store screenshots depend on it) |
| **Risks if postponed** | Poor UX, low store conversion |
| **Recommendation** | Configure before marketing screenshots |

### 3.8 Favorites list screen (mobile)

| Field | Detail |
|-------|-------|
| **Current status** | ✅ Implemented — `/favorites` screen with pagination, remove, cache sync |
| **Classification** | 🟢 V1 REQUIRED |
| **Why** | Core library feature incomplete without browse/manage view |
| **User/business value** | Users expect to see saved titles |
| **Backend impact** | None (API exists) |
| **Mobile impact** | New screen + profile navigation link |
| **Dependencies** | None |
| **Complexity** | Low |
| **Store importance** | Medium |
| **Risks if postponed** | Feature feels broken; inefficient status resolution workaround |
| **Recommendation** | Add paginated favorites screen using existing API |

---

## 4. V1 Recommended Features

Strongly improve the initial product; can technically launch without, but should be planned immediately after required items.

### 4.1 Email verification

| Field | Detail |
|-------|--------|
| **Current status** | Missing |
| **Classification** | 🟡 V1 RECOMMENDED |
| **Why** | Reduces spam accounts and invalid emails; not always enforced at V1 |
| **Complexity** | Medium |
| **Recommendation** | Implement if abuse appears in beta; defer only with monitoring |

### 4.2 Crash / error monitoring

| Field | Detail |
|-------|--------|
| **Current status** | Missing (no Sentry/Crashlytics/etc.) |
| **Classification** | 🟡 V1 RECOMMENDED |
| **Why** | Blind to production failures without it |
| **Complexity** | Low–Medium |
| **Recommendation** | Add before wide release; acceptable for closed beta without |

### 4.3 Basic product analytics

| Field | Detail |
|-------|--------|
| **Current status** | Missing |
| **Classification** | 🟡 V1 RECOMMENDED |
| **Why** | Need funnel/retention data post-launch; not a user-facing feature |
| **Complexity** | Low |
| **Recommendation** | Minimal event set: register, login, favorite, rate, review, watch mark |

### 4.4 Advanced search filter UI (mobile)

| Field | Detail |
|-------|--------|
| **Current status** | Backend supports genre/year/rating/sort; mobile has type filter only |
| **Classification** | 🟡 V1 RECOMMENDED |
| **Why** | Differentiator for discovery; basic search still works |
| **Complexity** | Medium |
| **Recommendation** | Ship V1 with basic search if timeline tight; add filters in first update |

### 4.5 Continue watching UX polish

| Field | Detail |
|-------|--------|
| **Current status** | Home section renders generic cards; no next-episode/progress indicator |
| **Classification** | 🟡 V1 RECOMMENDED |
| **Why** | Continue watching is a key retention surface for TV trackers |
| **Complexity** | Low (mobile UI only) |
| **Recommendation** | Add next-episode label and progress chip on home cards |

### 4.6 Session expiry UX (JWT re-login)

| Field | Detail |
|-------|--------|
| **Current status** | 60-min JWT, no refresh tokens; 401 clears session |
| **Classification** | 🟡 V1 RECOMMENDED |
| **Why** | Usable but abrupt; refresh tokens are larger scope |
| **Complexity** | Low (friendly message) to High (refresh tokens) |
| **Recommendation** | V1: clear "session expired" messaging; V1.1: refresh tokens |

### 4.7 Cast/crew ingestion from TMDB

| Field | Detail |
|-------|--------|
| **Current status** | Schema exists; genres synced; cast not populated |
| **Classification** | 🟡 V1 RECOMMENDED |
| **Why** | Degrades recommendation quality and detail richness |
| **Complexity** | Medium |
| **Recommendation** | Populate during movie/TV upsert |

### 4.8 Catalog freshness policy

| Field | Detail |
|-------|--------|
| **Current status** | On-demand upsert only; stale DB reads with no re-sync |
| **Classification** | 🟡 V1 RECOMMENDED |
| **Why** | Metadata can become outdated |
| **Complexity** | Medium |
| **Recommendation** | Re-fetch if `UpdatedAt` older than N days on detail read |

### 4.9 Lightweight onboarding

| Field | Detail |
|-------|--------|
| **Current status** | Missing — straight to login/home |
| **Classification** | 🟡 V1 RECOMMENDED |
| **Why** | Helps first-time users understand tracking value |
| **Complexity** | Low |
| **Recommendation** | 2–3 screen carousel; skip allowed |

### 4.10 Network offline banner

| Field | Detail |
|-------|--------|
| **Current status** | Network errors per-screen; no global offline indicator |
| **Classification** | 🟡 V1 RECOMMENDED |
| **Why** | Reduces confusion when API unreachable |
| **Complexity** | Low |
| **Recommendation** | NetInfo banner; no offline cache required for V1 |

### 4.11 Terms of service URL

| Field | Detail |
|-------|--------|
| **Current status** | Missing |
| **Classification** | 🟡 V1 RECOMMENDED |
| **Why** | Often required alongside privacy policy |
| **Complexity** | Low (non-engineering) |
| **Recommendation** | Prepare with legal review |

---

## 5. V1.1 / Post-Launch Features

Useful features to **deliberately postpone** until after initial public release.

| Feature | Current Status | Why Post-Launch | Complexity |
|---------|----------------|-----------------|------------|
| **Refresh tokens** | Missing | Re-login works; security stamp handles credential rotation | High |
| **Where to Watch / streaming availability** | Missing entirely | Tracking app core; licensing/provider complexity | High |
| **Playback position / resume timestamps** | Missing | Binary watched state sufficient for V1 tracker | High |
| **Episode/season user ratings** | Missing | Show-level ratings exist | Medium |
| **Review likes / replies / reporting** | Missing | Basic reviews sufficient for V1 community | High |
| **Notifications (push)** | Missing | No engagement loop requiring push yet | High |
| **Social feed / following** | Missing | Not core to personal tracking | High |
| **Moderation / admin tools** | Missing | Low volume at launch; manual DB if needed | High |
| **Elasticsearch** | Interface-ready; PostgreSQL only | Current catalog size manageable with ILIKE search | High |
| **Background catalog sync jobs** | Missing | On-demand upsert works at small scale | Medium |
| **Universal links / associated domains** | Missing | Custom scheme sufficient initially | Medium |
| **Guest browse mode** | Mobile auth-gated | Account-required model is valid for V1 | Low |
| **Paginated recommendations feed screen** | Hook exists, no screen | Home + discover cover basics | Low |
| **Dedicated continue-watching screen** | Home section only | Home section adequate for V1 | Low |
| **Score distribution UI** | API provides; UI doesn't | Nice detail polish | Low |
| **Data export (GDPR portability)** | Missing | Account deletion exists; export can follow | Medium |
| **Monetization / ads / premium** | Missing | No business model requirement identified | High |

---

## 6. Not Needed Now

Do not implement before V1 unless a concrete business or compliance reason emerges.

| Feature | Current Status | Classification | Rationale |
|---------|----------------|----------------|-----------|
| **Elasticsearch** | Not implemented | 🔴 NOT NEEDED NOW | PostgreSQL search adequate for launch scale |
| **Social feed / following** | Not implemented | 🔴 NOT NEEDED NOW | Personal tracker, not social network |
| **Push notifications** | Not implemented | 🔴 NOT NEEDED NOW | No notification-worthy events in V1 product |
| **Review likes/replies/reporting** | Not implemented | 🔴 NOT NEEDED NOW | Adds moderation burden without V1 need |
| **Episode/season ratings** | Not implemented | 🔴 NOT NEEDED NOW | Show-level ratings cover primary use case |
| **Where to Watch** | Not implemented | 🔴 NOT NEEDED NOW | Different product surface; TMDB licensing separate |
| **Playback position** | Not implemented | 🔴 NOT NEEDED NOW | No video player; tracking ≠ streaming |
| **Monetization / ads / premium** | Not implemented | 🔴 NOT NEEDED NOW | No revenue model defined |
| **Onboarding (elaborate)** | Not implemented | 🔴 NOT NEEDED NOW | Simple optional carousel sufficient if any |
| **Advanced search (if basic ships)** | Backend ready | 🔴 NOT NEEDED NOW | Only if basic search deemed enough for day one |
| **Analytics (if crash monitoring only)** | Not implemented | 🔴 NOT NEEDED NOW | Crash monitoring higher priority than funnels |
| **TVDB provider** | Config stub only | 🔴 NOT NEEDED NOW | TMDB TV likely sufficient first |
| **Collaborative filtering / ML recommendations** | Rule-based v1 | 🔴 NOT NEEDED NOW | Current engine is adequate foundation |
| **Certificate pinning** | Not implemented | 🔴 NOT NEEDED NOW | HTTPS + standard TLS sufficient for V1 |
| **Biometric login** | Not implemented | 🔴 NOT NEEDED NOW | Convenience feature |

---

## 7. Production / Store Blockers

### True blockers (must resolve before public release)

| # | Blocker | Owner | Notes |
|---|---------|-------|-------|
| 1 | Production HTTPS API deployed | Backend/DevOps | Set EAS `EXPO_PUBLIC_API_URL` |
| 2 | TMDB TV catalog (`Provider=Tmdb` + credentials) | Backend/DevOps | Implementation complete; configuration required |
| 3 | Password reset flow | Backend + Mobile | Account recovery |
| 4 | Auth rate limiting | Backend | Brute-force protection |
| 5 | Privacy policy URL | Legal/Ops | Store mandatory |
| 6 | Support email/URL | Ops | Store mandatory |
| 7 | Store screenshots + descriptions | Design/Ops | Both platforms |
| 8 | Signing credentials (EAS) | Mobile/DevOps | `eas credentials` |
| 9 | Production `EXPO_PUBLIC_IMAGE_BASE_URL` | Mobile/DevOps | Poster display |
| 10 | Final bundle IDs / package name decision | Product | Currently `com.movieapp.mobile` placeholder |
| 11 | TMDB API terms compliance | Legal/Ops | Attribution, usage limits |
| 12 | Database backup/recovery plan | DevOps | Production PostgreSQL |
| 13 | JWT signing key in secure production config | DevOps | Not in source control |

### Non-blockers (often confused with blockers)

| Item | Why not a blocker |
|------|-------------------|
| Elasticsearch | PostgreSQL search works at launch scale |
| Refresh tokens | Re-login acceptable with good UX |
| Email verification | Recommended, not always required day one |
| Notifications | No product feature depends on push |
| Social features | Out of scope for tracking V1 |
| Universal links | Custom scheme works for V1 |
| Offline mode | Network-required app is acceptable |
| Analytics SDK | Recommended but not submission-blocking |
| Onboarding | Optional polish |
| Where to Watch | Not core to tracking |

---

## 8. Security & Account Gaps

| Gap | Severity | V1 Action |
|-----|----------|-----------|
| No password reset | **High** | 🟢 Required |
| No email verification | Medium | 🟡 Recommended |
| No refresh tokens | Medium | 🔵 V1.1 |
| No rate limiting | **High** | 🟢 Required |
| 60-min JWT expiry | Low | 🟡 UX messaging |
| JWT in SecureStore | ✅ OK | Maintain |
| Passwords not logged | ✅ OK | Maintain |
| Security stamp invalidation | ✅ OK | Maintain |
| Account deletion with password confirm | ✅ OK | Document for stores |
| Generic login failure messages | ✅ OK | Maintain |
| No certificate pinning | Low | Accept for V1 |
| CORS configuration for production | Medium | Verify on deploy |
| TMDB API key exposure | Low | Server-side only ✅ |
| Review spam / abuse | Medium | Rate limits + future reporting |
| `EXPO_PUBLIC_*` secrets discipline | ✅ OK | Documented |

---

## 9. Commercial / Monetization Gaps

| Area | Status | V1 Decision |
|------|--------|-------------|
| Revenue model | Undefined | No monetization in V1 |
| Ads | Not implemented | 🔴 Not needed |
| Premium/subscription | Not implemented | 🔴 Not needed |
| Paywall | Not implemented | 🔴 Not needed |
| Analytics | Not implemented | 🟡 Recommended (operational) |
| Crash monitoring | Not implemented | 🟡 Recommended |
| Privacy policy | Missing URL | 🟢 Required |
| Terms of service | Missing URL | 🟡 Recommended |
| App Store privacy nutrition labels | Not prepared | 🟢 Required at submission |
| Play Data safety form | Not prepared | 🟢 Required at submission |
| Account deletion | ✅ Client + API ready | Document in store listing |
| Third-party disclosure (TMDB, backend) | Partially documented | Complete in store forms |

---

## 10. Technical / Infrastructure Gaps

| Component | Current | V1 Need | Priority |
|-----------|---------|---------|----------|
| **PostgreSQL** | ✅ Implemented | Production instance + migrations | 🟢 Required |
| **Redis** | ✅ Implemented with fallback | Production instance recommended | 🟡 Recommended |
| **Elasticsearch** | Not implemented | Not needed | 🔴 Defer |
| **TMDB movies** | ✅ On-demand | Production API key | 🟢 Required |
| **TMDB TV** | ✅ Implemented | Production config required | 🟢 Required |
| **TVDB** | Config stub | Defer | 🔴 Defer |
| **Background jobs** | None | Defer | 🔵 V1.1 |
| **Caching** | Redis TTL | Adequate for V1 | ✅ OK |
| **Logging** | Serilog console | Add production log aggregation | 🟡 Recommended |
| **Monitoring/alerting** | Health endpoints only | Uptime + error alerts | 🟡 Recommended |
| **API performance** | Acceptable for V1 scale | Load test before launch | 🟡 Recommended |
| **Cast ingestion** | Schema only | Recommended | 🟡 Recommended |
| **Catalog re-sync** | None | Recommended | 🟡 Recommended |
| **EAS build config** | ✅ Ready | Credentials + env vars | 🟢 Required |
| **Mobile env validation** | ✅ Production HTTPS check | Maintain | ✅ OK |

---

## 11. Dependency Map

```
Production HTTPS API URL
  → requires Backend deployment (PostgreSQL, Redis, JWT, TMDB key)
  → requires Mobile EAS env (EXPO_PUBLIC_API_URL, EXPO_PUBLIC_APP_ENV=production)
  → requires TLS + CORS

TMDB TV catalog
  → requires Backend TMDB TV provider (ITvShowDataProvider implementation)
  → requires TMDB API key + terms compliance
  → enables Mobile TV detail/season/episode (no contract change)

Password reset
  → requires Backend email sender + reset token storage + 2 endpoints
  → requires Mobile forgot-password + reset screens
  → depends on Email delivery service (SendGrid/SMTP)

Auth rate limiting
  → requires Backend middleware only
  → optionally Mobile 429 error handling

Favorites list screen
  → requires Mobile UI only
  → uses existing GET /api/favorites

Advanced search filters
  → requires Mobile UI (genre picker, year, rating range, sort)
  → uses existing GET /api/search parameters
  → optionally Backend genre list endpoint (genres in search results today)

Continue watching polish
  → requires Mobile home card UI
  → uses existing Home API ContinueWatching section (TV progress data)

Crash monitoring
  → requires Mobile SDK (e.g., Sentry)
  → optionally Backend error tracking

Privacy policy / support URL
  → requires Legal/Ops hosted pages
  → requires Mobile profile links

Store submission
  → requires EAS credentials
  → requires screenshots (need image base URL working)
  → requires privacy/support URLs
  → requires production API live

Where to Watch (deferred)
  → would require Backend provider integration (TMDB watch/providers)
  → would require Mobile availability UI
  → would require licensing review

Refresh tokens (deferred)
  → would require Backend token store + rotation
  → would require Mobile silent refresh logic
  → would change auth architecture

Elasticsearch (deferred)
  → would replace ISearchRepository implementation
  → no Mobile impact
```

---

## 12. Recommended Pre-Launch Order

### Phase A — Critical product gaps
1. Implement TMDB TV provider (replace Fake TV data)
2. Deploy production backend (HTTPS, PostgreSQL, Redis, secrets)
3. Configure mobile production env vars (API URL, image base URL)
4. Add favorites list screen (mobile)
5. Validate end-to-end on preview EAS builds

### Phase B — Security / account
6. Implement password reset (backend + mobile)
7. Add auth rate limiting (backend)
8. Add session-expired UX (mobile)
9. Optionally add email verification if timeline allows

### Phase C — Commercial / analytics
10. Add crash/error monitoring (mobile minimum)
11. Add basic analytics events (optional for closed beta)
12. Prepare privacy policy + support URL (legal/ops)
13. Prepare terms of service (recommended)

### Phase D — Production hardening
14. Production logging/alerting on backend
15. Database backup/recovery verification
16. Load/smoke test production API
17. Catalog freshness policy (re-sync stale titles)
18. Cast/crew ingestion (if time permits)

### Phase E — Store compliance
19. Finalize bundle IDs / package name
20. Create store screenshots (with real posters)
21. Complete App Store privacy labels + Play Data safety form
22. Document account deletion in store listings
23. Configure EAS signing credentials

### Phase F — Release candidate
24. Run full quality gate (265+ tests, lint, typecheck, config validate)
25. EAS production builds (Android AAB + iOS)
26. Internal TestFlight / Play internal testing
27. Fix RC bugs only — no new features
28. Store submission

---

## 13. Final V1 Scope

### What exactly should we implement before the first public release?

**Implement (engineering):**
1. TMDB TV catalog provider (backend)
2. Production backend deployment + HTTPS
3. Password reset flow (backend + mobile)
4. Auth rate limiting (backend)
5. Favorites list screen (mobile)
6. Production env configuration (EAS + image base URL)
7. Crash/error monitoring (mobile, strongly advised)
8. Continue-watching card polish (mobile, if time permits)
9. Advanced search filter UI (mobile, if time permits)

**Prepare (non-engineering / ops):**
10. Privacy policy URL
11. Support contact URL/email
12. Store screenshots and listing copy
13. Signing credentials
14. TMDB attribution/compliance review
15. Database backup strategy

**Do NOT implement before V1:**
Elasticsearch, social features, notifications, where-to-watch, playback position, review likes/replies, episode ratings, monetization, refresh tokens (unless schedule allows), universal links

---

### Summary table

| Feature | Status | Priority | Decision |
|---------|--------|----------|----------|
| Authentication (login/register) | ✅ Implemented | — | Ship as-is |
| Password reset | ✅ Implemented | 🟢 Required | Ship as-is |
| Email verification | ❌ Missing | 🟡 Recommended | Implement if time; monitor abuse |
| Refresh tokens | ❌ Missing | 🔵 V1.1 | Defer; improve session UX instead |
| Rate limiting | ✅ Implemented | 🟢 Required | Ship as-is |
| Home feed | ✅ Implemented | — | Ship as-is |
| Search (basic) | ✅ Implemented | — | Ship as-is |
| Advanced search filters | ⚠️ Backend only | 🟡 Recommended | Mobile UI if time permits |
| Movie/TV details | ✅ Implemented | — | Ship as-is |
| TMDB movies | ✅ Implemented | — | Ship with production key |
| TMDB TV catalog | ✅ Implemented | 🟢 Required | **Configure `Provider=Tmdb` in production** |
| Favorites toggle | ✅ Implemented | — | Ship as-is |
| Favorites list | ✅ Implemented | — | Ship as-is |
| Watchlists | ✅ Implemented | — | Ship as-is |
| Ratings | ✅ Implemented | — | Ship as-is |
| Reviews | ✅ Implemented | — | Ship as-is |
| Watch history | ✅ Implemented | — | Ship as-is |
| Continue watching | ⚠️ Partial | 🟡 Recommended | Polish home cards |
| Recommendations | ✅ Implemented | — | Ship as-is |
| Discovery (trending/popular) | ✅ Implemented | — | Ship as-is |
| Profile & account deletion | ✅ Implemented | — | Ship as-is |
| Where to Watch | ❌ Missing | 🔵 V1.1 | Defer |
| Playback position | ❌ Missing | 🔵 V1.1 | Defer |
| Onboarding | ❌ Missing | 🟡 Recommended | Simple optional flow |
| Notifications | ❌ Missing | 🔵 V1.1 | Defer |
| Social feed / following | ❌ Missing | 🔴 Not needed | Defer |
| Review likes/replies | ❌ Missing | 🔵 V1.1 | Defer |
| Episode/season ratings | ❌ Missing | 🔵 V1.1 | Defer |
| Elasticsearch | ❌ Missing | 🔴 Not needed | Defer |
| Analytics | ❌ Missing | 🟡 Recommended | Add before wide release |
| Crash monitoring | ❌ Missing | 🟡 Recommended | Add before wide release |
| Monetization | ❌ Missing | 🔴 Not needed | Defer |
| Deep linking (custom scheme) | ✅ Implemented | — | Ship as-is |
| Universal links | ❌ Missing | 🔵 V1.1 | Defer |
| Production EAS config | ⚠️ Partial | 🟢 Required | Complete credentials + env |
| Privacy policy URL | ❌ Missing | 🟢 Required | **Prepare before submission** |
| Support URL | ❌ Missing | 🟢 Required | **Prepare before submission** |
| Store assets | ❌ Missing | 🟢 Required | **Prepare before submission** |
| Image base URL | ⚠️ Optional today | 🟢 Required | **Configure for production** |
| Guest browse | ❌ Not supported | 🔵 V1.1 | Defer (auth-required V1 OK) |

---

*This document is planning-only. No features were implemented as part of Step 28.*

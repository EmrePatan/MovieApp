# MovieApp — Product & Engineering Roadmap

**Last updated:** 2026-09-15 (TV upcoming episodes v1)  
**Backend baseline:** see latest `origin/master`  
**Mobile baseline:** see latest `origin/master`

## Related document

| Document | Purpose |
|---|---|
| [PRODUCTION-LAUNCH-CHECKLIST.md](./PRODUCTION-LAUNCH-CHECKLIST.md) | Operational release gate — what must be verified to ship |
| **This document** | Product/engineering sequencing and status — what exists, what is next, what is blocked |

Neither document replaces code or tests.

---

## Status legend

| Status | Meaning |
|---|---|
| **DONE** | Shipped and accepted in the current product baseline |
| **IN PROGRESS** | Active work or validation underway |
| **NEXT** | Planned next product/engineering focus after current in-progress items |
| **BLOCKED** | Cannot proceed until an external or operational dependency is resolved |
| **BEFORE PRODUCTION** | Required engineering/ops work before store launch |
| **LATER / BACKLOG** | Intentionally deferred; not current execution priority |

Do not use vague completion percentages.

---

## Current product identity

Accepted information architecture — do not undo without an explicit product decision.

### Home = personal landing page

**All users:**
- Hot This Week hero (up to 5, TMDB weekly trending)
- Trending Now (10, local catalog sort by vote count)
- Top Rated (Bayesian-weighted catalog ranking)
- New Releases

**Personalized (warm user):**
- Recommended For You (10)

**Cold (low signal):**
- Welcome
- Explore CTA

### Search tab = Search + premium Explore landing while idle

**Idle Explore:**
- Explore by Genre

Search tab remains **Search**. Do **not** add a separate Explore bottom tab.

### Discover = secondary filtered/browse listing

Reached from Explore, genres, See All, and deep links. Provider-backed browse with filters.

### Profile = account + My Library

**My Library:**
- Favorites
- Watchlist
- Watch History
- Following

Do **not** create a new Library tab.

### Important IA decision (do not revert)

Home owns the primary browse rails (Hot This Week hero, Recommended For You, Trending Now, Top Rated, New Releases). Search idle Explore keeps genre entry points only; filtered browse remains in Discover. Do **not** restore the old overloaded Home with Popular, Continue Watching, Because You Watched, and genre fanout rails.

---

## DONE — Core platform

- .NET backend / Clean Architecture foundation
- PostgreSQL persistence (EF Core)
- Redis caching / distributed coordination
- Serilog structured logging
- Swagger / OpenAPI (Development environment)
- JWT authentication with `SecurityStamp` revocation support
- Hangfire + PostgreSQL job storage infrastructure
- TMDB provider abstraction (`MovieProviders:Provider`)
- Lazy catalog ingestion on demand
- GUID public catalog IDs with TMDB external identity
- Movie / TV catalog foundation
- Ratings
- Favorites
- Watchlists
- Watch history
- Search history
- Health checks (`/health`, `/health/ready`)

**Auth note:** No refresh-token flow. Current auth intentionally uses access tokens + security stamp; do not assume refresh tokens exist.

---

## DONE — Home Composition v2

- Hot This Week hero from TMDB weekly trending (`/trending/all/week`, movie + TV only, cached)
- Recommended For You (10, independent from hero; Recommendation 2.1 unchanged)
- Trending Now on Home (10, reuses existing local catalog trending via `IDiscoveryService.GetTrendingAsync`)
- Top Rated (10, Bayesian-weighted catalog ranking; no provider calls; Home rail caps Animation catalog genre to 3 via post-ranking diversity)
- New Releases on Home (10, existing regional-release semantics for movies)
- Search idle cleanup (Trending Now moved to Home; Top Rated / New Releases browse rails removed from Search landing)

## DONE — Discovery / Search / Home

Premium Home / Search / Explore / Discover information architecture.

- Personalized Home and cold Home
- Recommended For You
- Trending, Top Rated, New Releases
- Explore by Genre
- Search (movies / TV) and unified `/api/search`
- Search autocomplete
- Genre catalog API for filters
- Search history
- Discover browse with content-type, genre, year, rating, language, and sort filters
- Provider-backed Discover browse
- Local Explore preview
- Pagination and caching at a high level (Home TTL, recommendation cache, search provider refresh controls)

---

## DONE — Follow / release notifications

Generic **Catalog Follow** with release-notification pipeline.

**TV follow:**
- Follow show
- Notify New Seasons
- Notify New Episodes

**Movie follow:**
- Follow upcoming movie
- Notify when released

**Pipeline:**
- `CatalogReleaseEvent`
- User release notifications
- Fanout → delivery preparation → Expo dispatch → Expo ticket → receipt processing
- Notification dedupe / idempotency
- Notification center (inbox, unread, mark read, mark all read)
- Push device registration API (`/api/push-devices`) and mobile token registration
- Push tap ownership validation

**Distinctions (do not conflate):**
- Follow ≠ Favorite
- Follow ≠ Watchlist
- Follow ≠ Watched

---

## DONE — Upcoming / Following

- Mixed Following catalog surface
- Upcoming local catalog (DB-first)
- Movie future `ReleaseDate`
- TV future `FirstAirDate`
- `isFollowed` support where applicable

---

## DONE — TV Upcoming Episodes / Airing (v1)

Follow-first per-episode upcoming intelligence on top of existing Catalog Follow + release notifications.

**API (`GET /api/catalog/upcoming`):**
- Discriminated `upcomingKind`: `MovieRelease`, `TvShowPremiere`, `TvEpisode`
- Authenticated users: one next future episode per followed TV show (`AirDate > UTC today`)
- Ordering per show: `AirDate` ASC → `SeasonNumber` ASC → `EpisodeNumber` ASC → episode `Id` ASC
- Null air date excluded; airing today is **not** future; unfollowed shows absent; unfollow does not delete catalog episodes
- Movies and TV premieres (`FirstAirDate`) unchanged for anonymous and authenticated callers

**Freshness (followed shows only):**
- `TvUpcomingEpisodeSyncJob` (`movieapp:tv-upcoming-episode-sync`, hourly UTC) reads TMDB `tv/{id}` `next_episode_to_air`
- When `air_date` present: bounded `GetSeasonAsync` hydrate + episode upsert
- `TvShowCatalogSyncState.LastUpcomingEpisodeSyncAtUtc`; default TTL **6h** (`TvUpcomingEpisodeSync:FreshnessTtlHours`)
- Provider failure preserves last data and does not advance sync timestamp; successful empty sync still marks timestamp

**Notifications:** **IMPLEMENTED** via existing `HotReleaseCheckJob` + `ReleaseDetector` + fanout (episode dedupe already exists). No second notification system.

**Mobile:** Profile → My Library → Upcoming screen (`/upcoming`); `UpcomingCard` episode UX (`Sxx Exx`, episode title, relative air date).

**Migration:** `20260915134637_AddTvUpcomingEpisodeSync` (create only — do not assume applied on staging until deliberately migrated).

---

## DONE — Regional release & certification (v1)

Configured-market movie release semantics using persisted TMDB regional release data.

**Shipped (v1):**
- `ReleaseRegion:DefaultRegion` configuration (default `TR`; not hardcoded in business logic)
- `movie_regional_releases` persistence (`MovieRegionalRelease`)
- TMDB `movie/{id}/release_dates` provider abstraction
- Deterministic effective release resolver (TR theatrical/digital consumer semantics)
- `MovieReleaseCheck` uses regional effective date for `MovieReleased` events
- Movie Follow eligibility uses regional effective date when successfully synced
- Upcoming movies use regional effective date when successfully synced (DB-only)
- Certification persisted internally; **not** exposed on public API or mobile yet

**Guardrails (v1):**
- `Movie.ReleaseDate` remains TMDB global/primary `release_date` — semantics unchanged
- Recommendation 2.1 future filtering still uses global `Movie.ReleaseDate` (provider-free)
- Search / local New Releases / Explore preview / provider Discover / Home / collections unchanged
- No per-user / per-follower / recommendation / Upcoming / Discover-card TMDB release-date calls
- One bounded `release_dates` fetch per unique followed movie per release-check refresh when required

**Deferred:** user-level region preference, public API/mobile certification display, multi-region active population beyond configured default, recommendation future-filter by regional date.

---

## DONE — TV seasons, episodes & watch progress

Catalog TV depth and per-episode watch tracking (distinct from future Upcoming Episodes / Airing).

- TV season list and season detail
- Episode detail screens
- Mark watched / unwatched for movies and episodes
- Bulk season/episode watch updates
- Watched episodes list and recent watch history
- TV show watch progress and season-level watched state
- Mobile routes under `app/(tabs)/tv/[id]/season/...`

**Not the same as NEXT:** episode airing calendar, “Coming This Week”, or followed-show schedule intelligence.

---

## DONE — Recommendation 2.0

Local-first personalized recommendations.

**Signals:**
- Ratings, Favorites, Watched Movies
- Watched TV collapsed to title-level
- Watchlists
- TV Follow
- Search History (weak signal)
- Movie Follow = **exclusion only**

**Decisions:**
- Genre is primary local taste signal
- Person weight remains 0 in personalized scoring
- Collections are not positive taste signals
- Future content excluded per current rules
- Followed content excluded
- Candidate cap 500
- Collection / genre diversity preserved
- Because You Watched remains similarity-based
- Recommendation runtime is local-first (DB + cache)

---

## DONE — Keyword Ingestion v1

Local keyword persistence and opportunistic TMDB ingestion.

- `Keyword` entity
- `MovieKeyword`, `TvShowKeyword` relationships
- `Movie.KeywordsSyncedAtUtc`, `TvShow.KeywordsSyncedAtUtc`
- TMDB movie + TV keyword ingestion via `IKeywordsProvider`
- Authoritative relationship synchronization
- Successful-empty semantics (sync marker set even with zero keywords)
- Fail-soft provider behavior on detail paths
- Detail-page opportunistic ingestion (`enrichKeywords: true`)
- Summary paths do **not** keyword-enrich (`enrichKeywords: false`)

**Migration:** `20260915095315_AddCatalogKeywords`

---

## DONE — Recommendation 2.1

Keyword affinity added to personalized recommendations.

**Hierarchy:** Genre affinity > Keyword affinity > behavior / quality / popularity contributions

**Accepted defaults:**
- `PersonalizedGenreWeight` = **0.45**
- `PersonalizedKeywordWeight` = **0.15**
- Personalized recommendation cache = **`v3`**

**Behavior:**
- Persisted DB keyword data only
- Cross-type Movie ↔ TV keyword affinity
- Missing keyword metadata is neutral
- Personalized behavior similarity compares interacted catalog titles to candidates using source catalog `VoteAverage` and release/air year (cast/person overlap remains excluded)
- Source and candidate normalization, bounded 0..1
- **0 TMDB/provider calls** during recommendation execution
- No public API / mobile contract change for keywords

**Not planned:** Recommendation 2.2, embeddings, NLP keyword similarity.

---

## DONE — Similar content (detail-page)

Local similarity recommendations on movie/TV detail screens.

- `/api/recommendations` similar-movie and similar-TV endpoints
- Similarity algorithm version **`v1`** (genre/cast/rating/year — separate from personalized home `v3`)
- Mobile `SimilarContentSection` on movie and TV detail
- Provider-free at request time (local catalog data)

**Distinction:** This is **not** Home “Recommended For You” (Recommendation 2.0/2.1) and **not** “Because You Watched” similarity sections.

---

## DONE — Catalog keyword backfill infrastructure

`CatalogKeywordBackfillJob` implemented and accepted in code.

- Environment-neutral (staging + future production)
- Bounded per execution (default `BatchSize` 25, `MaxConcurrency` 2)
- Movie/TV fairness allocation
- User-interacted priority, popularity fallback, deterministic ordering
- Reuses Keyword Ingestion v1 (no duplicate sync logic)
- Per-title failure isolation, idempotent processing
- Coverage measurement service
- Hangfire recurring support (`movieapp:catalog-keyword-backfill`)
- Default `CatalogKeywordBackfill:Enabled` = **false**
- No migration required beyond Keyword Ingestion v1
- No recommendation-time coupling

| Aspect | Status |
|---|---|
| Implementation | **DONE** |
| Staging operational validation | **DONE / PASS** (2026-09-15 — see below) |

**Staging concurrency fixes validated:**
- **c295896** — independent DI scope / `ApplicationDbContext` per parallel backfill worker (`MaxConcurrency=2`)
- **a90f2a5** — PostgreSQL `ON CONFLICT ("TmdbKeywordId") DO NOTHING` for canonical keyword creation (`IX_keywords_TmdbKeywordId` race)

---

## DONE — Person / Cast / Crew

- Person Detail
- Filmography
- Cast & Crew 2.0
- Movie credits, TV aggregate credits
- Cast/Crew preview and See All browsing
- Lazy ingestion when selecting a catalog title from filmography

**Not done:** Person Search (see NEXT). Person results are not part of catalog Search today.

---

## DONE — Trailers / media foundation

Trailers & Media v1:

- Movie and TV video endpoints
- TMDB video provider
- Trailer > Teaser selection preference
- Official / language / newest preference
- YouTube key validation
- Provider + cache based (no DB persistence)
- Mobile Trailer action / playback

**Not done:** full image/media gallery (backdrops, posters, logos) — see NEXT.

---

## DONE — Collections

- TMDB movie collection metadata
- Movie collection entry point
- Collection detail / browse
- Provider-backed collection fetch
- Summary materialization

Collections remain navigation/context, **not** positive recommendation taste signals.

---

## DONE — Where to Watch

Streaming availability on movie and TV detail.

- Movie and TV watch-provider endpoints
- TMDB watch-provider integration (`IWatchProviderService`)
- Region-aware requests with validation/normalization
- Provider + cache based (**no DB persistence** — same pattern as trailers)
- Mobile `WhereToWatchRail` on movie and TV detail

Do **not** propose Where to Watch as future work.

---

## DONE — User reviews (first-party)

MovieApp-authored user reviews — **not** TMDB third-party reviews.

- Create / update / delete user review per movie or TV show
- Fetch current user review and paginated title reviews
- Mobile review composer, review list, and detail integration
- Distinct from backlog item **“TMDB Reviews integration”** (external critic/user reviews from TMDB)

Ratings (star scores) remain separate and are already part of core user-state.

---

## DONE — Account / identity

Authentication and account management beyond bare JWT login.

- Registration and login
- Forgot password and reset password flows
- Current user profile read/update
- Email change (re-auth required)
- Password change (re-auth required)
- Account deletion
- Profile analytics / statistics dashboard (`/api/users/me/statistics` + mobile `ProfileAnalyticsDashboard`)
- Mobile account screens: edit profile, email, password, delete account

**Auth note unchanged:** no refresh-token flow.

---

## DONE — Notification center

- Notification inbox
- Unread count
- Mark read / mark all read
- Home bell badge
- Notification screen
- Secure notification tap ownership validation

**Separate from inbox:** physical push delivery E2E remains **BLOCKED** (see below).

---

## DONE — Profile / My Library

Profile surfaces account + My Library in this order at a high level:

1. Favorites
2. Watchlist
3. Watch History
4. Following

Also done:
- Dedicated Following screen
- Profile analytics / statistics dashboard (watch activity insights)
- Counts / subtitles
- Pagination and invalidation fixes

**Product decision:** Watchlist selection remains **multi-select**. Do not auto-close watchlist selection after picking one list. Future UX polish (checkbox / Done / haptic) is backlog, not current priority.

---

## DONE — Performance / stabilization

Summarized engineering milestone (not a commit log):

- Backend read-path optimization
- Search batch upsert / reduced N+1
- Deterministic search ordering
- AdvancedSearch stabilization
- User-state integration stabilization
- Mobile query/render optimization
- Profile / library optimization
- Home / Discover / Search regression coverage

---

## DONE — Catalog keyword backfill operational staging validation

**Status date:** 2026-09-15

Migration confirmed present on staging: `20260915095315_AddCatalogKeywords`

### BEFORE final validated run (measured)

| Segment | Eligible | Synced | Unsynced | Coverage |
|---|---:|---:|---:|---:|
| Movies | 332 | 13 | 319 | ~3.92% |
| TV | 284 | 13 | 271 | ~4.58% |
| **Overall** | **616** | **26** | **590** | **4.22%** |

### Final validated bounded run

Configuration:

- `CatalogKeywordBackfill:Enabled` = true (manual / bounded)
- `BatchSize` = 25
- `MaxConcurrency` = 2

Structured log completion (event **6011**):

```
selected=25 succeeded=25 failed=0 skipped=0
movies=12 tv=13
durationMs=11905
coverageBefore=4.22% coverageAfter=8.28%
```

### AFTER final validated run (measured)

| Segment | Eligible | Synced | Unsynced | Coverage |
|---|---:|---:|---:|---:|
| Movies | 332 | 25 | 307 | ~7.53% |
| TV | 284 | 26 | 258 | ~9.15% |
| **Overall** | **616** | **51** | **565** | **8.28%** |

Delta exactly matches log: movies **+12**, TV **+13**, total **+25**.

### Operational PASS evidence

- [x] One real staging execution observed
- [x] Structured log completion (events 6010/6011)
- [x] AFTER coverage measured and reconciled with log
- [x] `failed=0`, `skipped=0`
- [x] No shared `ApplicationDbContext` concurrency exception
- [x] No `IX_keywords_TmdbKeywordId` / PostgreSQL `23505` failure
- [x] `MaxConcurrency=2` validated under real PostgreSQL

**Distinction:** backfill **mechanism / operational validation** = **DONE**. Full staging keyword coverage (565 unsynced) = **NOT DONE**.

---

## DONE — Controlled staging keyword coverage growth

**Status date:** 2026-09-15

Four additional sequential bounded batches completed after the operational validation checkpoint (**51 / 616 = 8.28%**).

Configuration (unchanged across all batches):

```
CatalogKeywordBackfill__BatchSize=25
CatalogKeywordBackfill__MaxConcurrency=2
```

Batches did **not** overlap.

| Batch | Succeeded | Movies | TV | Coverage before → after | Duration (ms) |
|---:|---:|---:|---:|---|---:|
| 1 | 25/25 | 12 | 13 | 8.28% → 12.34% | 13701 |
| 2 | 25/25 | 12 | 13 | 12.34% → 16.40% | 12113 |
| 3 | 25/25 | 12 | 13 | 16.40% → 20.45% | 12415 |
| 4 | 25/25 | 12 | 13 | 20.45% → 24.51% | 12415 |

**Combined:** 100 selected · 100 succeeded · 0 failed · 0 skipped · `MaxConcurrency=2` unchanged.

### Coverage checkpoint (derived from completion logs)

| Segment | Eligible | Synced (derived) | Unsynced (derived) | Coverage |
|---|---:|---:|---:|---:|
| Movies | 332 | **73** | 259 | ~22.0% |
| TV | 284 | **78** | 206 | ~27.5% |
| **Overall** | **616** | **151** | **465** | **24.51%** |

Derived from prior DB checkpoint (movies 25, TV 26) plus batch deltas (+48 movies, +52 TV). **Confirm with one fresh AFTER SQL snapshot before Recommendation 2.1 validation.**

No need to drain the remaining **465** unsynced titles for validation. Full catalog keyword coverage remains **intentionally NOT DONE**.

---

## DONE — Recommendation 2.1 real-data validation (keyword affinity)

Staging validation **PASS** (2026-09-15) on materially enriched data (~24.5% keyword coverage):

- Keyword affinity works end-to-end and materially affects ranking
- Shared-keyword contributions observed on real Home recommendations
- Genre remains stronger than keyword influence (`0.45` > `0.15`) as designed

**Follow-up fix (post-validation):** personalized behavior similarity now uses interacted source catalog `VoteAverage` and year when scoring via `SimilarityEngine` (previously hardcoded `0` / `null`, which inverted rating similarity). Cast/person overlap remains intentionally excluded. Personalized cache version bumped to **`v4`**.

**Not in scope:** Recommendation 2.2, coefficient auto-tuning, new recommendation architecture.

---

## NEXT — Media gallery

TMDB image/media endpoints for detail-screen polish:

- Backdrops
- Posters
- Logos

Prefer provider + cache initially. Do not automatically persist all image metadata.

Trailers v1 is DONE — do not reimplement trailers.

---

## NEXT — Person search / Person 2.0

**Done today:** Person Detail + Filmography.

**Future:**
- Search Person
- Richer biography / profile metadata
- Known For
- Person images where useful
- Improved Movie/TV filmography presentation

Do not mix Person results into catalog Search without deliberate UX.

---

## BEFORE PRODUCTION — Observability / operations

Mandatory before store launch. Need operational visibility for:

- API health, PostgreSQL, Redis
- Hangfire / job failures
- TMDB failures and rate behavior during backfill
- Keyword coverage trend
- Release detection and notification fanout
- Push preparation, dispatch, Expo receipts
- Unexpected 5xx rate

Do not claim dashboards or APM exist unless provisioned. Detailed checks: [PRODUCTION-LAUNCH-CHECKLIST.md](./PRODUCTION-LAUNCH-CHECKLIST.md) §11.

---

## BEFORE PRODUCTION — PostgreSQL integration suite

**Blocker:** `MovieApp.IntegrationTests` has pre-existing analyzer build failures (CA1707, CA1822, etc.). Unit/Release validation passes; integration project must be clean before production rehearsal.

- Fix analyzer/build blockers without weakening analyzers
- Run full PostgreSQL integration suite
- Establish clean production-rehearsal baseline

---

## BEFORE PRODUCTION — Production infrastructure

Production uses **separate** API, PostgreSQL, Redis, and secrets.

- Do **not** reuse staging database as production
- Do **not** assume staging catalog or keyword data transfers
- Production DB starts with migrations + its own lazy ingestion and backfill lifecycle

Detailed steps: [PRODUCTION-LAUNCH-CHECKLIST.md](./PRODUCTION-LAUNCH-CHECKLIST.md).

---

## BLOCKED — Physical iPhone push E2E

Final physical-device push chain is **not complete**.

**Target validation:**
physical iPhone → staging/production-equivalent build → login → notification permission → Expo token → active `PushDevice` → Follow → controlled release event → fanout → delivery → Expo ticket → **physical notification** → Expo receipt `Delivered` → safe tap routing

**External blocker observed:** Expo/EAS credential preparation — *“iTunes service key is empty”*. Do not randomly regenerate certificates as roadmap work. Remains blocked until EAS credential/build path is reliable.

When resolved: physical push E2E becomes a **hard launch gate** (see launch checklist §8).

---

## BEFORE PRODUCTION — Staging / production rehearsal

After major product work stabilizes:

- Freeze release candidate, clean CI, apply migrations
- Staging smoke, recurring jobs, keyword backfill, recommendation validation
- Notification pipeline, provider behavior, observability
- Production infrastructure rehearsal

Full ordered checklist: [PRODUCTION-LAUNCH-CHECKLIST.md](./PRODUCTION-LAUNCH-CHECKLIST.md) §13–14.

---

## BEFORE PRODUCTION — Store readiness

**iOS:** EAS production build, TestFlight, physical device validation, App Store metadata/privacy/review readiness

**Android:** Production build, internal testing, Play Console readiness

**Both:** Production API URL, version/build numbers, assets, privacy requirements, smoke test, no staging endpoints or secrets in release builds

Not complete.

---

## LATER / BACKLOG

Restrained backlog — not current execution priority.

- Alternative Titles / Translations
- External IDs / IMDb linking
- TMDB third-party Reviews integration (only if product value justifies — distinct from DONE first-party user reviews)
- TV Episode Groups if alternate ordering becomes necessary
- Watchlist multi-select UX polish (checkbox / Done / haptic)
- Deeper Person imagery / profile polish
- TMDB `append_to_response` provider optimization investigation

Do not turn every TMDB endpoint into a roadmap commitment.

---

## Explicitly not planned right now

Prevents scope creep. May be reconsidered later.

- Recommendation 2.2
- Embeddings / vector recommendation
- NLP keyword similarity
- Cast/person affinity in recommendation
- Collection affinity in recommendation
- New Library bottom tab
- New Explore bottom tab
- Restoring old overloaded Home
- Unbounded catalog keyword backfill
- Staging DB becoming production DB

---

## Known operational notes

- **Render Free** services may sleep; Hangfire cron is not guaranteed while sleeping. Manual bounded execution may be required on staging.
- **Staging PostgreSQL Free** instance has a known expiry: **2026-10-12**. Resolve before that date if staging must remain available. This is staging-only — not production infrastructure.
- **No Hangfire dashboard** is mapped today; there is **no secure public/admin HTTP trigger** for operational jobs (e.g. keyword backfill enqueue). Manual operations require DI/Hangfire host access or temporary env-driven recurring enablement.
- Staging API example: `https://movieapp-fpkg.onrender.com` (no secrets in this document).

---

## Completed-feature guardrail

Before proposing a “new” MovieApp feature, check DONE sections first.

**Already shipped — do not roadmap again:**
- Where to Watch
- User reviews (first-party MovieApp reviews)
- Trailers
- Collections
- Cast & Crew
- TV season/episode browsing and watch-progress tracking
- Similar content on detail pages
- Notification Center
- Push device registration API (implementation — not the same as physical push E2E)
- Person Detail / Filmography
- Following / Upcoming catalog (title-level + TV episode upcoming v1)
- Account management (profile, email/password change, delete account, statistics dashboard)
- Keyword ingestion and backfill **infrastructure**
- Recommendation 2.0 and 2.1

Extend this list when new capabilities ship.

---

## Maintenance rule

**Update PRODUCT-ROADMAP.md when:**
- Roadmap status changes
- A roadmap feature ships
- A feature becomes blocked or unblocked
- A significant product decision changes
- A new production-critical engineering milestone is discovered

**Update PRODUCTION-LAUNCH-CHECKLIST.md when:**
- EF migration added
- Recurring/background job added or changed
- Production secret/config added
- Deployment workflow changes
- External provider requirement changes
- Notification/release semantics change
- Store release requirement changes

When implementation changes either document's truth, update the relevant document in the **same feature commit** whenever practical.

---

## Current execution order

1. ~~**Finish** staging `CatalogKeywordBackfill` operational validation~~ **DONE (2026-09-15)**
2. ~~**Controlled staging keyword coverage growth** (~8% → ~25%)~~ **DONE (2026-09-15)**
3. **Recommendation 2.1** real-data validation **(IN PROGRESS)**
4. **Regional Release v1** staging validation (migration, release-check, follow, upcoming)
5. ~~**TV Upcoming Episodes / Airing**~~ **DONE (2026-09-15)**
6. **Media Gallery**
7. **Person Search / Person 2.0**
8. **Production observability / operations**
9. **Fix and run** PostgreSQL integration suite
10. **Production infrastructure** + launch rehearsal
11. **Physical iPhone Push E2E** when Expo blocker clears
12. **Store release readiness**
13. **App Store / Google Play release**

Steps 6–7 may be reordered if launch scope is frozen earlier, but production gates **8–12 cannot be skipped**.

---

*End of roadmap.*

# MovieApp — Product & Engineering Roadmap

**Last updated:** 2026-09-15 (Discovery 2.0 D1.5 navigation IA)  
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

### Primary bottom navigation

**Home | Discover | Library | Profile**

Search is **not** a bottom-tab destination. Search is a global action / entry point available from Home and Discover (and via deep links to the hidden Search route).

### Home = personalized landing / feed

**All users:**
- Prominent global Search entry near the top
- Hot This Week hero (up to 5, TMDB weekly trending)
- Trending Now (10, local catalog sort by vote count)
- Top Rated (Bayesian-weighted catalog ranking; requires ≥1 valid genre; final combined rail max 3 Animation / 10; may return fewer than requested rather than violate eligibility/diversity)
- New Releases

**Personalized (warm user):**
- Recommended For You (10)
- Coming Up (up to 5 followed TV upcoming episodes; omitted when empty)

**Cold (low signal):**
- Welcome
- Explore CTA → Discover tab

Home may still surface useful personalized library-derived sections (e.g. Continue Watching on Home when present) without replacing the Library tab.

### Discover = discovery hub

First-class bottom tab for finding new content:
- Global Search entry
- **Explore with Filters** (D1 Advanced Discover) → `/advanced-discover`
- Layout prepared for D4–D6 (On TV This Week, World Cinema, Pick Something For Me) — placeholders only until those phases ship; D2 Streaming Services and D3 Now in Theaters are live
- Existing real discovery content (Trending, Top Rated, genres, New Releases browse)

Filtered browse listing screens remain reachable from Discover, genres, See All, and deep links.

### Library = personal collection hub

First-class bottom tab for saved, watched, followed, rated, and in-progress content:
- Collection summary (counts from existing profile statistics)
- Continue Watching (from existing Home progress data)
- Destinations: Favorites, Watchlists, Following, Watch History, Coming Up, Ratings & Reviews (via Profile analytics)

Status visuals use only domain-backed states (watching/progress, completed where explicitly represented, saved/watchlist). Do **not** invent collection status.

### Profile = account and settings

- Identity / account editing
- Notification and regional/settings functionality
- Analytics / ratings insights
- Compact **Open My Library** shortcut (primary library access lives on the Library tab)

### Search = global action (hidden route)

Existing Search screen, autocomplete, history, and result navigation are preserved at `/(tabs)/search` (hidden from tab bar). Back navigation is origin-aware (Home → Search → Back → Home; Discover → Search → Back → Discover).

### Important IA decision (do not revert)

Home owns the primary browse rails. Discover owns the discovery hub and D1 Advanced Discover entry. Library owns personal collection access. Profile is account/settings-first. Do **not** restore Search as a primary bottom tab or make Profile the main library destination.

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
- Top Rated (10, Bayesian-weighted catalog ranking; no provider calls; requires ≥1 valid genre; final combined Home rail caps Animation catalog genre to max 3 via post-ranking diversity; may return fewer than 10 rather than violate eligibility/diversity)
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

## DONE — Discovery 2.0 D1 (Advanced Discover Core)

Reusable advanced discovery engine for later Discovery 2.0 phases (D2–D6).

**Backend (`GET /api/discovery/advanced`):**
- Typed MovieApp contract (not raw TMDB query strings)
- `mediaType`: `movie` | `tv` (required; no `all`)
- Filters: genres, year or year range, min/max rating, min vote count, min/max runtime, original language, **origin country** (`with_origin_country`; distinct from future release/watch region semantics)
- Sorts: `popularity_desc`, `rating_desc`, `newest`, `oldest`
- Adult content excluded (`include_adult=false`)
- Provider abstraction via `AdvancedDiscoverMoviesAsync` / `AdvancedDiscoverTvShowsAsync` on existing TMDB providers
- Results reuse `SearchResponse` / `SearchItem`; lazy summary ingestion via `EnsureFromSummariesAsync` for detail navigation
- Redis cache (10 min TTL)

**Mobile:**
- Discover hub entry: **Explore with Filters** → `/advanced-discover` (legacy Search idle card retained for deep links)
- Filters: media type, genres (multi-select), min rating, year/range, runtime presets, original language, origin country, sort
- Results: existing search cards, loading/empty/error/retry, infinite pagination, movie/TV detail navigation with filter state preserved on return

**Out of scope (subsequent phases at D1 ship):** streaming provider filters, Now in Theaters, TV This Week, World Cinema presets, Pick Something For Me.

## DONE — Discovery 2.0 D2 (Streaming Services Discovery)

Provider-first discovery by **watch region** and streaming availability, reusing the same typed advanced discovery engine as D1.

**Domain semantics (strict separation):**
- `originCountry` — where content originates (D1)
- `watchRegion` — ISO 3166-1 alpha-2 region for streaming availability queries (D2)
- `releaseRegion` — ISO 3166-1 alpha-2 region for theatrical availability queries (D3)

**Backend:**
- `GET /api/discovery/watch-providers?mediaType=movie|tv&watchRegion=TR` — stable MovieApp provider catalog (not raw TMDB DTOs); fields: `providerId`, `name`, `logoPath`, `displayPriority`; ordered by TMDB display priority for the region; cached 24h
- Extended `GET /api/discovery/advanced` with optional `watchRegion`, `watchProviderId[]`, `watchMonetizationType[]` (`stream`, `free`, `ads`, `rent`, `buy`)
- TMDB mapping: `watch_region`, `with_watch_providers`, `with_watch_monetization_types`; multi-provider selection uses **pipe OR** (`8|337` = Netflix OR Disney+); multi-monetization uses pipe OR; `stream` → `flatrate`
- Validation rejects provider/monetization filters without `watchRegion`; cache keys include watch region, providers, and monetization types
- Lazy summary ingestion unchanged (`EnsureFromSummariesAsync`); no schema change

**Mobile:**
- Discover hub **Streaming Services** entry active → `/streaming-discover` (D4–D6 remain coming-soon placeholders)
- Provider-first screen: watch region, multi-select providers (logos + names), Movies/TV toggle, availability types (Stream/Free/With Ads/Rent/Buy), optional min rating + sort, results via existing search cards with pagination and detail navigation
- Advanced Discover filter sheet extended with optional Streaming section (watch region, providers, availability); D1 URLs remain valid
- JustWatch attribution on Streaming Services screen (aligned with Where to Watch)
- Initial watch region defaults to existing app preference / `TR` fallback; no GPS/location permission

## DONE — Discovery 2.0 D3 (Now in Theaters)

Region-aware theatrical discovery for **movies only**, using explicit `releaseRegion` semantics (distinct from `originCountry` and `watchRegion`).

**Backend:**
- `GET /api/discovery/now-in-theaters?releaseRegion=TR&page=1&pageSize=20` — returns standard `SearchResponse` movie items
- TMDB source: **`movie/now_playing`** with `region={releaseRegion}` (canonical theatrical now-playing list per region; not Discover date filtering)
- Cache key includes `releaseRegion`, `page`, `pageSize`; TTL 30 minutes
- Lazy summary ingestion via `EnsureFromSummariesAsync`; no schema change

**Mobile:**
- Discover hub **Now in Theaters** preview carousel with See All, contained loading/error/empty states
- Full screen `/now-in-theaters` with release-region selector, infinite pagination, pull-to-refresh, movie detail navigation with return-state preservation
- Shared generic `RegionSelector` with semantic wrappers: `WatchRegionSelector` (D2) and `ReleaseRegionSelector` (D3)
- Default release region uses existing `TR` beta convention; no GPS/location permission

**Next Discovery 2.0 phase:** D4 On TV This Week.

## DONE — Discovery 2.0 D1.5 (Navigation IA + Discover Hub + Global Search + Library Hub)

**Mobile only** — no backend changes.

- Bottom navigation: **Home | Discover | Library | Profile**; Search and Watchlist hidden from tab bar (routes preserved)
- Global Search entry component on Home and Discover → existing Search experience with origin-aware back
- Discover hub with D1 **Explore with Filters**, D2 **Streaming Services**, D3 **Now in Theaters** preview, future D4–D6 placeholders (non-interactive), and existing Trending / Top Rated / genre browse
- Library hub reusing profile statistics, Home Continue Watching, and existing Favorites / Watchlists / Following / History / Coming Up destinations
- Premium library status accents for domain-backed watching (TV episode context) and summary counts; no invented movie playback progress
- Profile demoted to account/settings with compact Library shortcut

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

**Mobile:** Home Coming Up → See All → Upcoming screen (`/upcoming`); `UpcomingCard` episode UX (`Sxx Exx`, episode title, relative air date).

**Migration:** `20260915134637_AddTvUpcomingEpisodeSync` (create only — do not assume applied on staging until deliberately migrated).

## DONE — Home Coming Up (v1)

Home presentation layer over existing followed-TV upcoming episode catalog data (no new provider pipeline).

**Home composition order:**
1. Hot This Week hero
2. Recommended For You (personalized only)
3. Coming Up (up to 5 followed TV upcoming episodes; DB-only; omitted when empty)
4. Trending Now
5. Top Rated
6. New Releases

**Semantics:** authenticated user; personalized followed content only — future followed movies (release alert), followed TV premieres, and one next future episode per followed show (`AirDate > today`); null `AirDate` excluded; nearest air date first; Home See All and Profile → Coming Up use `/api/catalog/upcoming?scope=followed`; generic catalog discovery remains available via default `scope=catalog`.

**Mobile:** `HomeComingUpSection` / `HomeComingUpCard` on Home; card shows poster, show title, `Sxx Exx`, episode name, air date, relative label; tap → TV detail.

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

## DONE — Future Release Action Guardrail (v1)

Detail-screen action eligibility for consumption and follow/alert actions.

**Movie consumption (`isReleased`):**
- Uses regional effective release date via `MovieFollowReleaseDateResolver` + `MovieConsumptionReleaseGuardrail`
- Future effective release → Watched/Rating hidden
- Release date == today or past → Watched/Rating available
- Unknown/null effective release → conservative: consumption available

**Movie follow/alert (`canFollowForRelease`, `canSetReleaseAlert`):**
- Same effective release date source as regional follow eligibility — no second algorithm
- Future effective release → Alert/Follow available
- Release date == today or past → hidden
- Unknown/null effective release → conservative: available

**TV follow (`canFollow`):**
- Normalized from persisted `TvShowStatus` enum — no provider calls during detail render
- Ended / Canceled → Follow hidden
- Returning Series, In Production, Planned, Pilot, other non-terminal → Follow available
- Does **not** infer ended from `lastAirDate < today`

**Data:** UI eligibility only — existing `CatalogFollow` rows are not deleted; no cleanup jobs.

**Mobile:** consumes backend eligibility fields; hides (not disables) obsolete actions; action row reflows.

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
- Filmography (Known For preview on detail, See All full grid with All/Movies/TV filters, popularity-ranked ordering)
- Cast & Crew 2.0
- Movie credits, TV aggregate credits
- Cast/Crew preview and See All browsing
- Lazy ingestion when selecting a catalog title from filmography

**Done:** Person Search — unified search and autocomplete include person results via TMDB `search/person` (max 3 persons on All page 1).

---

## DONE — Person search / Person 2.0

- Unified search type `person` and mixed All results (max 3 persons on page 1)
- TMDB person provider search with popularity-based relevance ranking
- Autocomplete includes persons with `tmdbId` and `knownForDepartment`
- Lazy person persistence via `EnsureFromSummariesAsync`

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

## DONE — Media gallery

Movie / TV / Person galleries backed by lazy, cached TMDB image provider calls:

- Movie gallery (`GET /api/movies/{id}/images`) — backdrops + posters
- TV gallery (`GET /api/tvshows/{id}/images`) — backdrops + posters
- Person gallery (`GET /api/people/tmdb/{tmdbPersonId}/images`) — profile images
- Deterministic quality/language-aware ordering, duplicate path removal
- 24h server-side cache per entity (language-aware for movie/TV)
- Mobile detail preview rails, full gallery screens with filters, full-screen viewer

Trailers v1 remains DONE — do not reimplement trailers.

---

## DONE — Observability / operations (V1)

Shipped operational visibility:

- `GET /health`, `GET /health/live` — process liveness (no dependency probe)
- `GET /health/ready` — PostgreSQL + Redis readiness with safe JSON response writer
- Correlation ID middleware (`X-Correlation-Id` / `X-Request-Id`) with Serilog enrichment and ProblemDetails `correlationId`
- Background job start/failure/skip structured logs via `BackgroundJobOperationalRunner`
- TMDB HTTP client operational logging without credentials in log paths

Still required before store launch (process/infrastructure, not code):

- Log access path and operator review workflows
- Manual 5xx / job failure / provider failure monitoring during launch window
- External uptime monitoring and alert delivery (if provisioned)

Do not claim dashboards or APM exist unless provisioned. Detailed checks: [PRODUCTION-LAUNCH-CHECKLIST.md](./PRODUCTION-LAUNCH-CHECKLIST.md) §12.

---

## BEFORE PRODUCTION — PostgreSQL integration suite

**Status (2026-09-15):** **DONE** — Release IntegrationTests build is analyzer-clean; local PostgreSQL suite is fully green (**308 / 308**). Discover browse pagination is enforced at the merger boundary; Search/Home/Recommendation EF paths use deterministic ordering before `Take`/`Skip` and split-query projections where multiple collections are loaded.

- [x] Fix analyzer/build blockers without weakening analyzers
- [x] Run PostgreSQL integration suite locally with Docker Compose
- [x] Full suite green (308/308)
- [x] Runtime EF query-quality cleanup (row limiting + multi-collection projections)
- [ ] Establish clean production-rehearsal baseline

See [INTEGRATION-TESTS.md](./INTEGRATION-TESTS.md).

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
- Person Detail / Filmography (Known For preview + full grid)
- Media gallery (Movie / TV / Person)
- Following / Upcoming catalog (title-level + TV episode upcoming v1)
- Account management (profile, email/password change, delete account, statistics dashboard)
- Keyword ingestion and backfill **infrastructure**
- Recommendation 2.0 and 2.1
- Discovery 2.0 D1 (Advanced Discover Core)

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
6. ~~**Media Gallery**~~ **DONE**
7. ~~**Person Search / Person 2.0**~~ **DONE**
8. **Production observability / operations**
9. **Fix and run** PostgreSQL integration suite
10. **Production infrastructure** + launch rehearsal
11. **Physical iPhone Push E2E** when Expo blocker clears
12. **Store release readiness**
13. **App Store / Google Play release**

Steps 6–7 may be reordered if launch scope is frozen earlier, but production gates **8–12 cannot be skipped**.

---

*End of roadmap.*

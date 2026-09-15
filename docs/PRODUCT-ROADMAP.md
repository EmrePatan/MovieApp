# MovieApp — Product & Engineering Roadmap

**Last updated:** 2026-09-15  
**Backend baseline:** `e563d312630b08491c20569d5f462a045dd47cab`

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

**Personalized (warm user):**
- Hero
- Recommended For You
- Because You Watched (when available)

**Cold (low signal):**
- Welcome
- Trending Now
- Explore CTA

### Search tab = Search + premium Explore landing while idle

**Idle Explore:**
- Trending Now
- Top Rated
- New Releases
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

Do **not** restore the old overloaded Home with generic Popular, New Releases, Top Rated, Continue Watching, and genre fanout rails. Generic discovery belongs primarily in Search / Explore / Discover.

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

## DONE — Discovery / Search / Home

Premium Home / Search / Explore / Discover information architecture.

- Personalized Home and cold Home
- Recommended For You
- Because You Watched
- Trending, Top Rated, New Releases
- Explore by Genre
- Search (movies / TV)
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

**Not implemented:** upcoming episode calendar / per-episode airing schedule (see NEXT).

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
- Source and candidate normalization, bounded 0..1
- **0 TMDB/provider calls** during recommendation execution
- No public API / mobile contract change for keywords

**Not planned:** Recommendation 2.2, embeddings, NLP keyword similarity.

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
| Staging operational validation | **IN PROGRESS** (see below) |

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

## IN PROGRESS — Staging keyword backfill validation

**Status date:** 2026-09-15

Migration confirmed present on staging: `20260915095315_AddCatalogKeywords`

### BEFORE coverage (measured)

| Segment | Eligible | Synced | Unsynced | Coverage |
|---|---:|---:|---:|---:|
| Movies | 332 | 1 | 331 | ~0.30% |
| TV | 284 | 0 | 284 | 0% |
| **Overall** | **616** | **1** | **615** | **~0.16%** |

Existing keyword storage before backfill:
- `keywords` = 4
- `movie_keywords` = 4
- `tv_show_keywords` = 0

### Current validation

One bounded staging execution in progress:

- `BatchSize` = 25
- `MaxConcurrency` = 2

### Operational PASS requires

- [ ] One real staging execution observed
- [ ] Structured log completion (events 6010/6011)
- [ ] AFTER coverage measured
- [ ] Failure rate reviewed
- [ ] Decision whether hourly gradual backfill remains enabled

Do **not** mark operational validation DONE until the above pass.

---

## NEXT — Recommendation 2.1 real-data validation

After keyword coverage improves on staging:

- Inspect real Recommended For You results
- Verify keyword affinity improves semantic relevance where expected
- Compare sparse vs enriched catalog behavior
- Confirm genre remains stronger than keyword influence
- Inspect cross-type recommendations and diversity

**Do not auto-tune** `0.45` / `0.15`. Only tune after real-data evidence. **NO CHANGE** is a valid outcome.

---

## NEXT — Regional release & certification

High-priority next product feature.

**Goal:** Improve movie release semantics using TMDB regional release data.

Investigate: regional release dates, release type (theatrical / digital / physical), TV premiere where relevant, certification, region/country.

**Product value:** Movie Follow / `MovieReleased` notifications should eventually respect the user's intended market/region, not only a single global release date.

**Potential UI:** “In theaters in Türkiye”, digital release, certification/age rating.

**Launch impact:** Will change release-event semantics → must update [PRODUCTION-LAUNCH-CHECKLIST.md](./PRODUCTION-LAUNCH-CHECKLIST.md) when implemented.

---

## NEXT — TV upcoming episodes / airing

Build on TV Follow + New Episode notifications.

**Potential surfaces:**
- Upcoming Episodes
- Coming This Week
- Next episode on TV detail
- Season/episode air dates
- Followed-show upcoming schedule

Must respect existing IA — do not re-overload Home.

**Not implemented today.** Generic Upcoming catalog (title-level) is DONE; episode calendar is not.

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
- TMDB Reviews integration (only if product value justifies)
- Provider-backed “What’s Hot This Week” if Explore needs it
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
- Staging API example: `https://movieapp-fpkg.onrender.com` (no secrets in this document).

---

## Completed-feature guardrail

Before proposing a “new” MovieApp feature, check DONE sections first.

**Already shipped — do not roadmap again:**
- Where to Watch (if present in current product)
- Trailers
- Collections
- Cast & Crew
- Notification Center
- Person Detail / Filmography
- Following / Upcoming catalog (title-level)
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

1. **Finish** staging `CatalogKeywordBackfill` operational validation
2. **Allow** controlled coverage growth if first batch passes
3. **Recommendation 2.1** real-data validation
4. **Regional Release & Certification**
5. **TV Upcoming Episodes / Airing**
6. **Media Gallery**
7. **Person Search / Person 2.0**
8. **Production observability / operations**
9. **Fix and run** PostgreSQL integration suite
10. **Production infrastructure** + launch rehearsal
11. **Physical iPhone Push E2E** when Expo blocker clears
12. **Store release readiness**
13. **App Store / Google Play release**

Steps 4–7 may be reordered if launch scope is frozen earlier, but production gates **8–12 cannot be skipped**.

---

*End of roadmap.*

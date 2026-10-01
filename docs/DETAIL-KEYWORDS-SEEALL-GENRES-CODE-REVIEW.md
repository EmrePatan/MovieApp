# Code Review: Detail Keywords + See All Genres

Review date: 2026-10-01. Review-only; no code changes in this pass.

### Scope Review

**In scope (MovieApp):** Detail keyword read path, detail DTO/API mapping, list genre batch enrichment, DI, unit tests, `docs/DETAIL-KEYWORDS-SEEALL-GENRES-IMPLEMENTATION.md` (feature doc).

**Out of scope / must not commit:** Untracked keyword backfill scripts, recommendation investigation markdown, benchmark tools (`tools/PersonalizedKeywordBenchmark`, `tools/RecommendationKeywordBenchmark`), `keyword-localization-backfill-artifacts/`, `docs/RECOMMENDATION-POST-DEPLOYMENT-VALIDATION.md`, and related recommendation artifacts.

**Tracked diff:** 34 files, +294/−27 lines. All tracked changes align with the feature plus test/DI updates.

### Backend Review

| Component | Verdict |
|-----------|---------|
| `ICatalogTitleKeywordReadRepository` / `CatalogTitleKeywordReadRepository` | Single EF query per movie/TV detail; localization via `keyword_localizations` subqueries in projection; in-memory distinct/sort/`Take(30)`. |
| `DetailKeywordOverlay` | Applied after overlays; locale defaults to `en-US` when `contentLocale` is null. |
| `GetMovieByIdService` / `GetTvShowByIdService` | Cache stores canonical detail with `Keywords: []`; keywords applied in `Finalize*` or post-overlay path on every successful read. |
| `SearchItemCatalogMetadataEnricher` | Distinct movie/TV ids per page; two batch genre queries max. |
| `GenreReadRepository` | `Contains` on id lists; per-title cap 3 after ordinal sort on `Genre.Name`. |
| DTOs / mappers | `Keywords` on detail contracts; optional `Genres` on `SearchItem` with `?? []` in `SearchContractMapper`. |

Implementation matches the reported behavior.

### Detail Keyword Query Review

1. **Every HTTP detail request with locale?** Yes, for API paths using `GetByIdAsync(id, Request.ResolveContentLocale())`. Internal overloads with `contentLocale: null` still run keyword overlay with `en-US`.
2. **+1 query per request?** Yes: one `ToListAsync` per detail read (movie or TV), regardless of Redis detail cache hit.
3. **Batch in one query?** Yes at the application level (one round-trip). EF loads all `movie_keywords`/`tv_show_keywords` rows for the title with correlated localization picks per keyword in SQL—not per-keyword round trips.
4. **N+1?** No ORM N+1; not one SQL statement per keyword id.
5. **Accept-Language?** Passed from controller → service → `NormalizeLocale` in repository.
6. **Fallback?** `KeywordDiscoverLocalizationSupport.ResolveDisplayName` (requested locale → en-US → canonical → raw name).
7. **Missing localization?** Falls through chain; empty names filtered out.
8. **Runs when detail cache hit?** Yes—by design keywords are not in `MovieDetailsCacheEntry` / `TvShowDetailsCacheEntry`.
9. **DB traffic concern?** Every detail view adds one read; titles with many keyword joins return all rows before client-side `Take(30)` (see Performance). Acceptable for catalog metadata if keyword cardinality is moderate.
10. **Intentional?** Yes—locale-specific keywords without invalidating canonical detail cache.

### Detail Cache Interaction

- Cached payload: `MovieMapper`/`TvShowMapper` set `Keywords: []`.
- Redis TTL unchanged (15 minutes) for canonical fields only.
- No change to recommendation caches.

### See All Genre Query Review

- **Batching:** `GetOrderedGenreNamesByMovieIdsAsync` / `GetOrderedGenreNamesByTvShowIdsAsync`—one query each per enrichment call.
- **Mixed pages:** Both queries run only when respective id lists are non-empty.
- **Max 3:** `CatalogDisplayLimits.MaxListItemGenres` applied in `GroupOrderedGenreNames`.
- **Order:** Ordinal sort on English `Genre.Name` (matches detail genre ordering).
- **Pagination:** Enricher does not alter page/size/total fields.
- **Empty genres:** `ResolveGenres` returns `Array.Empty<string>()`; mapper emits `[]`.
- **Where enrichment runs:** `SummaryLocalizationOverlayService.ApplyToSearchItemsAsync` (search, discovery cache load, now-in-theaters, on-tv, home sections using overlay), `AdvancedDiscoverService`, `DiscoverBrowseService` before browse cache write.

**Duplicate enrichment (non–N+1):** `DiscoverBrowseService.BrowseAsync` calls `discoveryService.GetNewReleasesAsync` / `GetTopRatedAsync`, which already enrich via overlay on discovery cache miss, then calls `EnrichGenresAsync` again before browse cache. Result: up to **four** batch genre queries on a cold browse path for those modes (two + two), idempotent. Cached browse/discovery responses avoid repeat work.

Paths that only use `DiscoveryService` cached results without a second enrich: single enrich per cache miss.

### API Contract

- `GET /api/movies/{id}`, `GET /api/tvshows/{id}`: `keywords` required on contract → JSON array (camelCase `keywords`).
- List/search/discovery: `genres` on `SearchItemResponse`; omitted/null coerced to `[]` in mapper.
- Existing fields unchanged in position except inserted `keywords` on detail (breaking for strict clients that reject unknown fields—standard JSON clients ignore or bind new optional fields; mobile types updated).

### Localization

- Keywords: server-only; no backend `GenreLocalization` for keywords; no LLM at request time.
- Genres on lists: canonical English from DB; mobile `translateGenreNames`.
- Locales supported via existing `SupportedContentLocales` / `NormalizeLocale` (includes tr-TR, en-US, es-ES, de-DE, fr-FR, it-IT, pt-BR).

### Mobile Detail

*(See MovieApp.Mobile review doc.)*

### Mobile See All

*(See MovieApp.Mobile review doc.)*

### Performance

| Path | Expected | Observed |
|------|----------|----------|
| Detail | +1 keyword query | Confirmed |
| See All page | +0–2 genre batch queries | Confirmed; +2 more possible on duplicate browse/discovery enrich path |
| Per keyword/card SQL | None | Confirmed |

Keyword query fetches all join rows for the title before `Take(MaxDetailKeywords)` in memory—worth monitoring for titles with very large keyword sets.

### Tests

| Suite | Result |
|-------|--------|
| `dotnet build -c Release` | Pass |
| `MovieApp.UnitTests` (full) | Pass 2296/2296 |
| Focused: `SearchItemCatalogMetadataEnricherTests`, `DetailKeywordOverlayTests`, `SummaryOverlay_ReturnsCanonicalUnchanged` | Pass 5/5 |
| `MovieApp.LoadTests` | Not run (per review instructions) |

No integration test asserting live API `keywords`/`genres` JSON shape was found in this review pass.

### Architecture

- `ICatalogTitleKeywordReadRepository` → scoped in Infrastructure DI.
- `SearchItemCatalogMetadataEnricher` → scoped in Application DI; injected into overlay and discover services.
- `GetTvShowByIdService` factory updated with keyword repository.
- No circular dependencies identified.

### Security

- EF parameterized queries; no string-interpolated SQL in new repositories.
- No credentials or connection strings in feature diff.
- No new PII logging observed in feature paths.

### Git Diff Scope

**Stage for feature commit:** All 34 modified tracked files + untracked feature sources/tests listed in implementation doc + `docs/DETAIL-KEYWORDS-SEEALL-GENRES-IMPLEMENTATION.md` + this review doc.

**Do not stage:** Untracked recommendation/keyword-backfill/benchmark artifacts listed in Scope Review.

### Issues Found

| Severity | Issue |
|----------|--------|
| Low | Duplicate genre batch queries when `DiscoverBrowseService` delegates to `DiscoveryService` then re-enriches. |
| Low | Keyword SQL returns all keyword joins for a title before in-memory cap at 30. |
| Info | No dedicated integration test for new JSON fields. |
| Info | `MovieDetailsResult.Keywords` in cache entry remains `[]`—by design. |

None block release for the stated feature scope.

### Commit Readiness

**MovieApp: READY** — stage only feature-related paths; exclude unrelated untracked files.

# Detail Keywords & See All Genres

### Existing Architecture

- **Detail (mobile):** `MovieDetailContent` / `TvShowDetailContent` compose `DetailHero` (genres as dot-separated text via `translateGenreNames`), `DetailOverview`, rails, and similar sections. Chip styling for genres already exists in `DetailGenres` (`DetailSections.tsx`) but hero uses inline genre text.
- **See All (mobile):** Full-list screens (`discover-browse`, `advanced-discover`, `now-in-theaters`, `on-tv-this-week`, `world-cinema`, unified `search`) render `SearchResultCard` → `CatalogResultRow`.
- **API lists:** `SearchItem` / `SearchItemResponse` power discovery, browse, and search; localization overlay runs through `SummaryLocalizationOverlayService`.
- **Keywords (backend):** Persisted on `movie_keywords` / `tv_show_keywords` with `keyword_localizations`; discover search already resolves display names via `KeywordDiscoverLocalizationSupport`. Detail DTOs previously omitted keywords.

### Detail Keywords

- Added `keywords: string[]` to `MovieDetailsResult`, `TvShowDetailsResult`, `MovieDetailsResponse`, `TvShowDetailsResponse`.
- Loaded per request (not stored in the 15-minute detail cache) through `ICatalogTitleKeywordReadRepository` + `DetailKeywordOverlay`, using the same localization fallback chain as discover (requested locale → en-US → canonical → name).
- Deterministic order: ordinal case-insensitive sort on display name; cap `CatalogDisplayLimits.MaxDetailKeywords` (30).
- Empty keyword sets omit the mobile section entirely.

### See All Genres

- Added optional `genres` on `SearchItem` / `SearchItemResponse` (canonical English `Genre.Name`, max 3 per item).
- `SearchItemCatalogMetadataEnricher` batch-loads genres for movie/TV ids on each page (two queries per page: movies + TV).
- Invoked from `SummaryLocalizationOverlayService`, `AdvancedDiscoverService`, and `DiscoverBrowseService` before caching.

### Localization

- **Keywords:** Resolved server-side from `keyword_localizations` using `Accept-Language`; mobile displays strings as returned.
- **Genres on lists:** API returns canonical English names; mobile uses existing `translateGenreNames` (`catalog-labels.ts`) for all supported locales.
- **Detail keywords UI:** Section title and “show more” use `details.sections.keywords` / `showMoreKeywords` in stage-2 locale bundles.

### API / DTO Changes

| Contract | Change |
|----------|--------|
| `MovieDetailsResponse` / `TvShowDetailsResponse` | `Keywords: string[]` |
| `SearchItemResponse` | `Genres?: string[]` |

No new endpoints.

### Database Query Impact

| Flow | Additional queries |
|------|-------------------|
| Movie/TV detail GET | +1 keyword join query per request (single title) |
| Search/discovery page (N items) | +0–2 genre batch queries (movies + TV ids on page) |

No N+1 per card or per keyword.

### Mobile UI Changes

- `DetailKeywords` — chip row under overview; expand after 12 chips.
- `CatalogResultRow` — muted genre line (`·`-separated) under type/year metadata.
- Types: `keywords` on detail responses; `genres?` on `CatalogItem`.

### Tests

- Backend: `SearchItemCatalogMetadataEnricherTests`, `DetailKeywordOverlayTests`; adjusted `ListLocalizationStage2Tests` for genre enrichment on English overlay.
- Mobile: extended `catalog-result-row.test.tsx` for genre line.
- `dotnet build -c Release` succeeds; `MovieApp.UnitTests` passes (2296). `MovieApp.LoadTests` may fail on timing/concurrency (unchanged infrastructure; not required for this feature).

### Performance

- Keyword and genre reads are batched or single-title scoped; discovery caches include enriched genres per locale key.

### Screens / Components Changed

**Backend:** `GetMovieByIdService`, `GetTvShowByIdService`, `SummaryLocalizationOverlayService`, `AdvancedDiscoverService`, `DiscoverBrowseService`, `GenreReadRepository`, `CatalogTitleKeywordReadRepository`, contract mappers.

**Mobile (`MovieApp.Mobile`):** `MovieDetailContent`, `TvShowDetailContent`, `DetailSections`, `CatalogResultRow`, `SearchResultCard`, `catalog.ts`, detail types, stage-2 locale files.

### Known Limitations

- Home carousel cards (`HomeContentCard`) still omit genres (out of See All scope).
- `RecommendationItemResponse` unchanged; personalized rails unchanged.
- Detail hero still shows genres as inline text; keyword chips are a separate section below overview.
- Titles without synced keywords show no keyword section until catalog keyword ingestion backfill runs.

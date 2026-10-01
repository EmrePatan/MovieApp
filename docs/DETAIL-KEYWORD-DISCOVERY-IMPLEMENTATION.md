# Detail keyword discovery

## UX

Movie and TV detail show a compact **Keywords** section: two horizontal scroll rows of tappable chips. No vertical expansion or “show more”. Empty keyword lists hide the section entirely.

## Two-Row Keyword Rail

- Mobile: `DetailKeywordRail.tsx` — two independent horizontal `ScrollView`s, alternating chip distribution for balance (`splitKeywordsIntoTwoRows`).
- Display cap: `DETAIL_KEYWORD_RAIL_MAX` (16) on mobile; backend still returns up to `CatalogDisplayLimits.MaxDetailKeywords` (30) in one query.
- Styling reuses existing chip tokens (`surfaceElevated`, `border`, gold accent on press).

## Keyword Navigation

- Chip press → `openKeywordDiscoverBrowse` → `/discover-browse` with `keywords={guid}` and `keywordLabels` JSON for the header.
- Browse screen title: `getDiscoverBrowseScreenTitle` shows the localized keyword name when exactly one keyword filter is active.

## KeywordId Filtering

- Navigation passes **catalog `KeywordId` (Guid)**, not localized text.
- Discover browse API already accepts `keywordId` query params; `DiscoverBrowseService` / `AdvancedDiscoverService` resolve IDs via `IKeywordDiscoverReadRepository`.
- No client-side catalog text filtering.

## Backend Changes

- `CatalogKeywordSummary` / `CatalogKeywordSummaryResponse` (`id`, `name`).
- `ICatalogTitleKeywordReadRepository` joins `keyword_display_profiles`, filters `Displayable`, orders by `DisplayRank`, then localizes (single EF query per detail read, max 30).
- Detail DTOs: `keywords` is an array of `{ id, name }` on movie/TV detail responses.
- Localization path unchanged (`KeywordDiscoverLocalizationSupport`).

## Discover fail-closed

When browse criteria include `keywordId`(s) but TMDB IDs cannot be resolved (no TMDB external reference and no `keywords.TmdbKeywordId`), `DiscoverBrowseService` / `AdvancedDiscoverService` return an **empty** paginated result and **do not** call TMDB discover unfiltered.

Resolution order (batched): `keyword_external_references` (provider TMDB) → `keywords.TmdbKeywordId`.

## Keyword display profile

Persistent table `keyword_display_profiles` (`KeywordId`, DF, movie/TV coverage, `Displayable`, `DisplayRank`). Refreshed after keyword catalog statistics refresh (Hangfire / warmup), not on detail requests.

Deterministic rules in `KeywordDisplayQualityEvaluator` (document frequency bounds, generic DF ratio, token/length limits, relationship/incidental fragment patterns, person-name heuristic with thematic exceptions, thematic boost for `DisplayRank`). Separate from recommendation IDF.

Sample staging before/after: `tools/KeywordDisplayProfileSample` (read-only, `MOVIEAPP_STAGING_POSTGRES_CONNECTION`).

## Mobile Changes

- Detail API field: `keywords?: unknown` (`DetailKeywordsApiPayload`).
- `normalizeDetailKeywords()` — single entry for legacy `string[]` and `{ id, name }[]`.
- Normalized rail type: `CatalogKeywordSummary` (`id: string | null`, `name`).
- Legacy chips display only; chips without `id` are not tappable.
- `createKeywordDiscoverHref`, `DetailKeywordRail`, `detail-keyword-navigation.ts`.
- `discover-browse` header uses keyword label when applicable.

## Localization

- Display names come from the server detail response for the request locale.
- `keywordLabels` route param carries the same localized string for the browse title only (not sent to the browse API).

## Performance

- Detail: **one** keyword read query per detail request (overlay after cache), same as before.
- Rail: **no** per-chip network calls.
- Keyword tap: **one** discover browse catalog request (paginated).

## Tests

- Backend: `DetailKeywordOverlayTests`.
- Mobile: `detail-keyword-rail.test.ts`, `detail-keywords-component.test.tsx`.

## Known Limitations

- Rail shows at most 16 of up to 30 server keywords.
- Keyword order is deterministic by precomputed `DisplayRank` (not raw TMDB/import order).
- Multi-keyword browse still uses mode title unless exactly one keyword with a label is filtered.

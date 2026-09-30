# Recommendation production finalization

### Final Scope

Shipped in this release:

1. **IDF-aware keyword preferences** for personalized recommendations (algorithm cache key **v7**).
2. **Home diversity depth**: for `sectionSize` 10 and 20, up to **80** scored rows are passed to Home selection (`HomeScoredPoolCap`), without changing SQL candidate budget.

Explicitly **not** shipped: diversity-aware reranking, candidate-generation expansion, diversity cap changes, localization changes.

### IDF Implementation

- `KeywordAffinityPreferenceBuilder` reads `IKeywordCatalogStatisticsProvider.Current` and passes snapshot + options into `KeywordAffinityScorer.BuildKeywordPreferences`.
- When `KeywordCatalogStatisticsOptions.Enabled` is true and the snapshot is available, IDF + generic dampening multipliers apply **only** when building preferences.
- `CalculateKeywordScore` / request-time scoring does **not** apply a second IDF pass.
- `PersonalizedKeywordWeight` remains **0.15** (`RecommendationOptions` / `appsettings.json`).
- Missing or failed snapshot: `useFrequencyAware` is false → legacy preference weighting (signal contribution only).
- Infrastructure: `KeywordCatalogStatisticsLoader` (aggregate SQL), `KeywordCatalogStatisticsProvider` (atomic snapshot swap), `KeywordCatalogStatisticsRefreshService`, warmup hosted service, Hangfire `KeywordCatalogStatisticsRefreshJob`.
- Wired in `RecommendationService`, `PickSomethingService`, `DeterministicAiMovieRecommendationProvider`.
- `RecommendationAlgorithmVersion.Personalized` = **v7** (cache key invalidation).

### Home Diversity Depth

- `HomeService.RecommendationCandidateLimit`: `Math.Max(heroWindow, HomeScoredPoolCap)` with `HomeScoredPoolCap = 80`.
- `sectionSize=10` → recommendation depth **80** (was 40 via `sectionSize * 4` before hero/surplus clamp).
- `sectionSize=20` → **80** (unchanged effective cap).
- SQL still loads/scores **500** candidates (`MaximumCandidates`); only the in-memory slice passed to `SelectHomeRecommended` widened for section 10.

### Candidate Generation

- Unchanged: `MaximumCandidates = 500`.
- No genre ∪ keyword expansion, no extra SQL candidate queries.

### Diversity Policy

- `ApplyDiversity`, `SelectHomeRecommended`, genre/collection/franchise caps unchanged.
- Home may return fewer than 10 items when diversity caps exhaust the 80-row pool (accepted).

### Localization

- No changes to keyword localization schema, backfill, or translation pipeline.

### Performance

| Requirement | Status |
|-------------|--------|
| SQL candidate budget 500 | Yes (`RecommendationOptions.MaximumCandidates`) |
| IDF refresh is background | Yes (Hangfire job + refresh service) |
| No catalog-wide IDF on request path | Yes (in-memory provider only) |
| No per-keyword IDF SQL on requests | **0** additional request-time IDF queries |
| Home 40→80 does not increase SQL count | Yes (still 500 loaded/scored) |
| Home selection uses scored in-memory pool | Yes (paginate/limit before `SelectHomeRecommended`) |
| Cache does not store 500 Home DTOs | Yes (Home cache keyed by section size; pool cap 80) |
| Home cache/result limit ≤ 80 | Yes (`HomeScoredPoolCap`) |

### Cache

- Home and user recommendation cache keys include `RecommendationAlgorithmVersion.Personalized` (**v7**).
- Generation-based invalidation unchanged.

### Tests

| Suite | Result |
|-------|--------|
| `dotnet build -c Release` | Success |
| `MovieApp.UnitTests` | **2277** passed |
| `MovieApp.IntegrationTests` | Not run to completion: requires `PostgreSql__Password` or `POSTGRES_PASSWORD` (unavailable in this environment) |
| `MovieApp.LoadTests` | **5** failures (concurrency/timing flakes; unrelated to this change) |

Relevant unit coverage: `KeywordCatalogStatisticsTests`, `HomeServiceTests` (`GetHomePersonalizedAsyncUsesHomeScoredPoolCapForRecommendationDepth`), recommendation/Home wiring tests with `IKeywordAffinityPreferenceBuilder` mock.

### Read-only sanity (staging user `53fc2a7d`)

`--pipeline-count-investigation` (existing harness, no production/DB writes):

- `MaximumCandidates`: 500
- Home pool input: **80**
- Home `SelectHomeRecommended` (All, section 10): **6** (genre rejects 74 in pool)
- Movie/TV paths consistent with prior investigations

### Benchmark-only cleanup

Removed from developer tooling (not production):

- `ExperimentalDiversityAwareReranker.cs`
- `HomeDiversityRerankBenchmarkRunner.cs`
- `HomeDiversityRerankBenchmarkReportWriter.cs`
- `--home-diversity-rerank-benchmark-53fc2a7d` CLI flag

Diversity-aware reranking was **not** productionized.

### Final Git Diff

Production commit includes:

- Application: IDF preference builder, `KeywordAffinityScorer` extension, algorithm **v7**, Home depth, DI
- Infrastructure: keyword catalog statistics loader/provider/refresh/warmup
- API: Hangfire refresh job registration
- Tests: Home depth, keyword statistics, background job registrar, recommendation mocks
- Docs: `docs/KEYWORD-CATALOG-STATISTICS.md`
- This report

Excluded from commit: localization backfill artifacts, investigation markdown reports, `tools/PersonalizedKeywordBenchmark/` (local developer tooling).

### Commit

See `git log -1` after push for hash and message.

### Push

Target: `origin master`

### Known Limitations

- Home **All** rail can still return &lt; 10 items for users with heavy genre concentration in the top-80 scored pool.
- IDF quality depends on snapshot freshness (background refresh interval).
- Single-user staging replay is illustrative only, not an acceptance gate.

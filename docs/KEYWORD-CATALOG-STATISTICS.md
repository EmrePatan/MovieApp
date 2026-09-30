# Keyword catalog statistics (IDF) for personalized recommendations

## Why IDF exists

Personalized recommendations already blend a keyword affinity signal (`PersonalizedKeywordWeight = 0.15`). Raw user keyword preferences treat every tag equally, so very common catalog tags (for example broad genres-as-keywords) can dominate distinctive tastes. **Inverse document frequency (IDF)** down-weights keywords that appear on many eligible titles; **generic-keyword dampening** further reduces preference weight when a keyword appears on at least 10% of the eligible catalog.

## Document frequency (DF)

DF is computed in a **background job only**, never during `GET /recommendations`.

Eligibility matches the recommendation keyword benchmark:

- Movies and TV shows combined
- `VoteCount >= 20` (configurable via `KeywordCatalogStatistics:MinimumVoteCount`)
- At least one genre on the title
- Each keyword counted **at most once per title**

A single aggregate SQL query loads `N` (eligible title count) and per-`KeywordId` DF.

## IDF formula

Production uses `KeywordCatalogStatisticsMath.BuildIdfWeights`, aligned with `ExperimentalKeywordAffinityScorer.BuildIdfWeights` in the benchmark tools:

```
idf(keyword) = log((N + 1) / (df + 1)) + 1
```

## Generic-keyword dampening

When `df / N >= 0.10`, the **preference accumulation** step multiplies that keyword's contribution by `0.5` (`KeywordCatalogStatistics:GenericDampeningFactor`). Threshold and multiplier are centralized in `KeywordCatalogStatisticsMath`.

IDF and dampening are applied **once**, when building normalized keyword preferences. `KeywordAffinityScorer.CalculateKeywordScore` is unchanged.

## Snapshot lifecycle

1. `KeywordCatalogStatisticsLoader` loads DF from PostgreSQL.
2. `KeywordCatalogStatisticsSnapshot.Create` builds immutable `KeywordId → (DF, IDF, generic flag)` entries.
3. `KeywordCatalogStatisticsProvider` publishes the full snapshot with an atomic `Volatile` swap.
4. `IKeywordAffinityPreferenceBuilder` reads `provider.Current` per request (in-memory only).

Approximate size: ~9k keywords, sub-1 MB in process memory.

## Refresh mechanism

- **Hangfire** recurring job: `movieapp:keyword-catalog-statistics-refresh` (`KeywordCatalogStatisticsRefreshJob`), cron from `KeywordCatalogStatistics:RefreshCron` (default daily 03:00 UTC).
- **Startup**: `KeywordCatalogStatisticsWarmupHostedService` may trigger a refresh when `RefreshOnStartup` is true.
- Refresh retains the previous snapshot if load or validation fails.

## Fallback

If statistics are disabled, empty, or refresh fails, `KeywordAffinityScorer` uses the legacy preference path (no IDF). Recommendations continue to serve.

## Request-time performance

Recommendation requests perform **zero** additional PostgreSQL round-trips for IDF. Only in-memory dictionary lookups on the current snapshot.

## Cache versioning

`RecommendationAlgorithmVersion.Personalized` was bumped when IDF-aware scoring shipped so personalized/home recommendation caches do not serve stale rankings across algorithm changes.

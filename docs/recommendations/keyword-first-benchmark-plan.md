# Movie Cave — Recommendation Quality Benchmark (research only)

Status: **DESIGN / NOT EXECUTED**. This document is a reproducible test plan, not evidence of improved recommendations. No production behavior changes.

## Objective
Compare current personalized recommendations (baseline) with keyword-first and mixed discovery variants, preserving user exclusions, safety, existing cold-start policy and response contracts.

## Verified baseline (master, 2026-11-07)
- `src/MovieApp.Api/appsettings.json`: genre .45, keyword .15, behavior .20, popularity .10, recency .05; max candidates 500; min vote count 20; home 10 items.
- `RecommendationRepository.BuildMovieCandidateQuery/BuildTvCandidateQuery`: a candidate must match at least one preferred genre when preferred genres exist. Candidate IDs are selected primarily by descending vote count and vote average, with a bounded pool.
- `PersonalizedRecommendationEngine.ScoreCandidates`: weighted genre/keyword/behavior/popularity/recency scores; behavior compares at most 10 strongest positive signals.
- `KeywordAffinityScorer`: preference contributions divided by sqrt(number of signal keywords); optional catalog-frequency dampening; candidate overlap divided by sqrt(number of candidate keywords). Check statistics availability in the actual runtime before assuming dampening is active.
- Existing diversity limits include genre, collection and franchise family. Do not silently remove them.
- **Potential issue to verify:** configured person weight is not included in the personalized score expression. Do not count it as active weight.
- **Potential issue to verify:** total configured personalized score weights above sum to .95, not 1.00. Keep scoring comparisons normalized consistently and do not assume score is a calibrated probability.

## Hypotheses
H1: genre-only filtering drops high-affinity cross-genre candidates.
H2: vote-count-first truncation hides niche but relevant titles.
H3: broad genres (e.g. Drama) have limited discriminative value compared with specific, well-covered keywords.
H4: users with multiple distinct taste clusters are underserved by top-10-signal behavior matching.

## Required data quality checks (read-only)
- Catalog: number of eligible movie/TV titles, keyword coverage (% with >=1 eligible keyword), keywords per title (p50/p90), genre distribution, Drama prevalence, keyword document frequencies, excluded/generic keyword prevalence.
- User profiles: distinct positive titles, genre and keyword preference distributions, cold-start fraction, multi-cluster prevalence, negative ratings and exclusion correctness.
- Candidate stages: eligible count -> genre-filtered count -> top-by-votes count -> hydrated profiles count -> final top 10; measure missed cross-genre keyword matches.
- Missing keyword coverage should be measured by content type and popularity bands; a keyword-first model must have a genre fallback.

## Read-only benchmark design
Use a **non-production database snapshot/staging** and anonymized, consent-appropriate user profiles. No writes to user data, recommendation cache, or jobs. Fix an evaluation timestamp and deterministic tie-breakers; run baseline and variants on the same catalog and profile snapshot.

Candidate arms:
A. Baseline: unchanged genre filter, vote-ranked bounded pool, current weights.
B. Score-only: unchanged candidate pool, increase keyword weight (isolates scoring effects).
C. Retrieval-only: allocate bounded candidate slots to genre, keyword-overlap and controlled exploration channels, deduplicate, preserve exclusions and movie/TV coverage; baseline weights.
D. Combined: C retrieval + keyword-first weights + 70% high-confidence / 30% relevant cross-genre discovery (not random).
E. Multi-interest (later): D with distinct taste clusters; avoid implementing until C/D prove value.

Suggested **experimental** score weights for B/D: keyword .40, behavior .25, genre .15, popularity .10, recency .05, exploration/novelty .05. Note that exploration is better handled as a reranking/quota objective than a naive per-item scalar; document exact implementation before running. These are hypotheses, not proven optimum.

Retrieval must be budgeted, not an unbounded OR predicate: initial cap 500; record per-channel selected counts, duplicates, overlap, and incremental PostgreSQL commands/latency. Validate indexing and `EXPLAIN (ANALYZE, BUFFERS)` **only on staging**, with safe limits.

## Offline evaluation
- Temporal holdout: construct preferences from events strictly before cutoff T; hide subsequent positive rating/favorite/watch events as labels. Exclude leakage from watchlist and later catalog/user activity. If no usable temporal holdout exists, use leave-one-out positive titles and explicitly label it as weaker evidence.
- Primary: NDCG@10, Recall@10, and per-user hit rate; macro-average by user, not activity count.
- Secondary: catalog coverage, intra-list diversity (genre and keyword), novelty, cross-genre meaningful-match rate, keyword coverage of results, cold-start fallback, movie/TV balance.
- Slice results by sparse/rich history, keyword-sparse/rich titles, Drama-heavy users, movie/TV, and popularity bands.
- Operational: p50/p95 end-to-end latency, DB round trips, total SQL time, cache-miss cost, memory, and recommendation churn.
- Guardrails: no excluded/watched/rated/favorited/watchlisted/followed titles; no unreleased titles beyond current policy; preserve diversity caps, user privacy and existing API schema.

## Acceptance criteria (proposed, requires agreement)
- D improves macro NDCG@10 over A on temporal holdout, with bootstrap confidence intervals and no material regression in sparse-history cohorts.
- Discovery quota yields meaningful keyword/theme overlap; not simply a genre change.
- No exclusion, cold-start, contract or localization regressions.
- Candidate expansion fits an agreed staging p95 latency and DB budget; do not merge until measured.

## Safe rollout
1. Instrument baseline in staging without modifying production.
2. Run A/B/C/D offline and publish aggregate results and representative anonymized examples.
3. Review query plans and regression tests; keep feature flag default OFF.
4. Optional shadow evaluation (no user-visible change), then gradual rollout only after approval.

## Findings vs unknowns
**Confirmed from source:** configured score weights, hard genre predicate, vote-ranked candidate truncation, top-10 behavior signals, keyword overlap and diversity machinery.
**Not yet measured:** actual Drama dominance, keyword catalog coverage, frequency-statistics availability, retrieval loss, recommendation quality improvement, and SQL performance.

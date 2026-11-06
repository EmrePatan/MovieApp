-- Movie Cave recommendation catalog quality diagnostic
-- READ ONLY. Run against a non-production staging snapshot after verifying
-- actual EF migrations/table mappings. No user identifiers or activities.
-- PostgreSQL: execute each SELECT independently; no writes.
--
-- IMPORTANT: relation names assumed from conventional EF mappings.
-- Verify against pg_catalog before running. If names differ, adapt query.
--
-- Preflight: verify actual table/column names BEFORE running the queries.
-- If any expected column is absent, stop and adapt to actual migrations.
SELECT table_name, column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN ('movies','tv_shows','movie_genres','tv_show_genres',
                     'movie_keywords','tv_show_keywords','genres','keywords')
ORDER BY table_name, ordinal_position;

-- The diagnostic deliberately counts raw keyword links. To measure the
-- effective recommender coverage, filter out Excluded classification and
-- apply the same runtime keyword options after verifying enum persistence.
-- Do not interpret raw keyword coverage as usable keyword coverage.

-- 1. Eligible title and keyword coverage (movies and TV independently).
WITH eligible AS (
    SELECT 'movie'::text AS content_type, m."Id" AS id, m."VoteCount" AS votes
    FROM public.movies m
    WHERE (m."ReleaseDate" IS NULL OR m."ReleaseDate" <= CURRENT_DATE)
      AND m."VoteCount" >= 20
    UNION ALL
    SELECT 'tv', t."Id", t."VoteCount"
    FROM public.tv_shows t
    WHERE (t."FirstAirDate" IS NULL OR t."FirstAirDate" <= CURRENT_DATE)
      AND t."VoteCount" >= 20
), keywords_per_title AS (
    SELECT e.content_type, e.id, e.votes,
           CASE WHEN e.content_type = 'movie'
                THEN (SELECT count(DISTINCT mk."KeywordId") FROM public.movie_keywords mk
                      WHERE mk."MovieId" = e.id)
                ELSE (SELECT count(DISTINCT tk."KeywordId") FROM public.tv_show_keywords tk
                      WHERE tk."TvShowId" = e.id) END AS keyword_count
    FROM eligible e
)
SELECT content_type, count(*) AS eligible_titles,
       count(*) FILTER (WHERE keyword_count > 0) AS titles_with_keywords,
       round(100.0 * count(*) FILTER (WHERE keyword_count > 0) /
             nullif(count(*), 0), 2) AS keyword_coverage_pct,
       percentile_cont(0.5) WITHIN GROUP (ORDER BY keyword_count) AS keyword_p50,
       percentile_cont(0.9) WITHIN GROUP (ORDER BY keyword_count) AS keyword_p90
FROM keywords_per_title
GROUP BY content_type ORDER BY content_type;

-- 2. Genre prevalence among eligible titles (inspect Drama share).
WITH eligible AS (
    SELECT 'movie'::text AS content_type, m."Id" AS id
    FROM public.movies m
    WHERE (m."ReleaseDate" IS NULL OR m."ReleaseDate" <= CURRENT_DATE)
      AND m."VoteCount" >= 20
    UNION ALL
    SELECT 'tv', t."Id"
    FROM public.tv_shows t
    WHERE (t."FirstAirDate" IS NULL OR t."FirstAirDate" <= CURRENT_DATE)
      AND t."VoteCount" >= 20
), memberships AS (
    SELECT 'movie'::text AS content_type, mg."MovieId" AS id, mg."GenreId" AS genre_id
    FROM public.movie_genres mg
    UNION ALL
    SELECT 'tv', tg."TvShowId", tg."GenreId" FROM public.tv_show_genres tg
)
SELECT e.content_type, g."Name" AS genre,
       count(DISTINCT e.id) AS titles,
       round(100.0 * count(DISTINCT e.id) /
             nullif((SELECT count(*) FROM eligible x WHERE x.content_type=e.content_type),0),2)
           AS eligible_catalog_pct
FROM eligible e
JOIN memberships m ON m.content_type=e.content_type AND m.id=e.id
JOIN public.genres g ON g."Id"=m.genre_id
GROUP BY e.content_type, g."Name"
ORDER BY e.content_type, titles DESC;

-- 3. Most common keyword IDs (no user data).
-- A keyword's commonness is not automatically proof it is unhelpful.
WITH keyword_memberships AS (
    SELECT 'movie'::text AS content_type, mk."MovieId" AS id, mk."KeywordId" AS keyword_id
    FROM public.movie_keywords mk
    JOIN public.movies m ON m."Id"=mk."MovieId"
    WHERE (m."ReleaseDate" IS NULL OR m."ReleaseDate" <= CURRENT_DATE)
      AND m."VoteCount" >= 20
    UNION ALL
    SELECT 'tv', tk."TvShowId", tk."KeywordId"
    FROM public.tv_show_keywords tk
    JOIN public.tv_shows t ON t."Id"=tk."TvShowId"
    WHERE (t."FirstAirDate" IS NULL OR t."FirstAirDate" <= CURRENT_DATE)
      AND t."VoteCount" >= 20
)
SELECT km.content_type, km.keyword_id, k."Name",
       count(DISTINCT km.id) AS document_frequency
FROM keyword_memberships km
JOIN public.keywords k ON k."Id"=km.keyword_id
GROUP BY km.content_type, km.keyword_id, k."Name"
ORDER BY km.content_type, document_frequency DESC
LIMIT 100;

-- NOTE: These are exploratory queries, not the full candidate benchmark.
-- Keyword exclusion/classification and runtime frequency stats must be
-- verified against app configuration before interpreting recommendations.

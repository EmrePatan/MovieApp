-- Movie Cave: staging-only, read-only catalog candidate export.
-- Execute with psql -X -q -v ON_ERROR_STOP=1 -At -f tools/sql/recommendation_candidate_export_staging.sql
-- Redirect stdout to a local .json file. NEVER commit exported catalog data.
-- Prerequisite: verify tables/columns using recommendation_catalog_quality_staging.sql.
-- No user table or user activity accessed. Profile is intentionally empty;
-- use anonymized, consented profiles separately for meaningful personalization.
-- The 20,000-row vote-first cap is a pragmatic export limit and introduces\n-- popularity bias. It must not be used to estimate full-catalog coverage.\n-- This is a catalog export, NOT a production-equivalent recommendation benchmark.
BEGIN TRANSACTION READ ONLY;
SET LOCAL statement_timeout = '60s';
WITH candidate_rows AS (
  SELECT m."Id"::text AS id, 'movie'::text AS content_type,
         m."VoteCount" AS vote_count, m."VoteAverage" AS vote_average,
         (m."ReleaseDate" IS NOT NULL AND m."ReleaseDate" <= CURRENT_DATE
          AND m."VoteCount" >= 20) AS eligible,
         COALESCE((SELECT jsonb_agg(DISTINCT mg."GenreId"::text)
                   FROM public.movie_genres mg WHERE mg."MovieId" = m."Id"), '[]'::jsonb) AS genre_ids,
         COALESCE((SELECT jsonb_agg(DISTINCT mk."KeywordId"::text)
                   FROM public.movie_keywords mk WHERE mk."MovieId" = m."Id"), '[]'::jsonb) AS keyword_ids
  FROM public.movies m
  UNION ALL
  SELECT t."Id"::text, 'tv', t."VoteCount", t."VoteAverage",
         (t."FirstAirDate" IS NOT NULL AND t."FirstAirDate" <= CURRENT_DATE
          AND t."VoteCount" >= 20),
         COALESCE((SELECT jsonb_agg(DISTINCT tg."GenreId"::text)
                   FROM public.tv_show_genres tg WHERE tg."TvShowId" = t."Id"), '[]'::jsonb),
         COALESCE((SELECT jsonb_agg(DISTINCT tk."KeywordId"::text)
                   FROM public.tv_show_keywords tk WHERE tk."TvShowId" = t."Id"), '[]'::jsonb)
  FROM public.tv_shows t
), capped AS (
  SELECT * FROM candidate_rows
  WHERE eligible
  ORDER BY vote_count DESC, id
  LIMIT 20000
)
SELECT jsonb_build_object(
  'profiles', '[]'::jsonb,
  'candidates', COALESCE(jsonb_agg(jsonb_build_object(
    'id', id, 'type', content_type, 'vote_count', vote_count,
    'vote_average', vote_average, 'eligible', eligible,
    'genre_ids', genre_ids, 'keyword_ids', keyword_ids
  )), '[]'::jsonb)
)::text FROM capped;
COMMIT;

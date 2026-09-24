-- Movie Cave #60 — READ-ONLY catalog ID extraction (PostgreSQL)
-- Use a read replica / read-only role. Do not run writes in production.

-- ---------------------------------------------------------------------------
-- HOT movies (popular, ingested catalog)
-- Adjust LIMIT for ~10–20 IDs for tests/load/data/hot-content.json
-- ---------------------------------------------------------------------------
SELECT m.id AS movie_id,
       m.tmdb_id,
       m.title,
       m.vote_count,
       m.vote_average
FROM movies m
WHERE m.tmdb_id IS NOT NULL
ORDER BY m.vote_count DESC NULLS LAST, m.vote_average DESC NULLS LAST
LIMIT 20;

-- ---------------------------------------------------------------------------
-- HOT TV shows
-- ---------------------------------------------------------------------------
SELECT t.id AS tv_show_id,
       t.tmdb_id,
       t.title,
       t.vote_count,
       t.vote_average,
       t.status
FROM tv_shows t
WHERE t.tmdb_id IS NOT NULL
ORDER BY t.vote_count DESC NULLS LAST, t.vote_average DESC NULLS LAST
LIMIT 20;

-- ---------------------------------------------------------------------------
-- VARIED movies — stratified by popularity (example: 5 per decile bucket)
-- Requires PostgreSQL window functions; tune LIMIT per bucket for ~200+ total
-- ---------------------------------------------------------------------------
WITH ranked AS (
    SELECT m.id,
           m.tmdb_id,
           m.title,
           m.vote_count,
           NTILE(10) OVER (ORDER BY m.vote_count DESC NULLS LAST) AS popularity_bucket
    FROM movies m
    WHERE m.tmdb_id IS NOT NULL
)
SELECT id AS movie_id, tmdb_id, title, vote_count, popularity_bucket
FROM ranked
WHERE popularity_bucket BETWEEN 1 AND 10
ORDER BY popularity_bucket, vote_count DESC
LIMIT 250;

-- ---------------------------------------------------------------------------
-- VARIED TV — same pattern
-- ---------------------------------------------------------------------------
WITH ranked AS (
    SELECT t.id,
           t.tmdb_id,
           t.title,
           t.vote_count,
           NTILE(10) OVER (ORDER BY t.vote_count DESC NULLS LAST) AS popularity_bucket
    FROM tv_shows t
    WHERE t.tmdb_id IS NOT NULL
)
SELECT id AS tv_show_id, tmdb_id, title, vote_count, popularity_bucket
FROM ranked
WHERE popularity_bucket BETWEEN 1 AND 10
ORDER BY popularity_bucket, vote_count DESC
LIMIT 250;

-- ---------------------------------------------------------------------------
-- Genre spread (movies) — optional extra diversity
-- ---------------------------------------------------------------------------
SELECT DISTINCT ON (mg.genre_id)
       m.id AS movie_id,
       m.title,
       mg.genre_id
FROM movies m
INNER JOIN movie_genres mg ON mg.movie_id = m.id
WHERE m.tmdb_id IS NOT NULL
ORDER BY mg.genre_id, m.vote_count DESC NULLS LAST;

-- ---------------------------------------------------------------------------
-- External ratings — WARM snapshot movie IDs only (#57)
-- media_type stored as string: 'Movie' / 'Tv' (verify in your DB)
-- ---------------------------------------------------------------------------
SELECT m.id AS movie_id,
       m.tmdb_id,
       s.provider,
       s.fetched_at_utc
FROM movies m
INNER JOIN external_rating_snapshots s
    ON s.tmdb_id = m.tmdb_id
   AND s.media_type = 'Movie'
WHERE m.tmdb_id IS NOT NULL
ORDER BY s.fetched_at_utc DESC
LIMIT 30;

SELECT t.id AS tv_show_id,
       t.tmdb_id,
       s.provider,
       s.fetched_at_utc
FROM tv_shows t
INNER JOIN external_rating_snapshots s
    ON s.tmdb_id = t.tmdb_id
   AND s.media_type = 'Tv'
WHERE t.tmdb_id IS NOT NULL
ORDER BY s.fetched_at_utc DESC
LIMIT 30;

-- ---------------------------------------------------------------------------
-- Post-export API validation (operator, not SQL):
--   GET /api/movies/{movie_id}  -> 200
--   GET /api/tvshows/{tv_show_id} -> 200
-- Or run: tests/load/scripts/Test-ContentIds.ps1
-- ---------------------------------------------------------------------------

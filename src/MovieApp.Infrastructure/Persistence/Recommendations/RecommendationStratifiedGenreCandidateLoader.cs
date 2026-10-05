using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Configuration;
using MovieApp.Application.Recommendations;
using MovieApp.Infrastructure.Persistence.Repositories;
using Npgsql;
using NpgsqlTypes;

namespace MovieApp.Infrastructure.Persistence.Recommendations;

internal sealed class RecommendationStratifiedGenreCandidateLoader(ApplicationDbContext dbContext)
{
    public async Task<List<IReadOnlyList<Guid>>> LoadMovieGenreBucketsAsync(
        IReadOnlyList<Guid> genreOrder,
        IReadOnlySet<Guid> excludedMovieIds,
        int perGenreLimit,
        RecommendationOptions options,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        if (genreOrder.Count == 0 || perGenreLimit <= 0)
        {
            return [];
        }

        if (IsNpgsql())
        {
            metrics.RecordRoundTrip();
            var rows = await QueryMovieRowsSqlAsync(
                genreOrder,
                excludedMovieIds,
                perGenreLimit,
                options,
                cancellationToken);
            return RecommendationStratifiedGenreSelection.BuildBuckets(genreOrder, rows, perGenreLimit);
        }

        metrics.RecordRoundTrip();
        var efRows = await QueryMovieRowsEfAsync(
            genreOrder,
            excludedMovieIds,
            options,
            cancellationToken);
        return RecommendationStratifiedGenreSelection.BuildBuckets(genreOrder, efRows, perGenreLimit);
    }

    public async Task<List<IReadOnlyList<Guid>>> LoadTvGenreBucketsAsync(
        IReadOnlyList<Guid> genreOrder,
        IReadOnlySet<Guid> excludedTvShowIds,
        int perGenreLimit,
        RecommendationOptions options,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        if (genreOrder.Count == 0 || perGenreLimit <= 0)
        {
            return [];
        }

        if (IsNpgsql())
        {
            metrics.RecordRoundTrip();
            var rows = await QueryTvRowsSqlAsync(
                genreOrder,
                excludedTvShowIds,
                perGenreLimit,
                options,
                cancellationToken);
            return RecommendationStratifiedGenreSelection.BuildBuckets(genreOrder, rows, perGenreLimit);
        }

        metrics.RecordRoundTrip();
        var efRows = await QueryTvRowsEfAsync(
            genreOrder,
            excludedTvShowIds,
            options,
            cancellationToken);
        return RecommendationStratifiedGenreSelection.BuildBuckets(genreOrder, efRows, perGenreLimit);
    }

    private bool IsNpgsql() =>
        dbContext.Database.IsRelational() &&
        dbContext.Database.ProviderName is "Npgsql.EntityFrameworkCore.PostgreSQL";

    private async Task<List<StratifiedGenreVoteRow>> QueryMovieRowsEfAsync(
        IReadOnlyList<Guid> genreOrder,
        IReadOnlySet<Guid> excludedMovieIds,
        RecommendationOptions options,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var minVoteCount = options.CandidateMinVoteCount;
        var genreSet = genreOrder.ToHashSet();

        var query =
            from genreLink in dbContext.MovieGenres.AsNoTracking()
            where genreSet.Contains(genreLink.GenreId)
            join movie in dbContext.Movies.AsNoTracking() on genreLink.MovieId equals movie.Id
            where (movie.ReleaseDate == null || movie.ReleaseDate <= today)
                  && (minVoteCount <= 0 || movie.VoteCount >= minVoteCount)
                  && (excludedMovieIds.Count == 0 || !excludedMovieIds.Contains(movie.Id))
            select new StratifiedGenreVoteRow(
                genreLink.GenreId,
                movie.Id,
                movie.VoteCount,
                movie.VoteAverage);

        return await query.ToListAsync(cancellationToken);
    }

    private async Task<List<StratifiedGenreVoteRow>> QueryTvRowsEfAsync(
        IReadOnlyList<Guid> genreOrder,
        IReadOnlySet<Guid> excludedTvShowIds,
        RecommendationOptions options,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var minVoteCount = options.CandidateMinVoteCount;
        var genreSet = genreOrder.ToHashSet();

        var query =
            from genreLink in dbContext.TvShowGenres.AsNoTracking()
            where genreSet.Contains(genreLink.GenreId)
            join tvShow in dbContext.TvShows.AsNoTracking() on genreLink.TvShowId equals tvShow.Id
            where (tvShow.FirstAirDate == null || tvShow.FirstAirDate <= today)
                  && (minVoteCount <= 0 || tvShow.VoteCount >= minVoteCount)
                  && (excludedTvShowIds.Count == 0 || !excludedTvShowIds.Contains(tvShow.Id))
            select new StratifiedGenreVoteRow(
                genreLink.GenreId,
                tvShow.Id,
                tvShow.VoteCount,
                tvShow.VoteAverage);

        return await query.ToListAsync(cancellationToken);
    }

    private async Task<List<StratifiedGenreVoteRow>> QueryMovieRowsSqlAsync(
        IReadOnlyList<Guid> genreOrder,
        IReadOnlySet<Guid> excludedMovieIds,
        int perGenreLimit,
        RecommendationOptions options,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH ranked AS (
                SELECT
                    mg."GenreId" AS genre_id,
                    m."Id" AS content_id,
                    m."VoteCount" AS vote_count,
                    m."VoteAverage" AS vote_average,
                    ROW_NUMBER() OVER (
                        PARTITION BY mg."GenreId"
                        ORDER BY m."VoteCount" DESC, m."VoteAverage" DESC, m."Id") AS row_number
                FROM movie_genres AS mg
                INNER JOIN movies AS m ON mg."MovieId" = m."Id"
                WHERE mg."GenreId" = ANY(@genre_ids)
                    AND (m."ReleaseDate" IS NULL OR m."ReleaseDate" <= @today)
                    AND (@min_vote_count <= 0 OR m."VoteCount" >= @min_vote_count)
                    AND (
                        cardinality(@excluded_ids) = 0
                        OR NOT (m."Id" = ANY(@excluded_ids)))
            )
            SELECT genre_id, content_id, vote_count, vote_average
            FROM ranked
            WHERE row_number <= @per_genre_limit
            ORDER BY genre_id, row_number
            """;

        return await ExecuteVoteRowQueryAsync(
            sql,
            genreOrder,
            excludedMovieIds,
            perGenreLimit,
            options,
            cancellationToken);
    }

    private async Task<List<StratifiedGenreVoteRow>> QueryTvRowsSqlAsync(
        IReadOnlyList<Guid> genreOrder,
        IReadOnlySet<Guid> excludedTvShowIds,
        int perGenreLimit,
        RecommendationOptions options,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH ranked AS (
                SELECT
                    tg."GenreId" AS genre_id,
                    t."Id" AS content_id,
                    t."VoteCount" AS vote_count,
                    t."VoteAverage" AS vote_average,
                    ROW_NUMBER() OVER (
                        PARTITION BY tg."GenreId"
                        ORDER BY t."VoteCount" DESC, t."VoteAverage" DESC, t."Id") AS row_number
                FROM tv_show_genres AS tg
                INNER JOIN tv_shows AS t ON tg."TvShowId" = t."Id"
                WHERE tg."GenreId" = ANY(@genre_ids)
                    AND (t."FirstAirDate" IS NULL OR t."FirstAirDate" <= @today)
                    AND (@min_vote_count <= 0 OR t."VoteCount" >= @min_vote_count)
                    AND (
                        cardinality(@excluded_ids) = 0
                        OR NOT (t."Id" = ANY(@excluded_ids)))
            )
            SELECT genre_id, content_id, vote_count, vote_average
            FROM ranked
            WHERE row_number <= @per_genre_limit
            ORDER BY genre_id, row_number
            """;

        return await ExecuteVoteRowQueryAsync(
            sql,
            genreOrder,
            excludedTvShowIds,
            perGenreLimit,
            options,
            cancellationToken);
    }

    private async Task<List<StratifiedGenreVoteRow>> ExecuteVoteRowQueryAsync(
        string sql,
        IReadOnlyList<Guid> genreOrder,
        IReadOnlySet<Guid> excludedIds,
        int perGenreLimit,
        RecommendationOptions options,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new NpgsqlParameter("genre_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = genreOrder.ToArray()
        });
        command.Parameters.Add(new NpgsqlParameter("today", NpgsqlDbType.Date)
        {
            Value = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        command.Parameters.Add(new NpgsqlParameter("min_vote_count", NpgsqlDbType.Integer)
        {
            Value = options.CandidateMinVoteCount
        });
        command.Parameters.Add(new NpgsqlParameter("excluded_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = excludedIds.Count == 0 ? Array.Empty<Guid>() : excludedIds.ToArray()
        });
        command.Parameters.Add(new NpgsqlParameter("per_genre_limit", NpgsqlDbType.Integer)
        {
            Value = perGenreLimit
        });

        var rows = new List<StratifiedGenreVoteRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new StratifiedGenreVoteRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetInt32(2),
                reader.GetDecimal(3)));
        }

        return rows;
    }
}

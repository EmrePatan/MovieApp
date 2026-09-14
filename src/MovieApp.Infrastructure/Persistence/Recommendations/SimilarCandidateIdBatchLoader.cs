using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Recommendations;
using Npgsql;
using NpgsqlTypes;

namespace MovieApp.Infrastructure.Persistence.Recommendations;

internal sealed class SimilarCandidateIdBatchLoader(ApplicationDbContext dbContext)
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LoadMovieCandidateIdsAsync(
        IReadOnlyList<SimilaritySourceGenreRequest> sources,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        var activeSources = sources
            .Where(source => source.GenreIds.Count > 0)
            .ToList();

        if (activeSources.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Guid>>();
        }

        var sql = BuildMovieSql(activeSources.Count);
        var parameters = BuildParameters(activeSources, maxCandidates);
        var rows = await ExecuteAsync(sql, parameters, cancellationToken);
        return GroupRows(rows, sources.Select(source => source.SourceId));
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LoadTvShowCandidateIdsAsync(
        IReadOnlyList<SimilaritySourceGenreRequest> sources,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        var activeSources = sources
            .Where(source => source.GenreIds.Count > 0)
            .ToList();

        if (activeSources.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Guid>>();
        }

        var sql = BuildTvShowSql(activeSources.Count);
        var parameters = BuildParameters(activeSources, maxCandidates);
        var rows = await ExecuteAsync(sql, parameters, cancellationToken);
        return GroupRows(rows, sources.Select(source => source.SourceId));
    }

    private static string BuildMovieSql(int sourceCount)
    {
        var sourceValues = string.Join(
            ", ",
            Enumerable.Range(0, sourceCount).Select(index => $"(@source_id_{index}, @genre_ids_{index})"));

        return $"""
            WITH sources(source_id, genre_ids) AS (
                VALUES {sourceValues}
            ),
            ranked AS (
                SELECT
                    s.source_id,
                    m."Id" AS candidate_id,
                    ROW_NUMBER() OVER (
                        PARTITION BY s.source_id
                        ORDER BY m."VoteCount" DESC, m."VoteAverage" DESC, m."Id") AS row_number
                FROM sources s
                INNER JOIN movies m ON m."Id" <> s.source_id
                INNER JOIN movie_genres mg ON mg."MovieId" = m."Id"
                WHERE mg."GenreId" = ANY(s.genre_ids)
            )
            SELECT source_id, candidate_id
            FROM ranked
            WHERE row_number <= @max_candidates
            ORDER BY source_id, row_number
            """;
    }

    private static string BuildTvShowSql(int sourceCount)
    {
        var sourceValues = string.Join(
            ", ",
            Enumerable.Range(0, sourceCount).Select(index => $"(@source_id_{index}, @genre_ids_{index})"));

        return $"""
            WITH sources(source_id, genre_ids) AS (
                VALUES {sourceValues}
            ),
            ranked AS (
                SELECT
                    s.source_id,
                    t."Id" AS candidate_id,
                    ROW_NUMBER() OVER (
                        PARTITION BY s.source_id
                        ORDER BY t."VoteCount" DESC, t."VoteAverage" DESC, t."Id") AS row_number
                FROM sources s
                INNER JOIN tv_shows t ON t."Id" <> s.source_id
                INNER JOIN tv_show_genres tg ON tg."TvShowId" = t."Id"
                WHERE tg."GenreId" = ANY(s.genre_ids)
            )
            SELECT source_id, candidate_id
            FROM ranked
            WHERE row_number <= @max_candidates
            ORDER BY source_id, row_number
            """;
    }

    private static List<NpgsqlParameter> BuildParameters(
        List<SimilaritySourceGenreRequest> sources,
        int maxCandidates)
    {
        var parameters = new List<NpgsqlParameter>
        {
            new("max_candidates", NpgsqlDbType.Integer) { Value = maxCandidates }
        };

        for (var index = 0; index < sources.Count; index++)
        {
            parameters.Add(new NpgsqlParameter($"source_id_{index}", NpgsqlDbType.Uuid)
            {
                Value = sources[index].SourceId
            });
            parameters.Add(new NpgsqlParameter($"genre_ids_{index}", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
            {
                Value = sources[index].GenreIds.ToArray()
            });
        }

        return parameters;
    }

    private async Task<List<SimilarCandidateIdRow>> ExecuteAsync(
        string sql,
        IReadOnlyList<NpgsqlParameter> parameters,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        var results = new List<SimilarCandidateIdRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SimilarCandidateIdRow(
                reader.GetGuid(0),
                reader.GetGuid(1)));
        }

        return results;
    }

    private static Dictionary<Guid, IReadOnlyList<Guid>> GroupRows(
        IReadOnlyList<SimilarCandidateIdRow> rows,
        IEnumerable<Guid> requestedSourceIds)
    {
        var grouped = rows
            .GroupBy(row => row.SourceId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)group.Select(item => item.CandidateId).ToList());

        foreach (var sourceId in requestedSourceIds)
        {
            grouped.TryAdd(sourceId, []);
        }

        return grouped;
    }

    private sealed record SimilarCandidateIdRow(Guid SourceId, Guid CandidateId);
}

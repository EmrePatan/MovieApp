using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using Npgsql;

namespace MovieApp.Infrastructure.Persistence.Recommendations;

public sealed class KeywordDisplayProfileLoader(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IOptions<KeywordCatalogStatisticsOptions> statisticsOptions)
{
    private const string AggregateCoverageSql = """
        WITH eligible_movies AS (
          SELECT m."Id"
          FROM movies m
          WHERE m."VoteCount" >= @minVote
            AND EXISTS (SELECT 1 FROM movie_genres mg WHERE mg."MovieId" = m."Id")
        ),
        eligible_tv AS (
          SELECT t."Id"
          FROM tv_shows t
          WHERE t."VoteCount" >= @minVote
            AND EXISTS (SELECT 1 FROM tv_show_genres tg WHERE tg."TvShowId" = t."Id")
        ),
        movie_counts AS (
          SELECT mk."KeywordId" AS keyword_id, COUNT(DISTINCT mk."MovieId")::int AS movie_count
          FROM movie_keywords mk
          INNER JOIN eligible_movies em ON em."Id" = mk."MovieId"
          GROUP BY mk."KeywordId"
        ),
        tv_counts AS (
          SELECT tk."KeywordId" AS keyword_id, COUNT(DISTINCT tk."TvShowId")::int AS tv_count
          FROM tv_show_keywords tk
          INNER JOIN eligible_tv et ON et."Id" = tk."TvShowId"
          GROUP BY tk."KeywordId"
        ),
        all_pairs AS (
          SELECT DISTINCT mk."KeywordId" AS keyword_id, mk."MovieId" AS doc_id
          FROM movie_keywords mk
          INNER JOIN eligible_movies em ON em."Id" = mk."MovieId"
          UNION
          SELECT DISTINCT tk."KeywordId" AS keyword_id, tk."TvShowId" AS doc_id
          FROM tv_show_keywords tk
          INNER JOIN eligible_tv et ON et."Id" = tk."TvShowId"
        ),
        df AS (
          SELECT keyword_id, COUNT(*)::int AS document_frequency
          FROM all_pairs
          GROUP BY keyword_id
        )
        SELECT
          df.keyword_id,
          df.document_frequency,
          COALESCE(mc.movie_count, 0) AS movie_count,
          COALESCE(tc.tv_count, 0) AS tv_count
        FROM df
        LEFT JOIN movie_counts mc ON mc.keyword_id = df.keyword_id
        LEFT JOIN tv_counts tc ON tc.keyword_id = df.keyword_id;
        """;

    internal async Task<KeywordDisplayProfileLoadResult?> LoadAsync(CancellationToken cancellationToken)
    {
        var minVote = statisticsOptions.Value.MinimumVoteCount;
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var connectionString = context.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        long documentCount;
        const string documentCountSql = """
            SELECT
              (SELECT COUNT(*)::bigint FROM movies m
               WHERE m."VoteCount" >= @minVote
                 AND EXISTS (SELECT 1 FROM movie_genres mg WHERE mg."MovieId" = m."Id"))
            +
              (SELECT COUNT(*)::bigint FROM tv_shows t
               WHERE t."VoteCount" >= @minVote
                 AND EXISTS (SELECT 1 FROM tv_show_genres tg WHERE tg."TvShowId" = t."Id"));
            """;

        await using (var docCommand = new NpgsqlCommand(documentCountSql, connection))
        {
            docCommand.Parameters.AddWithValue("minVote", minVote);
            documentCount = (long)(await docCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);
        }

        if (documentCount <= 0)
        {
            return null;
        }

        var rows = new List<KeywordDisplayProfileCoverageRow>();
        await using (var command = new NpgsqlCommand(AggregateCoverageSql, connection))
        {
            command.Parameters.AddWithValue("minVote", minVote);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new KeywordDisplayProfileCoverageRow(
                    reader.GetGuid(0),
                    reader.GetInt32(1),
                    reader.GetInt32(2),
                    reader.GetInt32(3)));
            }
        }

        if (rows.Count == 0)
        {
            return null;
        }

        var keywordIds = rows.Select(row => row.KeywordId).ToList();
        var names = await context.Keywords
            .AsNoTracking()
            .Where(keyword => keywordIds.Contains(keyword.Id))
            .Select(keyword => new
            {
                keyword.Id,
                keyword.Name,
                keyword.CanonicalName,
            })
            .ToListAsync(cancellationToken);

        var namesById = names.ToDictionary(row => row.Id);

        return new KeywordDisplayProfileLoadResult(
            (int)Math.Min(int.MaxValue, documentCount),
            rows,
            namesById.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.CanonicalName ?? pair.Value.Name));
    }
}

internal sealed record KeywordDisplayProfileCoverageRow(
    Guid KeywordId,
    int DocumentFrequency,
    int MovieTitleCount,
    int TvTitleCount);

internal sealed record KeywordDisplayProfileLoadResult(
    int CatalogDocumentCount,
    IReadOnlyList<KeywordDisplayProfileCoverageRow> Rows,
    IReadOnlyDictionary<Guid, string> CanonicalNamesByKeywordId);

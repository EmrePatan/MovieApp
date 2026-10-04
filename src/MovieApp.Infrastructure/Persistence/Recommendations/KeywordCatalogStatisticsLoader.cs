using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Persistence.Keywords;
using Npgsql;

namespace MovieApp.Infrastructure.Persistence.Recommendations;

public sealed class KeywordCatalogStatisticsLoader(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IOptions<KeywordCatalogStatisticsOptions> options) : IKeywordCatalogStatisticsLoader
{
    private const string DocumentCountSql = """
        SELECT
          (SELECT COUNT(*)::bigint FROM movies m
           WHERE m."VoteCount" >= @minVote
             AND EXISTS (SELECT 1 FROM movie_genres mg WHERE mg."MovieId" = m."Id"))
        +
          (SELECT COUNT(*)::bigint FROM tv_shows t
           WHERE t."VoteCount" >= @minVote
             AND EXISTS (SELECT 1 FROM tv_show_genres tg WHERE tg."TvShowId" = t."Id"));
        """;

    private const string AggregateDfSql = """
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
        movie_pairs AS (
          SELECT DISTINCT mk."KeywordId" AS keyword_id, mk."MovieId" AS doc_id
          FROM movie_keywords mk
          INNER JOIN keywords kw ON kw."Id" = mk."KeywordId"
            AND kw."ClassificationStatus" <> @excludedClassificationStatus
          INNER JOIN eligible_movies em ON em."Id" = mk."MovieId"
        ),
        tv_pairs AS (
          SELECT DISTINCT tk."KeywordId" AS keyword_id, tk."TvShowId" AS doc_id
          FROM tv_show_keywords tk
          INNER JOIN keywords kw ON kw."Id" = tk."KeywordId"
            AND kw."ClassificationStatus" <> @excludedClassificationStatus
          INNER JOIN eligible_tv et ON et."Id" = tk."TvShowId"
        ),
        all_pairs AS (
          SELECT keyword_id, doc_id FROM movie_pairs
          UNION
          SELECT keyword_id, doc_id FROM tv_pairs
        )
        SELECT keyword_id, COUNT(*)::int AS df
        FROM all_pairs
        GROUP BY keyword_id;
        """;

    public async Task<KeywordCatalogStatisticsLoadResult?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var minVote = options.Value.MinimumVoteCount;
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var connectionString = context.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        long documentCount;
        await using (var docCommand = new NpgsqlCommand(DocumentCountSql, connection))
        {
            docCommand.Parameters.AddWithValue("minVote", minVote);
            documentCount = (long)(await docCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);
        }

        if (documentCount <= 0)
        {
            return null;
        }

        var documentFrequency = new Dictionary<Guid, int>();
        await using (var dfCommand = new NpgsqlCommand(AggregateDfSql, connection))
        {
            dfCommand.Parameters.AddWithValue("minVote", minVote);
            dfCommand.Parameters.AddWithValue("excludedClassificationStatus", KeywordClassificationSql.ExcludedStatus);
            await using var reader = await dfCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                documentFrequency[reader.GetGuid(0)] = reader.GetInt32(1);
            }
        }

        if (documentFrequency.Count == 0)
        {
            return null;
        }

        return new KeywordCatalogStatisticsLoadResult((int)Math.Min(int.MaxValue, documentCount), documentFrequency);
    }
}

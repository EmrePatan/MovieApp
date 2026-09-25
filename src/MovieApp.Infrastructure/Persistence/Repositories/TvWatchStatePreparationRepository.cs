using System.Data;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class TvWatchStatePreparationRepository(ApplicationDbContext dbContext) : ITvWatchStatePreparationRepository
{
    public async Task<TvWatchStatePreparation> PrepareAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT
                EXISTS(SELECT 1 FROM tv_shows t WHERE t."Id" = @tv_show_id) AS show_exists,
                COALESCE(
                    (
                        SELECT json_agg(
                            json_build_object(
                                'season_number', s."SeasonNumber",
                                'episode_count', s."EpisodeCount",
                                'has_episodes', EXISTS(
                                    SELECT 1 FROM episodes e WHERE e."SeasonId" = s."Id"))
                            ORDER BY s."SeasonNumber")
                        FROM seasons s
                        WHERE s."TvShowId" = @tv_show_id AND s."SeasonNumber" >= 1
                    ),
                    '[]'::json) AS regular_seasons_json,
                COALESCE(
                    (
                        SELECT array_agg(e."Id" ORDER BY s."SeasonNumber", e."EpisodeNumber")
                        FROM episodes e
                        INNER JOIN seasons s ON e."SeasonId" = s."Id"
                        WHERE s."TvShowId" = @tv_show_id AND s."SeasonNumber" >= 1
                    ),
                    ARRAY[]::uuid[]) AS episode_ids
            """;

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new NpgsqlParameter("tv_show_id", NpgsqlDbType.Uuid) { Value = tvShowId });

        if (command.Connection!.State != ConnectionState.Open)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return TvWatchStatePreparation.NotFound();
        }

        var showExists = reader.GetBoolean(0);
        if (!showExists)
        {
            return TvWatchStatePreparation.NotFound();
        }

        var seasonsJson = reader.GetString(1);
        var episodeIds = reader.IsDBNull(2)
            ? Array.Empty<Guid>()
            : (Guid[])reader.GetValue(2);

        var seasons = TvWatchStatePreparationParser.ParseRegularSeasons(seasonsJson);
        var (ingestionRequired, missingSeasonNumbers, seasonsWithEpisodeRowsCount) =
            TvWatchStatePreparationParser.EvaluateIngestion(seasons);

        return new TvWatchStatePreparation(
            TvShowExists: true,
            IngestionRequired: ingestionRequired,
            MissingSeasonNumbers: missingSeasonNumbers,
            EpisodeIds: episodeIds,
            RegularSeasonCount: seasons.Count,
            SeasonsWithEpisodeRowsCount: seasonsWithEpisodeRowsCount);
    }
}

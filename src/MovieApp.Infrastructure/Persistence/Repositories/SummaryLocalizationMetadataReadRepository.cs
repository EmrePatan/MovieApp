using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Enums;
using Npgsql;
using NpgsqlTypes;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class SummaryLocalizationMetadataReadRepository(
    ApplicationDbContext dbContext,
    IContentLocalizedPosterRepository contentLocalizedPosterRepository) : ISummaryLocalizationMetadataReadRepository
{
    public async Task<SummaryLocalizationMetadataBatch> LoadAsync(
        IReadOnlyList<Guid> movieIds,
        IReadOnlyList<Guid> tvIds,
        IReadOnlyList<ContentLocalizedPosterKey> posterKeys,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (movieIds.Count == 0
            && tvIds.Count == 0
            && posterKeys.Count == 0)
        {
            return EmptyBatch();
        }

        var scope = CatalogSearchTitleLanguageScope.FromContentLocale(contentLocale);

        var productionStopwatch = Stopwatch.StartNew();
        var productionTask = LoadProductionContextsAsync(movieIds, tvIds, cancellationToken);
        var titlesStopwatch = Stopwatch.StartNew();
        var titlesTask = LoadTitleRowsAsync(movieIds, tvIds, cancellationToken);

        await Task.WhenAll(productionTask, titlesTask).ConfigureAwait(false);

        productionStopwatch.Stop();
        titlesStopwatch.Stop();

        var (movieContexts, tvContexts) = await productionTask.ConfigureAwait(false);
        LocalizationOverlayPerfAmbient.RecordMovieProductionContextsMs(productionStopwatch.ElapsedMilliseconds);
        LocalizationOverlayPerfAmbient.RecordTvProductionContextsMs(0);

        var titleRows = await titlesTask.ConfigureAwait(false);
        var localizedMovieTitles = ContentSearchTitleDisplayTitleResolver.BuildMovieDisplayTitles(titleRows, scope);
        var localizedTvTitles = ContentSearchTitleDisplayTitleResolver.BuildTvDisplayTitles(titleRows, scope);
        LocalizationOverlayPerfAmbient.RecordLocalizedMovieTitlesMs(titlesStopwatch.ElapsedMilliseconds);
        LocalizationOverlayPerfAmbient.RecordLocalizedTvTitlesMs(0);

        var postersStopwatch = Stopwatch.StartNew();
        var localizedPosters = await LocalizedPosterDisplayOverlay.LoadPosterPathsAsync(
            contentLocalizedPosterRepository,
            posterKeys,
            contentLocale,
            cancellationToken).ConfigureAwait(false);
        postersStopwatch.Stop();
        LocalizationOverlayPerfAmbient.RecordLocalizedPostersMs(postersStopwatch.ElapsedMilliseconds);

        return new SummaryLocalizationMetadataBatch(
            movieContexts,
            tvContexts,
            localizedMovieTitles,
            localizedTvTitles,
            localizedPosters);
    }

    private static SummaryLocalizationMetadataBatch EmptyBatch() =>
        new(
            new Dictionary<Guid, ContentProductionContext>(),
            new Dictionary<Guid, ContentProductionContext>(),
            new Dictionary<CatalogContentKey, string>(),
            new Dictionary<CatalogContentKey, string>(),
            new Dictionary<ContentLocalizedPosterKey, string>());

    private async Task<IReadOnlyList<Domain.Entities.ContentSearchTitle>> LoadTitleRowsAsync(
        IReadOnlyList<Guid> movieIds,
        IReadOnlyList<Guid> tvIds,
        CancellationToken cancellationToken)
    {
        if (movieIds.Count == 0 && tvIds.Count == 0)
        {
            return [];
        }

        return await dbContext.ContentSearchTitles
            .AsNoTracking()
            .Where(row =>
                (row.TitleKind == ContentSearchTitleKind.Translation
                    || row.TitleKind == ContentSearchTitleKind.Alternative)
                && ((row.ContentType == CatalogContentType.Movie && movieIds.Contains(row.ContentId))
                    || (row.ContentType == CatalogContentType.Tv && tvIds.Contains(row.ContentId))))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(IReadOnlyDictionary<Guid, ContentProductionContext> Movies, IReadOnlyDictionary<Guid, ContentProductionContext> Tvs)> LoadProductionContextsAsync(
        IReadOnlyList<Guid> movieIds,
        IReadOnlyList<Guid> tvIds,
        CancellationToken cancellationToken)
    {
        if (movieIds.Count == 0 && tvIds.Count == 0)
        {
            return (new Dictionary<Guid, ContentProductionContext>(), new Dictionary<Guid, ContentProductionContext>());
        }

        var connectionString = dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string is not configured.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT 0 AS kind, m."Id", m."OriginalLanguage", m."PrimaryOriginCountryCode"
            FROM movies AS m
            WHERE m."Id" = ANY(@movie_ids)
            UNION ALL
            SELECT 1 AS kind, t."Id", t."OriginalLanguage", t."PrimaryOriginCountryCode"
            FROM tv_shows AS t
            WHERE t."Id" = ANY(@tv_ids)
            """;

        command.Parameters.Add(new NpgsqlParameter("movie_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = movieIds.ToArray(),
        });
        command.Parameters.Add(new NpgsqlParameter("tv_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = tvIds.ToArray(),
        });

        var movieContexts = new Dictionary<Guid, ContentProductionContext>();
        var tvContexts = new Dictionary<Guid, ContentProductionContext>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var kind = reader.GetInt32(0);
            var id = reader.GetGuid(1);
            var originalLanguage = reader.IsDBNull(2) ? null : reader.GetString(2);
            var primaryOriginCountryCode = reader.IsDBNull(3) ? null : reader.GetString(3);
            var context = new ContentProductionContext(originalLanguage, primaryOriginCountryCode);
            if (kind == 0)
            {
                movieContexts[id] = context;
            }
            else
            {
                tvContexts[id] = context;
            }
        }

        return (movieContexts, tvContexts);
    }
}

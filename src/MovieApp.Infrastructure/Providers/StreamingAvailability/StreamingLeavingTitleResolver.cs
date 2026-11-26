using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.StreamingLeaving;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Providers.StreamingAvailability;

public sealed partial class StreamingLeavingTitleResolver(
    IServiceScopeFactory scopeFactory,
    ILogger<StreamingLeavingTitleResolver> logger) : IStreamingLeavingTitleResolver
{
    public async Task<IReadOnlyDictionary<StreamingTitleKey, Guid>> ResolveAsync(
        IReadOnlyList<StreamingTitleKey> titles,
        CancellationToken cancellationToken = default)
    {
        var resolved = new Dictionary<StreamingTitleKey, Guid>();

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var movies = await services.GetRequiredService<IMovieRepository>().GetExistingIdsByTmdbIdsAsync(
                TmdbIds(titles, CatalogContentType.Movie),
                cancellationToken);
            var tvShows = await services.GetRequiredService<ITvShowRepository>().GetExistingIdsByTmdbIdsAsync(
                TmdbIds(titles, CatalogContentType.Tv),
                cancellationToken);

            foreach (var title in titles)
            {
                var existing = title.MediaType == CatalogContentType.Movie ? movies : tvShows;
                if (existing.TryGetValue(title.TmdbId, out var id))
                {
                    resolved[title] = id;
                }
            }
        }

        foreach (var title in titles)
        {
            if (resolved.ContainsKey(title))
            {
                continue;
            }

            try
            {
                // One scope per title: a failed save must not poison the next one.
                await using var scope = scopeFactory.CreateAsyncScope();
                if (await AddToCatalogAsync(scope.ServiceProvider, title, cancellationToken) is { } id)
                {
                    resolved[title] = id;
                }
                else
                {
                    LogUnmatched(logger, title.MediaType, title.TmdbId);
                }
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogAddFailed(logger, exception, title.MediaType, title.TmdbId);
            }
        }

        return resolved;
    }

    private static List<int> TmdbIds(IReadOnlyList<StreamingTitleKey> titles, CatalogContentType mediaType) =>
        titles.Where(title => title.MediaType == mediaType).Select(title => title.TmdbId).ToList();

    private static async Task<Guid?> AddToCatalogAsync(
        IServiceProvider services,
        StreamingTitleKey title,
        CancellationToken cancellationToken)
    {
        var externalId = title.TmdbId.ToString(CultureInfo.InvariantCulture);
        var upsertService = services.GetRequiredService<ICatalogProviderUpsertService>();

        // Keyword enrichment spends another provider's quota, so it is left to the detail screen.
        if (title.MediaType == CatalogContentType.Movie)
        {
            var details = await services.GetRequiredService<IMovieDataProvider>()
                .GetMovieAsync(externalId, cancellationToken: cancellationToken);
            return details is null
                ? null
                : (await upsertService.UpsertMovieFromProviderAsync(details, false, cancellationToken)).Id;
        }

        var tvDetails = await services.GetRequiredService<ITvShowDataProvider>()
            .GetTvShowAsync(externalId, cancellationToken: cancellationToken);
        return tvDetails is null
            ? null
            : (await upsertService.UpsertTvShowFromProviderAsync(tvDetails, false, cancellationToken)).Id;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Leaving-soon title not found in the catalog provider: {MediaType} {TmdbId}.")]
    private static partial void LogUnmatched(ILogger logger, CatalogContentType mediaType, int tmdbId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Leaving-soon title could not be added to the catalog: {MediaType} {TmdbId}.")]
    private static partial void LogAddFailed(
        ILogger logger,
        Exception exception,
        CatalogContentType mediaType,
        int tmdbId);
}

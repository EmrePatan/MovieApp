using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.Application.Services.Home;

public sealed class HotThisWeekService(
    ITrendingWeekDataProvider trendingWeekDataProvider,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    ICacheService cacheService,
    IOptions<HomeOptions> options,
    ILogger<HotThisWeekService> logger) : IHotThisWeekService
{
    private readonly HomeOptions _options = options.Value;

    public async Task<IReadOnlyList<SearchItem>> GetItemsAsync(
        SearchContentType type,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0)
        {
            return [];
        }

        var cacheKey = HotThisWeekCacheKeys.Create(type, maxItems);
        var cached = await cacheService.GetAsync<HotThisWeekCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Items;
        }

        try
        {
            var items = await LoadAndIngestAsync(type, maxItems, cancellationToken);

            await cacheService.SetAsync(
                cacheKey,
                new HotThisWeekCacheEntry { Items = items },
                TimeSpan.FromMinutes(_options.HotThisWeekCacheTtlMinutes),
                cancellationToken);

            return items;
        }
        catch (Exception exception)
        {
            HotThisWeekLogMessages.LogTrendingProviderFailed(logger, exception);
            return [];
        }
    }

    private async Task<IReadOnlyList<SearchItem>> LoadAndIngestAsync(
        SearchContentType type,
        int maxItems,
        CancellationToken cancellationToken)
    {
        var trendingItems = await trendingWeekDataProvider.GetTrendingWeekAsync(cancellationToken);
        if (trendingItems.Count == 0)
        {
            return [];
        }

        var filteredTrending = trendingItems
            .Where(item => MatchesTypeFilter(item, type))
            .Take(maxItems)
            .ToList();

        if (filteredTrending.Count == 0)
        {
            return [];
        }

        var movieSummaries = new List<MovieProviderSummary>();
        var tvSummaries = new List<TvShowProviderSummary>();

        foreach (var item in filteredTrending)
        {
            if (item.MediaType == "movie")
            {
                movieSummaries.Add(ToMovieSummary(item));
            }
            else if (item.MediaType == "tv")
            {
                tvSummaries.Add(ToTvSummary(item));
            }
        }

        var movieIds = movieSummaries.Count > 0
            ? await movieRepository.EnsureFromSummariesAsync(movieSummaries, cancellationToken)
            : new Dictionary<int, Guid>();

        var tvIds = tvSummaries.Count > 0
            ? await tvShowRepository.EnsureFromSummariesAsync(tvSummaries, cancellationToken)
            : new Dictionary<int, Guid>();

        var items = new List<SearchItem>();

        foreach (var trendingItem in filteredTrending)
        {
            if (trendingItem.MediaType == "movie" &&
                movieIds.TryGetValue(trendingItem.TmdbId, out var movieId))
            {
                var summary = movieSummaries.First(summary => summary.TmdbId == trendingItem.TmdbId);
                items.Add(ProviderSearchMapper.ToSearchItem(summary, movieId));
            }
            else if (trendingItem.MediaType == "tv" &&
                     tvIds.TryGetValue(trendingItem.TmdbId, out var tvId))
            {
                var summary = tvSummaries.First(summary => summary.TmdbId == trendingItem.TmdbId);
                items.Add(ProviderSearchMapper.ToSearchItem(summary, tvId));
            }
        }

        return items;
    }

    private static bool MatchesTypeFilter(TrendingWeekProviderItem item, SearchContentType type) =>
        type switch
        {
            SearchContentType.Movie => item.MediaType == "movie",
            SearchContentType.Tv => item.MediaType == "tv",
            _ => item.MediaType is "movie" or "tv"
        };

    private static MovieProviderSummary ToMovieSummary(TrendingWeekProviderItem item) =>
        new(
            ExternalId: $"tmdb-{item.TmdbId}",
            TmdbId: item.TmdbId,
            TvdbId: null,
            ImdbId: null,
            Title: item.Title,
            Overview: item.Overview,
            ReleaseDate: item.ReleaseDate,
            PosterPath: item.PosterPath,
            VoteAverage: item.VoteAverage,
            VoteCount: item.VoteCount);

    private static TvShowProviderSummary ToTvSummary(TrendingWeekProviderItem item) =>
        new(
            ExternalId: $"tmdb-{item.TmdbId}",
            TmdbId: item.TmdbId,
            TvdbId: null,
            ImdbId: null,
            Title: item.Title,
            OriginalTitle: item.OriginalTitle,
            Overview: item.Overview,
            FirstAirDate: item.ReleaseDate,
            PosterPath: item.PosterPath,
            BackdropPath: item.BackdropPath,
            OriginalLanguage: null,
            VoteAverage: item.VoteAverage,
            VoteCount: item.VoteCount);
}

internal sealed class HotThisWeekCacheEntry
{
    public required IReadOnlyList<SearchItem> Items { get; init; }
}

using System.Globalization;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.Application.Services.Home;

internal static class TrendingWeekCatalogMapper
{
    internal static MovieProviderSummary ToMovieSummary(TrendingWeekProviderItem item) =>
        new(
            item.TmdbId.ToString(CultureInfo.InvariantCulture),
            item.TmdbId,
            null,
            null,
            item.Title,
            item.Overview,
            item.ReleaseDate,
            item.PosterPath,
            item.VoteAverage,
            item.VoteCount);

    internal static TvShowProviderSummary ToTvSummary(TrendingWeekProviderItem item) =>
        new(
            item.TmdbId.ToString(CultureInfo.InvariantCulture),
            item.TmdbId,
            null,
            null,
            item.Title,
            item.OriginalTitle,
            item.Overview,
            item.ReleaseDate,
            item.PosterPath,
            item.BackdropPath,
            null,
            item.VoteAverage,
            item.VoteCount);

    internal static List<SearchItem> MapOrderedItems(
        IReadOnlyList<TrendingWeekProviderItem> providerItems,
        IReadOnlyDictionary<int, Guid> movieIds,
        IReadOnlyDictionary<int, Guid> tvIds)
    {
        var items = new List<SearchItem>(providerItems.Count);

        foreach (var providerItem in providerItems)
        {
            if (providerItem.MediaType == "movie" &&
                movieIds.TryGetValue(providerItem.TmdbId, out var movieId))
            {
                items.Add(ProviderSearchMapper.ToSearchItem(ToMovieSummary(providerItem), movieId));
                continue;
            }

            if (providerItem.MediaType == "tv" &&
                tvIds.TryGetValue(providerItem.TmdbId, out var tvShowId))
            {
                items.Add(ProviderSearchMapper.ToSearchItem(ToTvSummary(providerItem), tvShowId));
            }
        }

        return items;
    }
}

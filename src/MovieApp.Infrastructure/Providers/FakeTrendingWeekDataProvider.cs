using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeTrendingWeekDataProvider : ITrendingWeekDataProvider
{
    public const int TrendingMovieOneTmdbId = 910001;
    public const int TrendingMovieTwoTmdbId = 910002;
    public const int TrendingTvOneTmdbId = 920001;
    public const int TrendingPersonTmdbId = 930001;

    public Task<IReadOnlyList<TrendingWeekProviderItem>> GetTrendingWeekAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TrendingWeekProviderItem>>(CreateItems());

    public Task<TrendingWeekPage> GetTrendingWeekPageAsync(
        int page,
        CancellationToken cancellationToken = default)
    {
        var items = CreateItems();
        if (page > 1)
        {
            return Task.FromResult(new TrendingWeekPage([], page, items.Count, 1));
        }

        return Task.FromResult(new TrendingWeekPage(items, 1, items.Count, 1));
    }

    private static List<TrendingWeekProviderItem> CreateItems()
    {
        var items = new List<TrendingWeekProviderItem>
        {
            CreateItem("movie", TrendingMovieOneTmdbId, "Trending Movie One", 8.1m, 1200),
            CreateItem("person", TrendingPersonTmdbId, "Trending Person", 0m, 0),
            CreateItem("tv", TrendingTvOneTmdbId, "Trending Show One", 8.4m, 900),
            CreateItem("movie", TrendingMovieTwoTmdbId, "Trending Movie Two", 7.9m, 800)
        };

        for (var index = items.Count; index < 16; index++)
        {
            var mediaType = index % 2 == 0 ? "tv" : "movie";
            items.Add(CreateItem(
                mediaType,
                910100 + index,
                $"Trending Title {index + 1}",
                7.5m,
                500 + index));
        }

        return items;
    }

    private static TrendingWeekProviderItem CreateItem(
        string mediaType,
        int tmdbId,
        string title,
        decimal voteAverage,
        int voteCount) =>
        new(
            mediaType,
            tmdbId,
            title,
            null,
            $"{title} overview.",
            new DateOnly(2025, 1, 1),
            $"/fake/trending-{tmdbId}.jpg",
            null,
            voteAverage,
            voteCount);
}

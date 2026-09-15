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
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TrendingWeekProviderItem> items =
        [
            new(
                "movie",
                TrendingMovieOneTmdbId,
                "Trending Movie One",
                null,
                "First trending movie.",
                new DateOnly(2025, 1, 1),
                "/fake/trending-movie-one.jpg",
                null,
                8.1m,
                1200),
            new(
                "tv",
                TrendingTvOneTmdbId,
                "Trending Show One",
                null,
                "First trending show.",
                new DateOnly(2024, 6, 1),
                "/fake/trending-show-one.jpg",
                null,
                8.4m,
                900),
            new(
                "movie",
                TrendingMovieTwoTmdbId,
                "Trending Movie Two",
                null,
                "Second trending movie.",
                new DateOnly(2025, 2, 1),
                "/fake/trending-movie-two.jpg",
                null,
                7.9m,
                800)
        ];

        return Task.FromResult(items);
    }
}

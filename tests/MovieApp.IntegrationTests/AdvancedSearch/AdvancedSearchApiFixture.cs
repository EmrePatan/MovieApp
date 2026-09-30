using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.IntegrationTests.AdvancedSearch;

public sealed class AdvancedSearchApiFixture : IAsyncLifetime
{
    public AdvancedSearchWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
        await cacheService.RemoveAsync(MovieSearchCacheKeys.Create(
            "Interstellar",
            SearchPaginationDefaults.DefaultPage,
            SearchPaginationDefaults.DefaultPageSize));
        await cacheService.RemoveAsync(TvShowSearchCacheKeys.Create(
            "breaking",
            SearchPaginationDefaults.DefaultPage,
            SearchPaginationDefaults.DefaultPageSize));
        await cacheService.RemoveAsync(MovieSearchCacheKeys.Create(
            FakeMovieDataProvider.RecentReleaseQueryToken,
            MovieSearchPagination.DefaultPage,
            MovieSearchPagination.DefaultPageSize));
        await cacheService.RemoveAsync(ExplorePreviewCacheKeys.Create(10, string.Empty));
        await cacheService.RemoveAsync(ExplorePreviewCacheKeys.Create(
            SearchPaginationDefaults.DefaultPageSize,
            string.Empty));
        await ClearDiscoveryBrowseCachesAsync(cacheService);

        await using var context = CreateContext();
        context.SearchHistories.RemoveRange(context.SearchHistories);
        context.WatchedEpisodes.RemoveRange(context.WatchedEpisodes);
        context.WatchedMovies.RemoveRange(context.WatchedMovies);
        context.Reviews.RemoveRange(context.Reviews);
        context.Ratings.RemoveRange(context.Ratings);
        context.WatchlistItems.RemoveRange(context.WatchlistItems);
        context.Watchlists.RemoveRange(context.Watchlists);
        context.Favorites.RemoveRange(context.Favorites);
        context.Episodes.RemoveRange(context.Episodes);
        context.Seasons.RemoveRange(context.Seasons);
        context.MovieGenres.RemoveRange(context.MovieGenres);
        context.TvShowGenres.RemoveRange(context.TvShowGenres);
        context.Movies.RemoveRange(context.Movies);
        context.TvShows.RemoveRange(context.TvShows);
        context.Genres.RemoveRange(context.Genres);
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();
    }

    private static async Task ClearDiscoveryBrowseCachesAsync(ICacheService cacheService)
    {
        var contentLocale = SupportedContentLocales.Default;
        var topRatedMovieBrowse = new DiscoverBrowseCriteria(
            DiscoverBrowseMode.TopRated,
            SearchContentType.Movie,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            null,
            1,
            1);
        var topRatedTvBrowse = topRatedMovieBrowse with { Type = SearchContentType.Tv };

        await cacheService.RemoveAsync(DiscoveryBrowseCacheKeys.Create(topRatedMovieBrowse, contentLocale));
        await cacheService.RemoveAsync(DiscoveryBrowseCacheKeys.Create(topRatedTvBrowse, contentLocale));
        await cacheService.RemoveAsync(
            DiscoveryTopRatedCacheKeys.Create(new DiscoveryCriteria(SearchContentType.Movie, 1, 1), contentLocale));
        await cacheService.RemoveAsync(
            DiscoveryTopRatedCacheKeys.Create(new DiscoveryCriteria(SearchContentType.Tv, 1, 1), contentLocale));
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        Factory.Dispose();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(AdvancedSearchIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

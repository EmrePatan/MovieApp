using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Common;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.RatingsReviews;

public sealed class RatingsReviewsApiFixture : IAsyncLifetime
{
    public RatingsReviewsWebApplicationFactory Factory { get; } = new();

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

        await using var context = CreateContext();
        context.Reviews.RemoveRange(context.Reviews);
        context.Ratings.RemoveRange(context.Ratings);
        context.WatchlistItems.RemoveRange(context.WatchlistItems);
        context.Watchlists.RemoveRange(context.Watchlists);
        context.Favorites.RemoveRange(context.Favorites);
        context.MovieGenres.RemoveRange(context.MovieGenres);
        context.TvShowGenres.RemoveRange(context.TvShowGenres);
        context.Movies.RemoveRange(context.Movies);
        context.TvShows.RemoveRange(context.TvShows);
        context.Genres.RemoveRange(context.Genres);
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();
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
            .UseNpgsql(RatingsReviewsIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

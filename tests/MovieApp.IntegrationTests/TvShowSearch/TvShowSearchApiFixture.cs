using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Common;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.IntegrationTests.TvShowSearch;

public sealed class TvShowSearchApiFixture : IAsyncLifetime
{
    public TvShowSearchWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var tracker = scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();
        tracker.Reset();

        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
        await cacheService.RemoveAsync(TvShowSearchCacheKeys.Create(
            "breaking",
            SearchPaginationDefaults.DefaultPage,
            SearchPaginationDefaults.DefaultPageSize));
        await cacheService.RemoveAsync(TvShowSearchCacheKeys.Create("breaking", 2, SearchPaginationDefaults.DefaultPageSize));

        await using var context = CreateContext();
        context.Episodes.RemoveRange(context.Episodes);
        context.Seasons.RemoveRange(context.Seasons);
        context.TvShowPeople.RemoveRange(context.TvShowPeople);
        context.TvShowGenres.RemoveRange(context.TvShowGenres);
        context.TvShows.RemoveRange(context.TvShows);
        context.MovieGenres.RemoveRange(context.MovieGenres);
        context.MoviePeople.RemoveRange(context.MoviePeople);
        context.Movies.RemoveRange(context.Movies);
        context.Genres.RemoveRange(context.Genres);
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
            .UseNpgsql(TvShowSearchIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

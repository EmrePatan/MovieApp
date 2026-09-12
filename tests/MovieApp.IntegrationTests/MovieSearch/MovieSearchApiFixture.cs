using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Movies;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.IntegrationTests.MovieSearch;

public sealed class MovieSearchApiFixture : IAsyncLifetime
{
    public MovieSearchWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var tracker = scope.ServiceProvider.GetRequiredService<MovieDataProviderCallTracker>();
        tracker.Reset();

        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
        await cacheService.RemoveAsync(MovieSearchCacheKeys.Create(
            "Interstellar",
            MovieSearchPagination.DefaultPage,
            MovieSearchPagination.DefaultPageSize));
        await cacheService.RemoveAsync(MovieSearchCacheKeys.Create("Interstellar", 2, MovieSearchPagination.DefaultPageSize));
        await cacheService.RemoveAsync(MovieSearchCacheKeys.Create(
            FakeMovieDataProvider.EmptyImdbCatalogQueryToken,
            MovieSearchPagination.DefaultPage,
            MovieSearchPagination.DefaultPageSize));
        await cacheService.RemoveAsync(MovieSearchCacheKeys.Create(
            FakeMovieDataProvider.DuplicateImdbCatalogQueryToken,
            MovieSearchPagination.DefaultPage,
            MovieSearchPagination.DefaultPageSize));

        await using var context = CreateContext();
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
            .UseNpgsql(MovieSearchIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

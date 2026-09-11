using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.FavoritesWatchlists;

public sealed class FavoritesWatchlistsApiFixture : IAsyncLifetime
{
    public FavoritesWatchlistsWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        _ = Factory;

        await using var context = CreateContext();
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
            .UseNpgsql(FavoritesWatchlistsIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

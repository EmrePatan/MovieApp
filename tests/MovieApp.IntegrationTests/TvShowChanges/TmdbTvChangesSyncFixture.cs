using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.IntegrationTests.TvShowChanges;

public sealed class TmdbTvChangesSyncFixture : IAsyncLifetime
{
    public TmdbTvChangesSyncWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        await using var context = CreateContext();
        context.UserReleaseNotificationEvents.RemoveRange(context.UserReleaseNotificationEvents);
        context.UserReleaseNotifications.RemoveRange(context.UserReleaseNotifications);
        context.CatalogReleaseEvents.RemoveRange(context.CatalogReleaseEvents);
        context.TvShowCatalogSyncStates.RemoveRange(context.TvShowCatalogSyncStates);
        context.TmdbTvChangesSyncCheckpoints.RemoveRange(context.TmdbTvChangesSyncCheckpoints);
        context.WatchedEpisodes.RemoveRange(context.WatchedEpisodes);
        context.WatchedMovies.RemoveRange(context.WatchedMovies);
        context.WatchlistItems.RemoveRange(context.WatchlistItems);
        context.Watchlists.RemoveRange(context.Watchlists);
        context.Favorites.RemoveRange(context.Favorites);
        context.CatalogFollows.RemoveRange(context.CatalogFollows);
        context.Episodes.RemoveRange(context.Episodes);
        context.Seasons.RemoveRange(context.Seasons);
        context.TvShowGenres.RemoveRange(context.TvShowGenres);
        context.TvShows.RemoveRange(context.TvShows);
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();
        ResetFakes();
    }

    public void ResetFakes()
    {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<FakeTmdbTvChangesProvider>().Reset();
        scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>().Reset();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        Factory.Dispose();
    }

    internal static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(TmdbTvChangesSyncIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

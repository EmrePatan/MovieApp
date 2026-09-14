using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.IntegrationTests.TvShowFollows;

public sealed class TvShowFollowsApiFixture : IAsyncLifetime
{
    public TvShowFollowsWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        _ = Factory;

        await using var context = CreateContext();
        context.UserReleaseNotificationEvents.RemoveRange(context.UserReleaseNotificationEvents);
        context.UserReleaseNotifications.RemoveRange(context.UserReleaseNotifications);
        context.CatalogReleaseEvents.RemoveRange(context.CatalogReleaseEvents);
        context.Episodes.RemoveRange(context.Episodes);
        context.Seasons.RemoveRange(context.Seasons);
        context.TvShowCatalogSyncStates.RemoveRange(context.TvShowCatalogSyncStates);
        context.TmdbTvChangesSyncCheckpoints.RemoveRange(context.TmdbTvChangesSyncCheckpoints);
        context.TvShowFollows.RemoveRange(context.TvShowFollows);
        context.Favorites.RemoveRange(context.Favorites);
        context.TvShowGenres.RemoveRange(context.TvShowGenres);
        context.TvShows.RemoveRange(context.TvShows);
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();
        ResetProviderTracker();
    }

    public void ResetProviderTracker()
    {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>().Reset();
    }

    public TvShowDataProviderCallTracker GetProviderTracker()
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();
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
            .UseNpgsql(TvShowFollowsIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

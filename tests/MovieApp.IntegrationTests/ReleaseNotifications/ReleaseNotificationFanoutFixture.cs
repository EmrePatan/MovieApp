using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.ReleaseNotifications;

public sealed class ReleaseNotificationFanoutFixture : IAsyncLifetime
{
    public ReleaseNotificationFanoutWebApplicationFactory Factory { get; } = new();

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
        context.TvShowFollows.RemoveRange(context.TvShowFollows);
        context.TvShows.RemoveRange(context.TvShows);
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();
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
            .UseNpgsql(ReleaseNotificationFanoutIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

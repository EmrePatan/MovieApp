using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.ReleaseDetection;

public sealed class ReleaseDetectorFixture : IAsyncLifetime
{
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
        context.Users.RemoveRange(context.Users);
        context.CatalogReleaseEvents.RemoveRange(context.CatalogReleaseEvents);
        context.Episodes.RemoveRange(context.Episodes);
        context.Seasons.RemoveRange(context.Seasons);
        context.TvShows.RemoveRange(context.TvShows);
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    internal static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ReleaseDetectorIntegrationDatabase.GetConnectionString())
            .Options);
}

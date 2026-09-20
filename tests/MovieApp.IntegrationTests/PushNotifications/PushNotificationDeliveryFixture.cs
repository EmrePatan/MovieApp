using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.PushNotifications;

public sealed class PushNotificationDeliveryFixture : IAsyncLifetime
{
    public PushNotificationDeliveryWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        Factory.ExpoPushClient.Reset();
        await using var context = CreateContext();
        context.PushNotificationDeliveries.RemoveRange(context.PushNotificationDeliveries);
        context.PushDevices.RemoveRange(context.PushDevices);
        context.UserReleaseNotificationEvents.RemoveRange(context.UserReleaseNotificationEvents);
        context.UserReleaseNotifications.RemoveRange(context.UserReleaseNotifications);
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
            .UseNpgsql(
                PushNotificationDeliveryIntegrationDatabase.GetConnectionString(),
                npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3))
            .Options;

        return new ApplicationDbContext(options);
    }
}

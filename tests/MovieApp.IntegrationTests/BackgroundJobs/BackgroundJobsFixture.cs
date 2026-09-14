using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;
using MovieApp.IntegrationTests.PushNotifications;
using MovieApp.IntegrationTests.ReleaseNotifications;

namespace MovieApp.IntegrationTests.BackgroundJobs;

public sealed class BackgroundJobsFixture : IAsyncLifetime
{
    public ReleaseNotificationFanoutWebApplicationFactory FanoutFactory { get; } = new();

    public BackgroundJobsWebApplicationFactory PushFactory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var fanoutContext = CreateFanoutContext();
        await fanoutContext.Database.MigrateAsync();

        await using var pushContext = CreatePushContext();
        await pushContext.Database.MigrateAsync();
    }

    public async Task ResetFanoutAsync()
    {
        await using var context = CreateFanoutContext();
        context.UserReleaseNotificationEvents.RemoveRange(context.UserReleaseNotificationEvents);
        context.UserReleaseNotifications.RemoveRange(context.UserReleaseNotifications);
        context.CatalogReleaseEvents.RemoveRange(context.CatalogReleaseEvents);
        context.CatalogFollows.RemoveRange(context.CatalogFollows);
        context.TvShows.RemoveRange(context.TvShows);
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();
    }

    public async Task ResetPushAsync()
    {
        PushFactory.ExpoPushClient.Reset();
        await using var context = CreatePushContext();
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
        await using var fanoutContext = CreateFanoutContext();
        await fanoutContext.Database.EnsureDeletedAsync();
        FanoutFactory.Dispose();

        await using var pushContext = CreatePushContext();
        await pushContext.Database.EnsureDeletedAsync();
        PushFactory.Dispose();
    }

    private static ApplicationDbContext CreateFanoutContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ReleaseNotificationFanoutIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ApplicationDbContext CreatePushContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(PushNotificationDeliveryIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Models.PushNotifications;
using MovieApp.Application.Services.PushNotifications;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Users;

namespace MovieApp.IntegrationTests.PushNotifications;

[CollectionDefinition("PushNotificationDelivery")]
public sealed class PushNotificationDeliveryTestsDefinition : ICollectionFixture<PushNotificationDeliveryFixture>;

[Collection("PushNotificationDelivery")]
public sealed class PushNotificationDeliveryIntegrationTests(PushNotificationDeliveryFixture fixture)
{
    private const string TokenA = "ExponentPushToken[aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa]";
    private const string TokenB = "ExponentPushToken[bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb]";

    [Fact]
    public async Task NotificationCreatesDeliveriesForActiveDevices()
    {
        await fixture.ResetAsync();
        var seed = await SeedNotificationWithDevicesAsync(deviceCount: 3);

        using var scope = fixture.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<IPushNotificationDeliveryPreparationService>()
            .PrepareAsync([seed.NotificationId]);

        Assert.Equal(3, result.DeliveriesCreated);

        await using var context = PushNotificationDeliveryFixture.CreateContext();
        Assert.Equal(3, await context.PushNotificationDeliveries.CountAsync());
    }

    [Fact]
    public async Task DeliveryPreparationIsIdempotent()
    {
        await fixture.ResetAsync();
        var seed = await SeedNotificationWithDevicesAsync(deviceCount: 2);
        using var scope = fixture.Factory.Services.CreateScope();
        var preparation = scope.ServiceProvider.GetRequiredService<IPushNotificationDeliveryPreparationService>();

        await preparation.PrepareAsync([seed.NotificationId]);
        var second = await preparation.PrepareAsync([seed.NotificationId]);

        Assert.Equal(0, second.DeliveriesCreated);
        await using var context = PushNotificationDeliveryFixture.CreateContext();
        Assert.Equal(2, await context.PushNotificationDeliveries.CountAsync());
    }

    [Fact]
    public async Task InactiveDeviceDoesNotReceiveDelivery()
    {
        await fixture.ResetAsync();
        var seed = await SeedNotificationWithDevicesAsync(deviceCount: 1, active: false);
        using var scope = fixture.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<IPushNotificationDeliveryPreparationService>()
            .PrepareAsync([seed.NotificationId]);

        Assert.Equal(0, result.DeliveriesCreated);
    }

    [Fact]
    public async Task SuccessfulTicketStoresExpoTicketId()
    {
        await fixture.ResetAsync();
        var seed = await SeedNotificationWithDevicesAsync(deviceCount: 1);
        fixture.Factory.ExpoPushClient.ConfigureSendSuccess("ticket-123");

        using var scope = fixture.Factory.Services.CreateScope();
        var preparation = scope.ServiceProvider.GetRequiredService<IPushNotificationDeliveryPreparationService>();
        var dispatch = scope.ServiceProvider.GetRequiredService<IPushNotificationDispatchService>();

        await preparation.PrepareAsync([seed.NotificationId]);
        await dispatch.DispatchDueAsync();

        await using var context = PushNotificationDeliveryFixture.CreateContext();
        var delivery = await context.PushNotificationDeliveries.SingleAsync();
        Assert.Equal(PushNotificationDeliveryStatus.Sent, delivery.Status);
        Assert.Equal("ticket-123", delivery.ExpoTicketId);
        Assert.Equal(1, fixture.Factory.ExpoPushClient.SendCallCount);
    }

    [Fact]
    public async Task SuccessfulReceiptMarksDelivered()
    {
        await fixture.ResetAsync();
        var seed = await SeedNotificationWithDevicesAsync(deviceCount: 1);
        fixture.Factory.ExpoPushClient.ConfigureSendSuccess("ticket-delivered");
        fixture.Factory.ExpoPushClient.ConfigureReceipt(
            "ticket-delivered",
            new ExpoPushReceiptResult("ticket-delivered", true, null, null, false, false));

        using var scope = fixture.Factory.Services.CreateScope();
        var preparation = scope.ServiceProvider.GetRequiredService<IPushNotificationDeliveryPreparationService>();
        var dispatch = scope.ServiceProvider.GetRequiredService<IPushNotificationDispatchService>();
        var receipts = scope.ServiceProvider.GetRequiredService<IPushNotificationReceiptService>();

        await preparation.PrepareAsync([seed.NotificationId]);
        await dispatch.DispatchDueAsync();
        await receipts.ProcessReceiptsAsync();

        await using var context = PushNotificationDeliveryFixture.CreateContext();
        var delivery = await context.PushNotificationDeliveries.SingleAsync();
        Assert.Equal(PushNotificationDeliveryStatus.Delivered, delivery.Status);
        Assert.NotNull(delivery.DeliveredAtUtc);
    }

    [Fact]
    public async Task DeviceNotRegisteredDeactivatesPushDevice()
    {
        await fixture.ResetAsync();
        var seed = await SeedNotificationWithDevicesAsync(deviceCount: 1);
        fixture.Factory.ExpoPushClient.ConfigureSendError("DeviceNotRegistered", permanent: true);

        using var scope = fixture.Factory.Services.CreateScope();
        var preparation = scope.ServiceProvider.GetRequiredService<IPushNotificationDeliveryPreparationService>();
        var dispatch = scope.ServiceProvider.GetRequiredService<IPushNotificationDispatchService>();

        await preparation.PrepareAsync([seed.NotificationId]);
        await dispatch.DispatchDueAsync();

        await using var context = PushNotificationDeliveryFixture.CreateContext();
        var delivery = await context.PushNotificationDeliveries.SingleAsync();
        var device = await context.PushDevices.SingleAsync();
        Assert.Equal(PushNotificationDeliveryStatus.PermanentFailure, delivery.Status);
        Assert.False(device.IsActive);
    }

    [Fact]
    public async Task RetryableFailureSchedulesRetry()
    {
        await fixture.ResetAsync();
        var seed = await SeedNotificationWithDevicesAsync(deviceCount: 1);
        fixture.Factory.ExpoPushClient.ConfigureSendError("ProviderError", permanent: false);

        using var scope = fixture.Factory.Services.CreateScope();
        var preparation = scope.ServiceProvider.GetRequiredService<IPushNotificationDeliveryPreparationService>();
        var dispatch = scope.ServiceProvider.GetRequiredService<IPushNotificationDispatchService>();

        await preparation.PrepareAsync([seed.NotificationId]);
        await dispatch.DispatchDueAsync();

        await using var context = PushNotificationDeliveryFixture.CreateContext();
        var delivery = await context.PushNotificationDeliveries.SingleAsync();
        Assert.Equal(PushNotificationDeliveryStatus.RetryableFailure, delivery.Status);
        Assert.NotNull(delivery.NextAttemptAtUtc);
        Assert.Equal(1, delivery.AttemptCount);
    }

    [Fact]
    public async Task ReassignedDeviceCannotReceivePreviousUsersNotification()
    {
        await fixture.ResetAsync();
        var seed = await SeedNotificationWithDevicesAsync(deviceCount: 1);
        using var scope = fixture.Factory.Services.CreateScope();
        var preparation = scope.ServiceProvider.GetRequiredService<IPushNotificationDeliveryPreparationService>();
        await preparation.PrepareAsync([seed.NotificationId]);

        await using (var context = PushNotificationDeliveryFixture.CreateContext())
        {
            var otherUser = User.Create(
                Guid.NewGuid(),
                $"reassigned-{Guid.NewGuid():N}@example.com",
                "hash",
                "Other User",
                DateTime.UtcNow);
            context.Users.Add(otherUser);
            var device = await context.PushDevices.SingleAsync();
            device.UserId = otherUser.Id;
            device.UpdatedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        var dispatch = scope.ServiceProvider.GetRequiredService<IPushNotificationDispatchService>();
        await dispatch.DispatchDueAsync();

        Assert.Equal(0, fixture.Factory.ExpoPushClient.SendCallCount);

        await using var verifyContext = PushNotificationDeliveryFixture.CreateContext();
        var delivery = await verifyContext.PushNotificationDeliveries.SingleAsync();
        Assert.Equal(PushNotificationDeliveryStatus.PermanentFailure, delivery.Status);
        Assert.Equal("DeviceOwnershipMismatch", delivery.LastErrorCode);
    }

    [Fact]
    public async Task ConcurrentDispatchDoesNotDoubleSend()
    {
        await fixture.ResetAsync();
        var seed = await SeedNotificationWithDevicesAsync(deviceCount: 1);
        fixture.Factory.ExpoPushClient.ConfigureSendSuccess("ticket-concurrent");

        using var preparationScope = fixture.Factory.Services.CreateScope();
        await preparationScope.ServiceProvider
            .GetRequiredService<IPushNotificationDeliveryPreparationService>()
            .PrepareAsync([seed.NotificationId]);

        await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            using var scope = fixture.Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IPushNotificationDispatchService>().DispatchDueAsync();
        }));

        Assert.Equal(1, fixture.Factory.ExpoPushClient.SendCallCount);
        await using var context = PushNotificationDeliveryFixture.CreateContext();
        Assert.Equal(1, await context.PushNotificationDeliveries.CountAsync());
    }

    [Fact]
    public async Task ReprocessingDoesNotDuplicateDeliveryRows()
    {
        await fixture.ResetAsync();
        var seed = await SeedNotificationWithDevicesAsync(deviceCount: 2);

        await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            using var scope = fixture.Factory.Services.CreateScope();
            await scope.ServiceProvider
                .GetRequiredService<IPushNotificationDeliveryPreparationService>()
                .PrepareAsync([seed.NotificationId]);
        }));

        await using var context = PushNotificationDeliveryFixture.CreateContext();
        Assert.Equal(2, await context.PushNotificationDeliveries.CountAsync());
    }

    private static async Task<SeedResult> SeedNotificationWithDevicesAsync(int deviceCount, bool active = true)
    {
        await using var context = PushNotificationDeliveryFixture.CreateContext();
        var user = User.Create(Guid.NewGuid(), $"push-{Guid.NewGuid():N}@example.com", "hash", "Push User", DateTime.UtcNow);
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            Title = "Push Show",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        context.TvShows.Add(tvShow);

        var notification = new UserReleaseNotification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TvShowId = tvShow.Id,
            NotificationType = UserReleaseNotificationType.NewEpisodes,
            Status = UserReleaseNotificationStatus.Pending,
            AggregationWindowKey = "2026-09-15",
            Title = "Push Show",
            Body = "1 new episode",
            CreatedAtUtc = DateTime.UtcNow
        };
        context.UserReleaseNotifications.Add(notification);

        for (var index = 0; index < deviceCount; index++)
        {
            context.PushDevices.Add(new PushDevice
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ExpoPushToken = index == 0
                    ? TokenA
                    : $"ExponentPushToken[cccccccccccccccccccccccccc{index:D2}]",
                Platform = PushDevicePlatform.Android,
                IsActive = active,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
        return new SeedResult(user.Id, notification.Id);
    }

    private sealed record SeedResult(Guid UserId, Guid NotificationId);
}

using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.PushNotifications;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.PushNotifications;
using MovieApp.Application.Services.PushNotifications;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.PushNotifications;

public sealed class PushNotificationDeliveryServiceTests
{
    private static readonly DateTime UtcNow = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Preparation_CreatesOneDeliveryPerActiveDevice()
    {
        var repository = new FakeDeliveryRepository { CreatedCount = 2 };
        var service = new PushNotificationDeliveryPreparationService(repository);

        var result = await service.PrepareAsync([Guid.NewGuid(), Guid.NewGuid()]);

        Assert.Equal(2, result.NotificationsProcessed);
        Assert.Equal(2, result.DeliveriesCreated);
    }

    [Fact]
    public async Task Preparation_IsIdempotentWhenRepositoryReturnsZero()
    {
        var repository = new FakeDeliveryRepository { CreatedCount = 0 };
        var service = new PushNotificationDeliveryPreparationService(repository);

        var notificationId = Guid.NewGuid();
        await service.PrepareAsync([notificationId]);
        var second = await service.PrepareAsync([notificationId]);

        Assert.Equal(0, second.DeliveriesCreated);
        Assert.Equal(2, repository.CreateCalls);
    }

    [Fact]
    public async Task Preparation_WithZeroDevices_IsNotAnError()
    {
        var repository = new FakeDeliveryRepository { CreatedCount = 0 };
        var service = new PushNotificationDeliveryPreparationService(repository);

        var result = await service.PrepareAsync([Guid.NewGuid()]);

        Assert.Equal(1, result.NotificationsProcessed);
        Assert.Equal(0, result.DeliveriesCreated);
    }

    [Fact]
    public async Task Dispatch_TicketSuccess_MarksSentNotDelivered()
    {
        var delivery = CreateDelivery();
        var repository = new FakeDeliveryRepository
        {
            ClaimedDeliveries = [delivery]
        };
        var expo = new FakeExpoPushClient
        {
            SendResults =
            [
                new ExpoPushSendResult(delivery.Id, true, "ticket-1", null, null, false, false)
            ]
        };

        var service = CreateDispatchService(repository, expo);
        var result = await service.DispatchDueAsync();

        Assert.Equal(1, result.SentCount);
        Assert.Equal(PushNotificationDeliveryStatus.Sent, delivery.Status);
        Assert.Equal("ticket-1", delivery.ExpoTicketId);
        Assert.Null(delivery.DeliveredAtUtc);
        Assert.Equal(1, delivery.AttemptCount);
    }

    [Fact]
    public async Task Dispatch_InvalidToken_MarksPermanentFailureAndDeactivatesDevice()
    {
        var delivery = CreateDelivery();
        var repository = new FakeDeliveryRepository { ClaimedDeliveries = [delivery] };
        var expo = new FakeExpoPushClient
        {
            SendResults =
            [
                new ExpoPushSendResult(
                    delivery.Id,
                    false,
                    null,
                    "DeviceNotRegistered",
                    "Device not registered",
                    true,
                    false)
            ]
        };

        await CreateDispatchService(repository, expo).DispatchDueAsync();

        Assert.Equal(PushNotificationDeliveryStatus.PermanentFailure, delivery.Status);
        Assert.False(delivery.PushDevice.IsActive);
    }

    [Fact]
    public async Task Dispatch_TransientError_SchedulesRetry()
    {
        var delivery = CreateDelivery();
        var repository = new FakeDeliveryRepository { ClaimedDeliveries = [delivery] };
        var expo = new FakeExpoPushClient
        {
            SendResults =
            [
                new ExpoPushSendResult(
                    delivery.Id,
                    false,
                    null,
                    "ProviderError",
                    "Temporary",
                    false,
                    true)
            ]
        };

        await CreateDispatchService(repository, expo).DispatchDueAsync();

        Assert.Equal(PushNotificationDeliveryStatus.RetryableFailure, delivery.Status);
        Assert.NotNull(delivery.NextAttemptAtUtc);
        Assert.Equal(1, delivery.AttemptCount);
    }

    [Fact]
    public async Task Dispatch_MaxAttempts_MarksPermanentFailure()
    {
        var delivery = CreateDelivery();
        delivery.AttemptCount = 4;
        var repository = new FakeDeliveryRepository { ClaimedDeliveries = [delivery] };
        var expo = new FakeExpoPushClient
        {
            SendResults =
            [
                new ExpoPushSendResult(
                    delivery.Id,
                    false,
                    null,
                    "ProviderError",
                    "Temporary",
                    false,
                    true)
            ]
        };

        await CreateDispatchService(repository, expo).DispatchDueAsync();

        Assert.Equal(PushNotificationDeliveryStatus.PermanentFailure, delivery.Status);
        Assert.Equal(5, delivery.AttemptCount);
        Assert.Null(delivery.NextAttemptAtUtc);
    }

    [Fact]
    public async Task Dispatch_OwnershipMismatch_DoesNotSend()
    {
        var delivery = CreateDelivery();
        delivery.PushDevice.UserId = Guid.NewGuid();
        var repository = new FakeDeliveryRepository { ClaimedDeliveries = [delivery] };
        var expo = new FakeExpoPushClient();

        await CreateDispatchService(repository, expo).DispatchDueAsync();

        Assert.Empty(expo.SentMessages);
        Assert.Equal(PushNotificationDeliveryStatus.PermanentFailure, delivery.Status);
        Assert.Equal("DeviceOwnershipMismatch", delivery.LastErrorCode);
    }

    [Fact]
    public async Task Dispatch_InactiveDevice_IsExcluded()
    {
        var delivery = CreateDelivery();
        delivery.PushDevice.IsActive = false;
        var repository = new FakeDeliveryRepository { ClaimedDeliveries = [delivery] };
        var expo = new FakeExpoPushClient();

        await CreateDispatchService(repository, expo).DispatchDueAsync();

        Assert.Empty(expo.SentMessages);
        Assert.Equal(PushNotificationDeliveryStatus.PermanentFailure, delivery.Status);
    }

    [Fact]
    public async Task Dispatch_MalformedExpoResponse_ThrowsWithoutMarkingSent()
    {
        var delivery = CreateDelivery();
        var repository = new FakeDeliveryRepository { ClaimedDeliveries = [delivery] };
        var expo = new FakeExpoPushClient { ThrowOnSend = true, SendResults = [] };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateDispatchService(repository, expo).DispatchDueAsync());

        Assert.Equal(PushNotificationDeliveryStatus.Pending, delivery.Status);
    }

    [Fact]
    public async Task Dispatch_WhenDisabled_DoesNotCallExpo()
    {
        var delivery = CreateDelivery();
        var repository = new FakeDeliveryRepository { ClaimedDeliveries = [delivery] };
        var expo = new FakeExpoPushClient();

        await CreateDispatchService(repository, expo, enabled: false).DispatchDueAsync();

        Assert.Empty(expo.SentMessages);
        Assert.Equal(0, repository.ClaimCalls);
    }

    [Fact]
    public async Task Receipt_Success_MarksDelivered()
    {
        var delivery = CreateDelivery();
        delivery.Status = PushNotificationDeliveryStatus.Sent;
        delivery.ExpoTicketId = "ticket-1";
        var repository = new FakeDeliveryRepository { SentDeliveries = [delivery] };
        var expo = new FakeExpoPushClient
        {
            ReceiptResults = [new ExpoPushReceiptResult("ticket-1", true, null, null, false, false)]
        };

        await CreateReceiptService(repository, expo).ProcessReceiptsAsync();

        Assert.Equal(PushNotificationDeliveryStatus.Delivered, delivery.Status);
        Assert.NotNull(delivery.DeliveredAtUtc);
    }

    [Fact]
    public async Task Receipt_TransientError_SchedulesRetryWithoutDuplicatingDelivery()
    {
        var delivery = CreateDelivery();
        delivery.Status = PushNotificationDeliveryStatus.Sent;
        delivery.ExpoTicketId = "ticket-1";
        var repository = new FakeDeliveryRepository { SentDeliveries = [delivery] };
        var expo = new FakeExpoPushClient
        {
            ReceiptResults =
            [
                new ExpoPushReceiptResult(
                    "ticket-1",
                    false,
                    "ProviderError",
                    "Temporary",
                    false,
                    true)
            ]
        };

        await CreateReceiptService(repository, expo).ProcessReceiptsAsync();

        Assert.Equal(PushNotificationDeliveryStatus.RetryableFailure, delivery.Status);
        Assert.Null(delivery.ExpoTicketId);
        Assert.NotNull(delivery.NextAttemptAtUtc);
        Assert.Equal(1, delivery.AttemptCount);
    }

    [Fact]
    public async Task Receipt_DeviceNotRegistered_DeactivatesDevice()
    {
        var delivery = CreateDelivery();
        delivery.Status = PushNotificationDeliveryStatus.Sent;
        delivery.ExpoTicketId = "ticket-1";
        var repository = new FakeDeliveryRepository { SentDeliveries = [delivery] };
        var expo = new FakeExpoPushClient
        {
            ReceiptResults =
            [
                new ExpoPushReceiptResult(
                    "ticket-1",
                    false,
                    "DeviceNotRegistered",
                    "Unregistered",
                    true,
                    false)
            ]
        };

        await CreateReceiptService(repository, expo).ProcessReceiptsAsync();

        Assert.Equal(PushNotificationDeliveryStatus.PermanentFailure, delivery.Status);
        Assert.False(delivery.PushDevice.IsActive);
    }

    private static PushNotificationDispatchService CreateDispatchService(
        FakeDeliveryRepository repository,
        FakeExpoPushClient expo,
        bool enabled = true) =>
        new(repository, expo, Options.Create(new PushNotificationsOptions
        {
            Enabled = enabled,
            MaxAttempts = 5,
            DispatchBatchSize = 100
        }));

    private static PushNotificationReceiptService CreateReceiptService(
        FakeDeliveryRepository repository,
        FakeExpoPushClient expo) =>
        new(repository, expo, Options.Create(new PushNotificationsOptions
        {
            Enabled = true,
            MaxAttempts = 5,
            DispatchBatchSize = 100
        }));

    private static PushNotificationDelivery CreateDelivery()
    {
        var userId = Guid.NewGuid();
        return new PushNotificationDelivery
        {
            Id = Guid.NewGuid(),
            Status = PushNotificationDeliveryStatus.Pending,
            AttemptCount = 0,
            UserReleaseNotificationId = Guid.NewGuid(),
            PushDeviceId = Guid.NewGuid(),
            UserReleaseNotification = new UserReleaseNotification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TvShowId = Guid.NewGuid(),
                NotificationType = UserReleaseNotificationType.NewEpisodes,
                Title = "Show",
                Body = "1 new episode",
                NotificationEvents = [new UserReleaseNotificationEvent()]
            },
            PushDevice = new PushDevice
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ExpoPushToken = "ExponentPushToken[abcdefghijklmnopqrstuvwxyz123456]",
                IsActive = true
            }
        };
    }

    private sealed class FakeDeliveryRepository : IPushNotificationDeliveryRepository
    {
        public int CreatedCount { get; init; }

        public int CreateCalls { get; private set; }

        public int ClaimCalls { get; private set; }

        public IReadOnlyList<PushNotificationDelivery> ClaimedDeliveries { get; init; } = [];

        public IReadOnlyList<PushNotificationDelivery> SentDeliveries { get; init; } = [];

        public Task<int> CreateMissingDeliveriesAsync(
            IReadOnlyCollection<Guid> userReleaseNotificationIds,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(CreatedCount);
        }

        public Task<IReadOnlyList<Guid>> GetNotificationIdsNeedingPreparationAsync(
            int batchSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyList<PushNotificationDelivery>> ClaimDueDeliveriesAsync(
            int batchSize,
            DateTime utcNow,
            DateTime claimUntilUtc,
            Guid claimToken,
            CancellationToken cancellationToken = default)
        {
            ClaimCalls++;
            foreach (var delivery in ClaimedDeliveries)
            {
                delivery.ClaimToken = claimToken;
            }

            return Task.FromResult(ClaimedDeliveries);
        }

        public Task<IReadOnlyList<PushNotificationDelivery>> GetSentDeliveriesForReceiptAsync(
            int batchSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SentDeliveries);

        public Task SaveDeliveryUpdatesAsync(
            IReadOnlyCollection<PushNotificationDelivery> deliveries,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateNotificationStatusesAsync(
            IReadOnlyCollection<Guid> userReleaseNotificationIds,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeExpoPushClient : IExpoPushClient
    {
        public bool ThrowOnSend { get; init; }

        public IReadOnlyList<ExpoPushSendResult> SendResults { get; init; } = [];

        public IReadOnlyList<ExpoPushReceiptResult> ReceiptResults { get; init; } = [];

        public List<PushNotificationMessage> SentMessages { get; } = [];

        public Task<IReadOnlyList<ExpoPushSendResult>> SendAsync(
            IReadOnlyList<PushNotificationMessage> messages,
            CancellationToken cancellationToken = default)
        {
            SentMessages.AddRange(messages);

            if (ThrowOnSend)
            {
                throw new InvalidOperationException("Malformed Expo response.");
            }

            return Task.FromResult(SendResults);
        }

        public Task<IReadOnlyList<ExpoPushReceiptResult>> GetReceiptsAsync(
            IReadOnlyCollection<string> ticketIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ReceiptResults);
    }
}

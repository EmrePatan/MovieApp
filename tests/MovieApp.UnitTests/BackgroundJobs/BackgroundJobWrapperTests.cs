using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Api.BackgroundJobs;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.PushNotifications;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.HotRelease;
using MovieApp.Application.Models.PushNotifications;
using MovieApp.Application.Models.ReleaseNotifications;
using MovieApp.Application.Models.TvShowChanges;
using MovieApp.Application.Services.HotRelease;
using MovieApp.Application.Services.PushNotifications;
using MovieApp.Application.Services.ReleaseNotifications;
using MovieApp.Application.Services.TvShowChanges;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.BackgroundJobs;

public sealed class BackgroundJobWrapperTests
{
    [Fact]
    public async Task TmdbJob_UsesUtcNowAndCallsSyncService()
    {
        var sync = new FakeTmdbChangesSyncService();
        var job = new TmdbTvChangesSyncJob(sync, NullLogger<TmdbTvChangesSyncJob>.Instance);

        await job.ExecuteAsync();

        Assert.NotNull(sync.ReceivedUtcNow);
        Assert.Equal(DateTimeKind.Utc, sync.ReceivedUtcNow!.Value.Kind);
    }

    [Fact]
    public async Task HotReleaseJob_UsesUtcBoundaryDate()
    {
        var service = new FakeHotReleaseCheckService();
        var job = new HotReleaseCheckJob(service, NullLogger<HotReleaseCheckJob>.Instance);

        await job.ExecuteAsync();

        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), service.ReceivedBoundaryDate);
    }

    [Fact]
    public async Task FanoutJob_DiscoversAndProcessesPendingEvents()
    {
        var eventId = Guid.NewGuid();
        var repository = new FakeFanoutRepository { PendingIds = [eventId] };
        var service = new FakeFanoutService();
        var job = new ReleaseNotificationFanoutJob(
            repository,
            service,
            Options.Create(new BackgroundJobsOptions { FanoutBatchSize = 25 }),
            NullLogger<ReleaseNotificationFanoutJob>.Instance);

        await job.ExecuteAsync();

        Assert.Equal([eventId], service.ProcessedIds);
        Assert.Equal(25, repository.RequestedBatchSize);
    }

    [Fact]
    public async Task PreparationJob_DiscoversAndPreparesNotifications()
    {
        var notificationId = Guid.NewGuid();
        var repository = new FakeDeliveryRepository { PendingNotificationIds = [notificationId] };
        var service = new FakePreparationService();
        var job = new PushDeliveryPreparationJob(
            repository,
            service,
            Options.Create(new BackgroundJobsOptions { PreparationBatchSize = 50 }),
            NullLogger<PushDeliveryPreparationJob>.Instance);

        await job.ExecuteAsync();

        Assert.Equal([notificationId], service.PreparedIds);
        Assert.Equal(50, repository.RequestedBatchSize);
    }

    [Fact]
    public async Task DispatchJob_CallsDispatchService()
    {
        var service = new FakeDispatchService();
        var job = new PushDispatchJob(service, NullLogger<PushDispatchJob>.Instance);

        await job.ExecuteAsync();

        Assert.Equal(1, service.Calls);
    }

    [Fact]
    public async Task ReceiptJob_CallsReceiptService()
    {
        var service = new FakeReceiptService();
        var job = new PushReceiptJob(service, NullLogger<PushReceiptJob>.Instance);

        await job.ExecuteAsync();

        Assert.Equal(1, service.Calls);
    }

    private sealed class FakeTmdbChangesSyncService : ITmdbTvChangesSyncService
    {
        public DateTime? ReceivedUtcNow { get; private set; }

        public Task<TmdbTvChangesSyncResult> SyncAsync(DateTime? utcNow = null, CancellationToken cancellationToken = default)
        {
            ReceivedUtcNow = utcNow;
            return Task.FromResult(new TmdbTvChangesSyncResult(1, 2, 3, DateOnly.FromDateTime(DateTime.UtcNow)));
        }
    }

    private sealed class FakeHotReleaseCheckService : IHotReleaseCheckService
    {
        public DateOnly ReceivedBoundaryDate { get; private set; }

        public Task<HotReleaseCheckResult> RunAsync(DateOnly boundaryDate, CancellationToken cancellationToken = default)
        {
            ReceivedBoundaryDate = boundaryDate;
            return Task.FromResult(new HotReleaseCheckResult(1, 1, 0, 0, 0));
        }
    }

    private sealed class FakeFanoutRepository : IReleaseNotificationFanoutRepository
    {
        public int RequestedBatchSize { get; private set; }

        public IReadOnlyList<Guid> PendingIds { get; init; } = [];

        public Task<IReadOnlyList<Guid>> GetPendingFanoutEventIdsAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            RequestedBatchSize = batchSize;
            return Task.FromResult(PendingIds);
        }

        public Task<IReadOnlyList<CatalogReleaseEvent>> GetEventsByIdsAsync(IReadOnlyCollection<Guid> catalogReleaseEventIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CatalogFollow>> GetEstablishedFollowsByTvShowIdsAsync(IReadOnlyCollection<Guid> tvShowIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, string>> GetTvShowTitlesByIdsAsync(IReadOnlyCollection<Guid> tvShowIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CatalogFollow>> GetMovieFollowsByMovieIdsAsync(IReadOnlyCollection<Guid> movieIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, string>> GetMovieTitlesByIdsAsync(IReadOnlyCollection<Guid> movieIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UserReleaseNotification>> GetNotificationsByBucketsAsync(IReadOnlyCollection<ReleaseNotificationBucketKey> bucketKeys, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<HashSet<(Guid UserId, Guid CatalogReleaseEventId)>> GetExistingEventLinksAsync(IReadOnlyCollection<Guid> catalogReleaseEventIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(int NotificationsCreated, int EventLinksCreated)> UpsertNotificationsAndLinksAsync(IReadOnlyList<UserReleaseNotification> newNotifications, IReadOnlyList<UserReleaseNotification> notificationsToUpdate, IReadOnlyList<UserReleaseNotificationEvent> newEventLinks, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeFanoutService : IReleaseNotificationFanoutService
    {
        public IReadOnlyList<Guid> ProcessedIds { get; private set; } = [];

        public Task<ReleaseNotificationFanoutResult> ProcessAsync(IReadOnlyCollection<Guid> catalogReleaseEventIds, CancellationToken cancellationToken = default)
        {
            ProcessedIds = catalogReleaseEventIds.ToList();
            return Task.FromResult(new ReleaseNotificationFanoutResult(ProcessedIds.Count, 1, 1, 1, 0, 0, 0));
        }
    }

    private sealed class FakeDeliveryRepository : IPushNotificationDeliveryRepository
    {
        public int RequestedBatchSize { get; private set; }

        public IReadOnlyList<Guid> PendingNotificationIds { get; init; } = [];

        public Task<IReadOnlyList<Guid>> GetNotificationIdsNeedingPreparationAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            RequestedBatchSize = batchSize;
            return Task.FromResult(PendingNotificationIds);
        }

        public Task<int> CreateMissingDeliveriesAsync(IReadOnlyCollection<Guid> userReleaseNotificationIds, DateTime utcNow, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PushNotificationDelivery>> ClaimDueDeliveriesAsync(int batchSize, DateTime utcNow, DateTime claimUntilUtc, Guid claimToken, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PushNotificationDelivery>> GetSentDeliveriesForReceiptAsync(int batchSize, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveDeliveryUpdatesAsync(IReadOnlyCollection<PushNotificationDelivery> deliveries, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateNotificationStatusesAsync(IReadOnlyCollection<Guid> userReleaseNotificationIds, DateTime utcNow, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakePreparationService : IPushNotificationDeliveryPreparationService
    {
        public IReadOnlyList<Guid> PreparedIds { get; private set; } = [];

        public Task<PushNotificationDeliveryPreparationResult> PrepareAsync(IReadOnlyCollection<Guid> userReleaseNotificationIds, CancellationToken cancellationToken = default)
        {
            PreparedIds = userReleaseNotificationIds.ToList();
            return Task.FromResult(new PushNotificationDeliveryPreparationResult(PreparedIds.Count, PreparedIds.Count));
        }
    }

    private sealed class FakeDispatchService : IPushNotificationDispatchService
    {
        public int Calls { get; private set; }

        public Task<PushNotificationDispatchResult> DispatchDueAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new PushNotificationDispatchResult(0, 0, 0, 0, 0));
        }
    }

    private sealed class FakeReceiptService : IPushNotificationReceiptService
    {
        public int Calls { get; private set; }

        public Task<PushNotificationReceiptResult> ProcessReceiptsAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new PushNotificationReceiptResult(0, 0, 0, 0));
        }
    }
}

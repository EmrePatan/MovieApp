using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.ExternalRatings;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.ExternalRatings;
using MovieApp.Application.Services.ExternalRatings;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.ExternalRatings;

public sealed class ExternalRatingsExpiredSnapshotTests
{
    [Fact]
    public async Task ExpiredNonNegativeSnapshotReturnsStaleRatingsWithoutBlockingOnRefresh()
    {
        var now = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var payload = new ExternalRatingSnapshotPayload
        {
            IsNegative = false,
            Ratings = [new ExternalRatingItem("imdb", 8.1m, 10)]
        };
        var snapshot = new ExternalRatingSnapshot
        {
            Id = Guid.NewGuid(),
            MediaType = CatalogContentType.Movie,
            TmdbId = 671,
            Provider = ExternalRatingsProvider.MdbList,
            PayloadJson = ExternalRatingSnapshotSerializer.Serialize(payload),
            FetchedAtUtc = now.AddDays(-20)
        };
        var refresh = new CountingRefreshService();
        var enqueuer = new CountingEnqueuer();
        var time = new FixedTimeProvider(now);
        var service = new ExternalRatingsAccessService(
            new FixedSnapshotRepository(snapshot),
            refresh,
            enqueuer,
            new OperationalFeatureState(),
            Options.Create(new ExternalRatingsOptions
            {
                Enabled = true,
                FreshHours = 72,
                StaleDays = 14,
                NegativeHours = 24
            }),
            time);

        var result = await service.GetAsync(CatalogContentType.Movie, 671, CancellationToken.None);

        Assert.True(result.IsStale);
        Assert.Equal("imdb", Assert.Single(result.Ratings).Source);
        Assert.Equal(0, refresh.Calls);
        Assert.Equal(1, enqueuer.Calls);
    }

    private sealed class FixedSnapshotRepository(ExternalRatingSnapshot snapshot) : IExternalRatingSnapshotRepository
    {
        public Task<ExternalRatingSnapshot?> GetAsync(
            CatalogContentType mediaType,
            int tmdbId,
            ExternalRatingsProvider provider,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ExternalRatingSnapshot?>(snapshot);

        public Task UpsertAsync(
            CatalogContentType mediaType,
            int tmdbId,
            ExternalRatingsProvider provider,
            ExternalRatingSnapshotPayload payload,
            DateTime fetchedAtUtc,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CountingRefreshService : IExternalRatingsRefreshService
    {
        public int Calls { get; private set; }

        public Task RefreshAsync(
            CatalogContentType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class CountingEnqueuer : IExternalRatingsRefreshJobEnqueuer
    {
        public int Calls { get; private set; }

        public void EnqueueRefresh(CatalogContentType mediaType, int tmdbId) => Calls++;
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private sealed class OperationalFeatureState : IExternalRatingsFeatureState
    {
        public bool IsOperational => true;
    }
}

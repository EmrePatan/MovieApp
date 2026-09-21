using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Application.Services.ReleaseDetection;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.ReleaseDetection;

public sealed class ReleaseDetectorTests
{
    private static readonly Guid TvShowId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
    private static readonly DateOnly Boundary = new(2026, 9, 15);

    [Fact]
    public async Task ScanTvShowAsync_UsesBoundedNumberOfRepositoryCalls()
    {
        var catalogRepository = new FakeReleaseDetectionCatalogRepository();
        var eventRepository = new FakeCatalogReleaseEventRepository();
        var detector = new ReleaseDetector(catalogRepository, eventRepository);

        await detector.ScanTvShowAsync(TvShowId, ReleaseDetectionMode.BoundaryCheck, Boundary);

        Assert.Equal(1, catalogRepository.GetSeasonsCallCount);
        Assert.Equal(1, eventRepository.GetDedupeKeysCallCount);
        Assert.Equal(1, eventRepository.TryAddCallCount);
    }

    [Fact]
    public async Task ScanTvShowAsync_ReturnsCreatedCountsByType()
    {
        var catalogRepository = new FakeReleaseDetectionCatalogRepository
        {
            Seasons =
            [
                new Season
                {
                    Id = Guid.NewGuid(),
                    TvShowId = TvShowId,
                    SeasonNumber = 1,
                    Episodes =
                    [
                        new Episode
                        {
                            Id = Guid.NewGuid(),
                            EpisodeNumber = 1,
                            AirDate = Boundary
                        },
                        new Episode
                        {
                            Id = Guid.NewGuid(),
                            EpisodeNumber = 2,
                            AirDate = Boundary
                        }
                    ]
                }
            ]
        };

        var eventRepository = new FakeCatalogReleaseEventRepository();
        var detector = new ReleaseDetector(catalogRepository, eventRepository);

        var result = await detector.ScanTvShowAsync(
            TvShowId,
            ReleaseDetectionMode.BaselineAbsorb,
            Boundary);

        Assert.Equal(3, result.EventsCreated);
        Assert.Equal(0, result.EventsAlreadyExisted);
        Assert.Equal(2, result.EpisodeEventsCreated);
        Assert.Equal(1, result.SeasonPremiereEventsCreated);
        Assert.Equal(3, result.CreatedEventIds.Count);
    }

    [Fact]
    public async Task ScanTvShowAsync_WithPreloadedSeasons_SkipsCatalogRepository()
    {
        var catalogRepository = new FakeReleaseDetectionCatalogRepository
        {
            Seasons =
            [
                new Season
                {
                    Id = Guid.NewGuid(),
                    TvShowId = TvShowId,
                    SeasonNumber = 1,
                    Episodes =
                    [
                        new Episode
                        {
                            Id = Guid.NewGuid(),
                            EpisodeNumber = 1,
                            AirDate = Boundary
                        }
                    ]
                }
            ]
        };

        var eventRepository = new FakeCatalogReleaseEventRepository();
        var detector = new ReleaseDetector(catalogRepository, eventRepository);

        await detector.ScanTvShowAsync(
            TvShowId,
            ReleaseDetectionMode.BaselineAbsorb,
            Boundary,
            catalogRepository.Seasons);

        Assert.Equal(0, catalogRepository.GetSeasonsCallCount);
    }

    [Fact]
    public async Task ScanTvShowAsync_HandlesConcurrentInsertAsAlreadyExisted()
    {
        var catalogRepository = new FakeReleaseDetectionCatalogRepository
        {
            Seasons =
            [
                new Season
                {
                    Id = Guid.NewGuid(),
                    TvShowId = TvShowId,
                    SeasonNumber = 1,
                    Episodes =
                    [
                        new Episode
                        {
                            Id = Guid.NewGuid(),
                            EpisodeNumber = 1,
                            AirDate = Boundary
                        }
                    ]
                }
            ]
        };

        var eventRepository = new FakeCatalogReleaseEventRepository
        {
            FailFirstBulkInsert = true,
            ExistingDedupeKeysAfterConflict =
            [
                MovieApp.Domain.Notifications.CatalogReleaseEventDedupeKey.ForEpisode(TvShowId, 1, 1)
            ]
        };

        var detector = new ReleaseDetector(catalogRepository, eventRepository);
        var result = await detector.ScanTvShowAsync(
            TvShowId,
            ReleaseDetectionMode.BoundaryCheck,
            Boundary);

        Assert.Equal(0, result.EventsCreated);
        Assert.True(result.EventsAlreadyExisted > 0);
    }

    private sealed class FakeReleaseDetectionCatalogRepository : IReleaseDetectionCatalogRepository
    {
        public int GetSeasonsCallCount { get; private set; }

        public IReadOnlyList<Season> Seasons { get; init; } = [];

        public Task<IReadOnlyList<Season>> GetSeasonsWithEpisodesAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default)
        {
            GetSeasonsCallCount++;
            return Task.FromResult(Seasons);
        }
    }

    private sealed class FakeCatalogReleaseEventRepository : ICatalogReleaseEventRepository
    {
        public int GetDedupeKeysCallCount { get; private set; }

        public int TryAddCallCount { get; private set; }

        public bool FailFirstBulkInsert { get; init; }

        public HashSet<string> ExistingDedupeKeysAfterConflict { get; init; } = new(StringComparer.Ordinal);

        private readonly HashSet<string> _dedupeKeys = new(StringComparer.Ordinal);

        public Task<HashSet<string>> GetDedupeKeysForTvShowAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default)
        {
            GetDedupeKeysCallCount++;
            return Task.FromResult(_dedupeKeys);
        }

        public Task<bool> ExistsByDedupeKeyAsync(
            string dedupeKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_dedupeKeys.Contains(dedupeKey));

        public Task<CatalogReleaseEventInsertResult> TryAddEventsAsync(
            IReadOnlyList<CatalogReleaseEvent> events,
            CancellationToken cancellationToken = default)
        {
            TryAddCallCount++;

            if (FailFirstBulkInsert)
            {
                foreach (var releaseEvent in events)
                {
                    _dedupeKeys.Add(releaseEvent.DedupeKey);
                }

                return Task.FromResult(new CatalogReleaseEventInsertResult(
                    0,
                    events.Count,
                    []));
            }

            foreach (var releaseEvent in events)
            {
                _dedupeKeys.Add(releaseEvent.DedupeKey);
            }

            return Task.FromResult(new CatalogReleaseEventInsertResult(
                events.Count,
                0,
                events.Select(releaseEvent => releaseEvent.Id).ToList()));
        }
    }
}

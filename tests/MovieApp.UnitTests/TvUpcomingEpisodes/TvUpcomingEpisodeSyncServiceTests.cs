using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.TvUpcomingEpisodes;
using MovieApp.Application.Services.TvUpcomingEpisodes;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.TvUpcomingEpisodes;

public sealed class TvUpcomingEpisodeSyncServiceTests
{
    private static readonly Guid TvShowId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTime SyncedAtUtc = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RunAsync_WhenProviderFails_PreservesExistingSyncTimestamp()
    {
        var repository = new FakeTvUpcomingEpisodeSyncRepository(
            [new TvUpcomingEpisodeSyncCandidate(TvShowId, 900101, null, null)]);
        var provider = new FakeSyncTvShowDataProvider(shouldFailGetTvShow: true);
        var syncStateRepository = new FakeCatalogSyncStateRepository(
            new Dictionary<Guid, DateTime?> { [TvShowId] = SyncedAtUtc.AddHours(-12) });
        var service = CreateService(repository, provider, syncStateRepository);

        var result = await service.RunAsync();

        Assert.Equal(1, result.Selected);
        Assert.Equal(0, result.Succeeded);
        Assert.Equal(1, result.Failed);
        Assert.Equal(SyncedAtUtc.AddHours(-12), syncStateRepository.GetLastSync(TvShowId));
        Assert.Equal(1, provider.GetTvShowCallCount);
    }

    [Fact]
    public async Task RunAsync_WhenNoUpcomingEpisode_MarksSyncTimestamp()
    {
        var repository = new FakeTvUpcomingEpisodeSyncRepository(
            [new TvUpcomingEpisodeSyncCandidate(TvShowId, 900101, null, null)]);
        var provider = new FakeSyncTvShowDataProvider(nextEpisode: null);
        var syncStateRepository = new FakeCatalogSyncStateRepository();
        var service = CreateService(repository, provider, syncStateRepository);

        var result = await service.RunAsync();

        Assert.Equal(1, result.Succeeded);
        Assert.Equal(0, result.Hydrated);
        Assert.NotNull(syncStateRepository.GetLastSync(TvShowId));
        Assert.Equal(0, provider.GetSeasonCallCount);
    }

    [Fact]
    public async Task RunAsync_WhenStale_SelectsCandidateForRefresh()
    {
        var repository = new FakeTvUpcomingEpisodeSyncRepository(
            [new TvUpcomingEpisodeSyncCandidate(TvShowId, 900101, null, null)]);
        var provider = new FakeSyncTvShowDataProvider(
            nextEpisode: new NextEpisodeToAirProviderSummary(1, 2, 3, "Next", new DateOnly(2026, 10, 1)));
        var syncStateRepository = new FakeCatalogSyncStateRepository(
            new Dictionary<Guid, DateTime?> { [TvShowId] = SyncedAtUtc.AddHours(-7) });
        var service = CreateService(repository, provider, syncStateRepository);

        var result = await service.RunAsync();

        Assert.Equal(1, result.Selected);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(1, result.Hydrated);
        Assert.Equal(1, provider.GetTvShowCallCount);
        Assert.Equal(1, provider.GetSeasonCallCount);
    }

    [Fact]
    public async Task RunAsync_WhenFresh_DoesNotSelectCandidate()
    {
        var repository = new FakeTvUpcomingEpisodeSyncRepository([]);
        var provider = new FakeSyncTvShowDataProvider();
        var syncStateRepository = new FakeCatalogSyncStateRepository(
            new Dictionary<Guid, DateTime?> { [TvShowId] = SyncedAtUtc });
        var service = CreateService(repository, provider, syncStateRepository);

        var result = await service.RunAsync();

        Assert.Equal(0, result.Selected);
        Assert.Equal(0, provider.GetTvShowCallCount);
    }

    [Fact]
    public async Task RunAsync_HydratesSeasonWhenNextEpisodeHasAirDate()
    {
        var repository = new FakeTvUpcomingEpisodeSyncRepository(
            [new TvUpcomingEpisodeSyncCandidate(TvShowId, 900101, null, null)]);
        var provider = new FakeSyncTvShowDataProvider(
            nextEpisode: new NextEpisodeToAirProviderSummary(55, 1, 4, "Four", new DateOnly(2026, 10, 2)));
        var seasonRepository = new FakeSeasonRepository();
        var syncStateRepository = new FakeCatalogSyncStateRepository();
        var service = CreateService(repository, provider, syncStateRepository, seasonRepository);

        await service.RunAsync();

        Assert.Equal(1, seasonRepository.UpsertCount);
        Assert.Equal(1, seasonRepository.LastSeasonNumber);
    }

    private static TvUpcomingEpisodeSyncService CreateService(
        ITvUpcomingEpisodeSyncRepository repository,
        ITvShowDataProvider provider,
        FakeCatalogSyncStateRepository syncStateRepository,
        ISeasonRepository? seasonRepository = null) =>
        new(
            repository,
            provider,
            new FixedExternalIdResolver("tmdb-900101"),
            seasonRepository ?? new FakeSeasonRepository(),
            syncStateRepository,
            Options.Create(new TvUpcomingEpisodeSyncOptions
            {
                Enabled = true,
                BatchSize = 25,
                FreshnessTtlHours = 6
            }));

    private sealed class FakeTvUpcomingEpisodeSyncRepository(IReadOnlyList<TvUpcomingEpisodeSyncCandidate> candidates)
        : ITvUpcomingEpisodeSyncRepository
    {
        public Task<IReadOnlyList<TvUpcomingEpisodeSyncCandidate>> SelectStaleFollowedShowsAsync(
            int batchSize,
            DateTime staleBeforeUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TvUpcomingEpisodeSyncCandidate>>(candidates.Take(batchSize).ToList());
    }

    private sealed class FakeSyncTvShowDataProvider : ITvShowDataProvider
    {
        private readonly bool _shouldFailGetTvShow;
        private readonly NextEpisodeToAirProviderSummary? _nextEpisode;

        public FakeSyncTvShowDataProvider(
            NextEpisodeToAirProviderSummary? nextEpisode = null,
            bool shouldFailGetTvShow = false)
        {
            _nextEpisode = nextEpisode;
            _shouldFailGetTvShow = shouldFailGetTvShow;
        }

        public int GetTvShowCallCount { get; private set; }

        public int GetSeasonCallCount { get; private set; }

        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderDetails?> GetTvShowAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default)
        {
            GetTvShowCallCount++;
            if (_shouldFailGetTvShow)
            {
                return Task.FromResult<TvShowProviderDetails?>(null);
            }

            return Task.FromResult<TvShowProviderDetails?>(new TvShowProviderDetails(
                externalId,
                900101,
                null,
                null,
                "Show",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0m,
                0,
                "Returning Series",
                [],
                [],
                _nextEpisode));
        }

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalTvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default)
        {
            GetSeasonCallCount++;
            return Task.FromResult<SeasonProviderDetails?>(new SeasonProviderDetails(
                externalTvShowId,
                1,
                null,
                seasonNumber,
                "Season",
                null,
                null,
                1,
                null,
                []));
        }

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalTvShowId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedExternalIdResolver(string externalId) : ITvShowExternalIdResolver
    {
        public string? Resolve(int? tmdbId, int? tvdbId, string? imdbId) => externalId;
    }

    private sealed class FakeSeasonRepository : ISeasonRepository
    {
        public int UpsertCount { get; private set; }

        public int? LastSeasonNumber { get; private set; }

        public Task<Season?> GetByTvShowIdAndSeasonNumberAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Season?>(null);

        public Task<Season> UpsertFromProviderAsync(
            Guid tvShowId,
            SeasonProviderDetails details,
            CancellationToken cancellationToken = default)
        {
            UpsertCount++;
            LastSeasonNumber = details.SeasonNumber;
            return Task.FromResult(new Season
            {
                Id = Guid.NewGuid(),
                TvShowId = tvShowId,
                SeasonNumber = details.SeasonNumber
            });
        }

        public async Task UpsertSeasonsFromProviderAsync(
            Guid tvShowId,
            IReadOnlyList<SeasonProviderDetails> details,
            CancellationToken cancellationToken = default)
        {
            foreach (var detail in details)
            {
                await UpsertFromProviderAsync(tvShowId, detail, cancellationToken);
            }
        }

        public Task<Season> UpsertSummaryFromProviderAsync(
            Guid tvShowId,
            SeasonProviderSummary summary,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeCatalogSyncStateRepository : ITvShowCatalogSyncStateRepository
    {
        private readonly Dictionary<Guid, DateTime?> _syncTimestamps;

        public FakeCatalogSyncStateRepository(Dictionary<Guid, DateTime?>? syncTimestamps = null)
        {
            _syncTimestamps = syncTimestamps ?? new Dictionary<Guid, DateTime?>();
        }

        public DateTime? GetLastSync(Guid tvShowId) =>
            _syncTimestamps.TryGetValue(tvShowId, out var value) ? value : null;

        public Task MarkUpcomingEpisodeSyncAsync(
            Guid tvShowId,
            DateTime syncedAtUtc,
            CancellationToken cancellationToken = default)
        {
            _syncTimestamps[tvShowId] = syncedAtUtc;
            return Task.CompletedTask;
        }

        public Task MarkRefreshedAsync(
            Guid tvShowId,
            TvShowCatalogRefreshReason reason,
            DateTime refreshedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkRefreshedBatchAsync(
            IReadOnlyList<Guid> tvShowIds,
            TvShowCatalogRefreshReason reason,
            DateTime refreshedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkChangeSignalAsync(
            Guid tvShowId,
            DateOnly changeSignalDate,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkChangesSyncAsync(
            Guid tvShowId,
            DateTime refreshedAtUtc,
            DateOnly changeSignalDate,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkHotReleaseAsync(
            Guid tvShowId,
            DateTime refreshedAtUtc,
            DateTime? nextHotCheckAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateNextHotCheckAsync(
            Guid tvShowId,
            DateTime? nextHotCheckAtUtc,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

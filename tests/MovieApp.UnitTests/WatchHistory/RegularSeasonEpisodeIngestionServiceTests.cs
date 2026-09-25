using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.WatchHistory;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.WatchHistory;

public sealed class RegularSeasonEpisodeIngestionServiceTests
{
    private static readonly Guid TvShowId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task IngestMissingSeasonsAsync_EmptySeasonNumbers_ReturnsZeroMetrics()
    {
        var service = CreateService(new BlockingSeasonProvider(), new RecordingSeasonRepository());

        var result = await service.IngestMissingSeasonsAsync(TvShowId, []);

        Assert.Equal(0, result.SeasonsPersisted);
        Assert.Equal(0, result.SaveChangesCount);
    }

    [Fact]
    public async Task IngestMissingSeasonsAsync_MissingTvShow_ThrowsNotFound()
    {
        var service = CreateService(
            new BlockingSeasonProvider(),
            new RecordingSeasonRepository(),
            missingTvShow: true);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.IngestMissingSeasonsAsync(TvShowId, [1]));
    }

    [Fact]
    public async Task IngestMissingSeasonsAsync_SingleSeason_FetchesAndPersistsOnce()
    {
        var provider = new BlockingSeasonProvider();
        var seasonRepository = new RecordingSeasonRepository();
        var catalogSync = new CountingCatalogSyncStateService();
        var service = CreateService(provider, seasonRepository, catalogSync: catalogSync);

        var result = await service.IngestMissingSeasonsAsync(TvShowId, [3]);

        Assert.Equal([3], provider.FetchedSeasonNumbers);
        Assert.Equal(1, seasonRepository.UpsertBatchCalls);
        Assert.Equal([3], seasonRepository.LastSeasonOrder);
        Assert.Equal(1, result.SeasonsPersisted);
        Assert.Equal(1, result.SaveChangesCount);
        Assert.Equal(1, catalogSync.MarkRefreshedCalls);
    }

    [Fact]
    public async Task IngestMissingSeasonsAsync_MultipleSeasons_BatchesSingleUpsertInSeasonOrder()
    {
        var provider = new BlockingSeasonProvider();
        var seasonRepository = new RecordingSeasonRepository();
        var service = CreateService(provider, seasonRepository);

        await service.IngestMissingSeasonsAsync(TvShowId, [2, 1, 3]);

        Assert.Equal(3, provider.FetchedSeasonNumbers.Count);
        Assert.Equal(1, seasonRepository.UpsertBatchCalls);
        Assert.Equal([1, 2, 3], seasonRepository.LastSeasonOrder);
    }

    [Fact]
    public async Task IngestMissingSeasonsAsync_ProviderReturnsNull_ThrowsNotFound()
    {
        var provider = new BlockingSeasonProvider { NullSeasonNumbers = new HashSet<int> { 2 } };
        var service = CreateService(provider, new RecordingSeasonRepository());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.IngestMissingSeasonsAsync(TvShowId, [1, 2]));
    }

    [Fact]
    public async Task IngestMissingSeasonsAsync_CancellationDuringFetch_ThrowsOperationCanceled()
    {
        var provider = new BlockingSeasonProvider { BlockUntilReleased = true };
        var service = CreateService(provider, new RecordingSeasonRepository());
        using var cts = new CancellationTokenSource();

        var ingestTask = service.IngestMissingSeasonsAsync(TvShowId, [1, 2, 3], cts.Token);
        await provider.WaitUntilBlockedAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ingestTask);
    }

    [Fact]
    public async Task IngestMissingSeasonsAsync_EightSeasons_PeakProviderConcurrencyDoesNotExceedSix()
    {
        var provider = new BlockingSeasonProvider { BlockUntilReleased = true };
        var service = CreateService(provider, new RecordingSeasonRepository());

        var ingestTask = service.IngestMissingSeasonsAsync(
            TvShowId,
            [1, 2, 3, 4, 5, 6, 7, 8]);

        await provider.WaitUntilPeakConcurrentAtLeastAsync(6, TimeSpan.FromSeconds(5));
        Assert.True(provider.PeakConcurrent <= 6, $"Peak concurrent was {provider.PeakConcurrent}");

        provider.ReleaseAllBlocked();
        var result = await ingestTask;

        Assert.Equal(8, result.SeasonsPersisted);
        Assert.True(result.ProviderSeasonFetchAccumulatedMs >= result.ProviderSeasonFetchWallMs);
    }

    [Fact]
    public async Task IngestMissingSeasonsAsync_PersistenceDoesNotOverlap()
    {
        var seasonRepository = new RecordingSeasonRepository();
        var service = CreateService(new BlockingSeasonProvider(), seasonRepository);

        await service.IngestMissingSeasonsAsync(TvShowId, [1, 2, 3, 4]);

        Assert.Equal(1, seasonRepository.UpsertBatchCalls);
        Assert.False(seasonRepository.ConcurrentUpsertDetected);
    }

    private static RegularSeasonEpisodeIngestionService CreateService(
        BlockingSeasonProvider provider,
        RecordingSeasonRepository seasonRepository,
        TvShow? tvShow = null,
        CountingCatalogSyncStateService? catalogSync = null,
        bool missingTvShow = false) =>
        new(
            new StubTvShowRepository(missingTvShow ? null : tvShow ?? CreateTvShow()),
            seasonRepository,
            provider,
            new StubExternalIdResolver(),
            catalogSync ?? new CountingCatalogSyncStateService());

    private static TvShow CreateTvShow() =>
        new()
        {
            Id = TvShowId,
            TmdbId = 900101,
            Title = "Test Show",
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    private sealed class StubExternalIdResolver : ITvShowExternalIdResolver
    {
        public string? Resolve(int? tmdbId, int? tvdbId, string? imdbId) => "ext-tv-900101";
    }

    private sealed class StubTvShowRepository(TvShow? tvShow) : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(tvShow);

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(tvShow);

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class BlockingSeasonProvider : ITvShowDataProvider
    {
        private readonly object _sync = new();
        private int _inFlight;
        private int _peakConcurrent;
        private int _blockedWaiters;
        private TaskCompletionSource _releaseAll = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool BlockUntilReleased { get; init; }

        public HashSet<int> NullSeasonNumbers { get; init; } = [];

        public List<int> FetchedSeasonNumbers { get; } = [];

        public int PeakConcurrent
        {
            get
            {
                lock (_sync)
                {
                    return _peakConcurrent;
                }
            }
        }

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
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalTvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                FetchedSeasonNumbers.Add(seasonNumber);
                _inFlight++;
                _peakConcurrent = Math.Max(_peakConcurrent, _inFlight);
                if (BlockUntilReleased)
                {
                    _blockedWaiters++;
                }
            }

            if (BlockUntilReleased)
            {
                await _releaseAll.Task.WaitAsync(cancellationToken);
            }

            lock (_sync)
            {
                _inFlight--;
            }

            return NullSeasonNumbers.Contains(seasonNumber)
                ? null
                : CreateSeasonDetails(externalTvShowId, seasonNumber);
        }

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalTvShowId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void ReleaseAllBlocked() => _releaseAll.TrySetResult();

        public async Task WaitUntilBlockedAsync(TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            while (!cts.IsCancellationRequested)
            {
                lock (_sync)
                {
                    if (_blockedWaiters > 0)
                    {
                        return;
                    }
                }

                await Task.Delay(10, cts.Token);
            }

            throw new TimeoutException("Provider calls did not block in time.");
        }

        public async Task WaitUntilPeakConcurrentAtLeastAsync(int minimumPeak, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            while (!cts.IsCancellationRequested)
            {
                if (PeakConcurrent >= minimumPeak)
                {
                    return;
                }

                await Task.Delay(10, cts.Token);
            }

            throw new TimeoutException(
                $"Peak concurrent {PeakConcurrent} did not reach {minimumPeak} in time.");
        }

        private static SeasonProviderDetails CreateSeasonDetails(string externalTvShowId, int seasonNumber) =>
            new(
                externalTvShowId,
                900101,
                null,
                seasonNumber,
                $"Season {seasonNumber}",
                null,
                new DateOnly(2026, 1, 1),
                1,
                null,
                [new EpisodeProviderDetails(
                    externalTvShowId,
                    seasonNumber,
                    null,
                    null,
                    1,
                    1,
                    "Episode 1",
                    null,
                    new DateOnly(2026, 1, 1),
                    null,
                    null,
                    0m,
                    0)]);
    }

    private sealed class RecordingSeasonRepository : ISeasonRepository
    {
        private int _inUpsert;

        public int UpsertBatchCalls { get; private set; }

        public List<int> LastSeasonOrder { get; private set; } = [];

        public bool ConcurrentUpsertDetected { get; private set; }

        public Task<Season?> GetByTvShowIdAndSeasonNumberAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Season?>(null);

        public Task<Season> UpsertFromProviderAsync(
            Guid tvShowId,
            SeasonProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task UpsertSeasonsFromProviderAsync(
            Guid tvShowId,
            IReadOnlyList<SeasonProviderDetails> details,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref _inUpsert, 1, 0) != 0)
            {
                ConcurrentUpsertDetected = true;
                throw new InvalidOperationException("Concurrent upsert detected.");
            }

            try
            {
                UpsertBatchCalls++;
                LastSeasonOrder = details.Select(d => d.SeasonNumber).ToList();
                await Task.Yield();
            }
            finally
            {
                _inUpsert = 0;
            }
        }

        public Task<Season> UpsertSummaryFromProviderAsync(
            Guid tvShowId,
            SeasonProviderSummary summary,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlySet<int>> GetRegularSeasonNumbersWithEpisodesAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());

        public Task<bool> IsRegularEpisodeIngestionRequiredAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<RegularEpisodeIngestionCheckResult> CheckRegularEpisodeIngestionRequiredAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CountingCatalogSyncStateService : ITvShowCatalogSyncStateService
    {
        public int MarkRefreshedCalls { get; private set; }

        public Task MarkRefreshedAsync(
            Guid tvShowId,
            TvShowCatalogRefreshReason reason,
            DateTime refreshedAtUtc,
            CancellationToken cancellationToken = default)
        {
            MarkRefreshedCalls++;
            return Task.CompletedTask;
        }

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

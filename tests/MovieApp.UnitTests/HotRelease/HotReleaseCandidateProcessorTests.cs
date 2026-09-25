using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.ReleaseDetection;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Models.HotRelease;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Application.Services.HotRelease;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Notifications;

namespace MovieApp.UnitTests.HotRelease;

public sealed class HotReleaseCandidateProcessorTests
{
    private static readonly Guid TvShowId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateOnly Boundary = new(2026, 9, 15);
    private static readonly DateOnly BeforeBoundary = new(2026, 9, 14);
    private static readonly DateOnly AfterBoundary = new(2026, 9, 16);
    private static readonly HotReleaseCandidate Candidate = new(TvShowId, 900101, null, null);

    [Fact]
    public async Task ProcessAsync_FutureEpisodeBeforeBoundary_CreatesNoEvents()
    {
        var detector = new RecordingReleaseDetector();
        var processor = CreateProcessor(
            detector,
            [CreateSeason(1, episodes: CreateEpisode(5, Boundary))]);

        var result = await processor.ProcessAsync(Candidate, BeforeBoundary);

        Assert.Equal(0, result.ReleaseEventsCreated);
        Assert.False(result.Hydrated);
        Assert.Equal(ReleaseDetectionMode.BoundaryCheck, Assert.Single(detector.Modes));
    }

    [Fact]
    public async Task ProcessAsync_FutureEpisodeOnBoundary_CreatesOneEvent()
    {
        var detector = new RecordingReleaseDetector { EventsCreated = 2 };
        var processor = CreateProcessor(
            detector,
            [CreateSeason(1, episodes: CreateEpisode(5, Boundary))]);

        var result = await processor.ProcessAsync(Candidate, Boundary);

        Assert.Equal(2, result.ReleaseEventsCreated);
        Assert.False(result.Hydrated);
    }

    [Fact]
    public async Task ProcessAsync_RepeatedNextDayCheck_DoesNotDuplicateEvents()
    {
        var detector = new RecordingReleaseDetector();
        detector.EventsCreated = 2;
        var processor = CreateProcessor(
            detector,
            [CreateSeason(1, episodes: CreateEpisode(5, Boundary))]);

        await processor.ProcessAsync(Candidate, Boundary);
        detector.EventsCreated = 0;
        var second = await processor.ProcessAsync(Candidate, AfterBoundary);

        Assert.Equal(0, second.ReleaseEventsCreated);
    }

    [Fact]
    public async Task ProcessAsync_PersistedEpisodeSufficient_DoesNotCallProvider()
    {
        var provider = new RecordingTvShowDataProvider();
        var processor = CreateProcessor(
            new RecordingReleaseDetector(),
            [CreateSeason(1, episodes: CreateEpisode(5, Boundary))],
            provider);

        await processor.ProcessAsync(Candidate, Boundary);

        Assert.Equal(0, provider.GetSeasonCallCount);
    }

    [Fact]
    public async Task ProcessAsync_PartialReleaseRelevantSeason_HydratesSeason()
    {
        var provider = new RecordingTvShowDataProvider();
        var processor = CreateProcessor(
            new RecordingReleaseDetector(),
            [CreateSeason(1, airDate: new DateOnly(2026, 8, 1), episodeCount: 10, episodes: CreateEpisode(1, new DateOnly(2026, 8, 1)))],
            provider);

        var result = await processor.ProcessAsync(Candidate, Boundary);

        Assert.True(result.Hydrated);
        Assert.Equal(1, provider.GetSeasonCallCount);
    }

    [Fact]
    public async Task ProcessAsync_FutureSeasonOutsideHotWindow_DoesNotHydrate()
    {
        var provider = new RecordingTvShowDataProvider();
        var processor = CreateProcessor(
            new RecordingReleaseDetector(),
            [CreateSeason(3, airDate: new DateOnly(2026, 12, 1), episodeCount: 8)],
            provider);

        await processor.ProcessAsync(Candidate, Boundary);

        Assert.Equal(0, provider.GetSeasonCallCount);
    }

    [Fact]
    public async Task ProcessAsync_SeasonZero_IsIgnoredForHydration()
    {
        var provider = new RecordingTvShowDataProvider();
        var processor = CreateProcessor(
            new RecordingReleaseDetector(),
            [CreateSeason(0, airDate: Boundary, episodeCount: 3, episodes: CreateEpisode(1, Boundary))],
            provider);

        await processor.ProcessAsync(Candidate, Boundary);

        Assert.Equal(0, provider.GetSeasonCallCount);
    }

    [Fact]
    public async Task ProcessAsync_ProviderHydration_UsesHotReleaseRefreshReason()
    {
        var syncState = new RecordingCatalogSyncStateService();
        var processor = CreateProcessor(
            new RecordingReleaseDetector(),
            [CreateSeason(1, airDate: new DateOnly(2026, 8, 1), episodeCount: 5, episodes: CreateEpisode(1, new DateOnly(2026, 8, 1)))],
            new RecordingTvShowDataProvider(),
            syncState);

        await processor.ProcessAsync(Candidate, Boundary);

        Assert.Equal(TvShowCatalogRefreshReason.HotRelease, Assert.Single(syncState.HotReleaseReasons));
        Assert.Single(syncState.HotReleaseRefreshedAtUtc);
    }

    [Fact]
    public async Task ProcessAsync_NoProviderHydration_DoesNotFakeLastRefreshedAtUtc()
    {
        var syncState = new RecordingCatalogSyncStateService();
        var processor = CreateProcessor(
            new RecordingReleaseDetector(),
            [CreateSeason(1, episodes: CreateEpisode(5, Boundary))],
            syncState: syncState);

        await processor.ProcessAsync(Candidate, Boundary);

        Assert.Empty(syncState.HotReleaseReasons);
        Assert.Empty(syncState.HotReleaseRefreshedAtUtc);
        Assert.Single(syncState.NextHotChecks);
        Assert.Null(syncState.NextHotChecks[0]);
    }

    [Fact]
    public async Task ProcessAsync_UpdatesNextHotCheckFromFutureRelease()
    {
        var syncState = new RecordingCatalogSyncStateService();
        var processor = CreateProcessor(
            new RecordingReleaseDetector(),
            [CreateSeason(1, episodes:
            [
                CreateEpisode(5, Boundary),
                CreateEpisode(6, new DateOnly(2026, 9, 18))
            ])],
            syncState: syncState);

        await processor.ProcessAsync(Candidate, Boundary);

        Assert.Equal(
            ReleaseDateTime.ToReleaseAtUtc(new DateOnly(2026, 9, 18)),
            Assert.Single(syncState.NextHotChecks));
    }

    private static HotReleaseCandidateProcessor CreateProcessor(
        RecordingReleaseDetector detector,
        IReadOnlyList<Season> seasons,
        RecordingTvShowDataProvider? provider = null,
        RecordingCatalogSyncStateService? syncState = null) =>
        new(
            new FakeCatalogRepository(seasons),
            new FakeSeasonRepository(),
            provider ?? new RecordingTvShowDataProvider(),
            new FakeExternalIdResolver(),
            detector,
            syncState ?? new RecordingCatalogSyncStateService());

    private static Season CreateSeason(
        int seasonNumber,
        DateOnly? airDate = null,
        int? episodeCount = null,
        params Episode[] episodes)
    {
        return new Season
        {
            Id = Guid.NewGuid(),
            TvShowId = TvShowId,
            SeasonNumber = seasonNumber,
            AirDate = airDate,
            EpisodeCount = episodeCount,
            Episodes = episodes.ToList()
        };
    }

    private static Episode CreateEpisode(int episodeNumber, DateOnly? airDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            SeasonId = Guid.NewGuid(),
            EpisodeNumber = episodeNumber,
            AirDate = airDate
        };

    private sealed class FakeCatalogRepository(IReadOnlyList<Season> seasons) : IReleaseDetectionCatalogRepository
    {
        public Task<IReadOnlyList<Season>> GetSeasonsWithEpisodesAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(seasons);
    }

    private sealed class FakeSeasonRepository : ISeasonRepository
    {
        public Task<Season?> GetByTvShowIdAndSeasonNumberAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Season?>(null);

        public Task<Season> UpsertFromProviderAsync(
            Guid tvShowId,
            SeasonProviderDetails details,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Season { TvShowId = tvShowId, SeasonNumber = details.SeasonNumber });

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
            Task.FromResult(new RegularEpisodeIngestionCheckResult(false, 0, 0, 0, 0, 0, []));
    }

    private sealed class FakeExternalIdResolver : ITvShowExternalIdResolver
    {
        public string? Resolve(int? tmdbId, int? tvdbId, string? imdbId) => "fake-tv-900101";
    }

    private sealed class RecordingReleaseDetector : IReleaseDetector
    {
        public int EventsCreated { get; set; }

        public List<ReleaseDetectionMode> Modes { get; } = [];

        public Task<ReleaseDetectionResult> ScanTvShowAsync(
            Guid tvShowId,
            ReleaseDetectionMode mode,
            DateOnly boundary,
            IReadOnlyList<Season>? seasons = null,
            CancellationToken cancellationToken = default)
        {
            Modes.Add(mode);
            return Task.FromResult(new ReleaseDetectionResult(
                EventsCreated,
                0,
                EventsCreated > 0 ? 1 : 0,
                EventsCreated > 1 ? 1 : 0,
                []));
        }
    }

    private sealed class RecordingTvShowDataProvider : ITvShowDataProvider
    {
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

        public Task<TvShowProviderDetails?> GetTvShowAsync(string externalId, bool includeKeywords = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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
                $"Season {seasonNumber}",
                null,
                new DateOnly(2026, 8, 1),
                10,
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

    private sealed class RecordingCatalogSyncStateService : ITvShowCatalogSyncStateService
    {
        public List<TvShowCatalogRefreshReason> HotReleaseReasons { get; } = [];

        public List<DateTime> HotReleaseRefreshedAtUtc { get; } = [];

        public List<DateTime?> NextHotChecks { get; } = [];

        public Task MarkRefreshedAsync(
            Guid tvShowId,
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
            CancellationToken cancellationToken = default)
        {
            HotReleaseReasons.Add(TvShowCatalogRefreshReason.HotRelease);
            HotReleaseRefreshedAtUtc.Add(refreshedAtUtc);
            NextHotChecks.Add(nextHotCheckAtUtc);
            return Task.CompletedTask;
        }

        public Task UpdateNextHotCheckAsync(
            Guid tvShowId,
            DateTime? nextHotCheckAtUtc,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            NextHotChecks.Add(nextHotCheckAtUtc);
            return Task.CompletedTask;
        }
    }
}

using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.ReleaseDetection;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Services.TvShowFollows;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.TvShowFollows;

public sealed class TvShowFollowBaselineServiceTests
{
    private static readonly Guid TvShowId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime NotifyFromUtc = new(2026, 9, 14, 21, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly BoundaryDate = DateOnly.FromDateTime(NotifyFromUtc);

    [Fact]
    public async Task EstablishAsync_FullyHydratedShow_DoesNotCallProviderAndEstablishesBaseline()
    {
        var follow = CreateFollow();
        var repository = new FakeTvShowFollowRepository(follow);
        var tvShowRepository = new FakeTvShowRepository(CreateFullyHydratedTvShow());
        var seasonHydrator = new FakeSeasonSummaryHydrator();
        var catalogRepository = new FakeReleaseDetectionCatalogRepository(CreateFullyHydratedTvShow().Seasons.ToList());
        var seasonRepository = new FakeSeasonRepository();
        var provider = new FakeTvShowDataProvider();
        var releaseDetector = new FakeReleaseDetector();

        var service = CreateService(
            repository,
            tvShowRepository,
            seasonHydrator,
            catalogRepository,
            seasonRepository,
            provider,
            releaseDetector);

        await service.EstablishAsync(follow);

        Assert.True(follow.IsBaselineEstablished);
        Assert.Equal(0, provider.GetTvShowCallCount);
        Assert.Equal(0, provider.GetSeasonCallCount);
        Assert.Equal(0, seasonHydrator.CallCount);
        Assert.Equal(1, releaseDetector.ScanCallCount);
        Assert.Equal(ReleaseDetectionMode.BaselineAbsorb, releaseDetector.LastMode);
        Assert.Equal(BoundaryDate, releaseDetector.LastBoundary);
    }

    [Fact]
    public async Task EstablishAsync_MissingSummaries_InvokesSummaryHydratorOnce()
    {
        var follow = CreateFollow();
        var tvShow = CreateTvShowWithoutSeasons();
        var repository = new FakeTvShowFollowRepository(follow);
        var tvShowRepository = new FakeTvShowRepository(tvShow, CreateTvShowWithSummariesOnly());
        var seasonHydrator = new FakeSeasonSummaryHydrator();
        var catalogRepository = new FakeReleaseDetectionCatalogRepository(CreateTvShowWithSummariesOnly().Seasons.ToList());
        var seasonRepository = new FakeSeasonRepository();
        var provider = new FakeTvShowDataProvider();
        var releaseDetector = new FakeReleaseDetector();

        var service = CreateService(
            repository,
            tvShowRepository,
            seasonHydrator,
            catalogRepository,
            seasonRepository,
            provider,
            releaseDetector);

        await service.EstablishAsync(follow);

        Assert.Equal(1, seasonHydrator.CallCount);
        Assert.Equal(1, provider.GetSeasonCallCount);
        Assert.True(follow.IsBaselineEstablished);
    }

    [Fact]
    public async Task EstablishAsync_PartialHistoricalSeason_HydratesSeason()
    {
        var follow = CreateFollow();
        var partialSeason = CreateSeasonSummary(1, new DateOnly(2026, 8, 1), 10, CreateEpisode(1, new DateOnly(2026, 8, 1)));
        var repository = new FakeTvShowFollowRepository(follow);
        var tvShowRepository = new FakeTvShowRepository(CreateTvShow([partialSeason]));
        var seasonHydrator = new FakeSeasonSummaryHydrator();
        var catalogRepository = new FakeReleaseDetectionCatalogRepository([partialSeason]);
        var seasonRepository = new FakeSeasonRepository();
        var provider = new FakeTvShowDataProvider();
        var releaseDetector = new FakeReleaseDetector();

        var service = CreateService(
            repository,
            tvShowRepository,
            seasonHydrator,
            catalogRepository,
            seasonRepository,
            provider,
            releaseDetector);

        await service.EstablishAsync(follow);

        Assert.Equal(1, provider.GetSeasonCallCount);
        Assert.Equal(1, seasonRepository.UpsertCallCount);
        Assert.True(follow.IsBaselineEstablished);
    }

    [Fact]
    public async Task EstablishAsync_FutureSeason_IsNotHydrated()
    {
        var follow = CreateFollow();
        var futureSeason = CreateSeasonSummary(2, new DateOnly(2026, 10, 1), 10);
        var repository = new FakeTvShowFollowRepository(follow);
        var tvShowRepository = new FakeTvShowRepository(CreateTvShow([futureSeason]));
        var seasonHydrator = new FakeSeasonSummaryHydrator();
        var catalogRepository = new FakeReleaseDetectionCatalogRepository([futureSeason]);
        var seasonRepository = new FakeSeasonRepository();
        var provider = new FakeTvShowDataProvider();
        var releaseDetector = new FakeReleaseDetector();

        var service = CreateService(
            repository,
            tvShowRepository,
            seasonHydrator,
            catalogRepository,
            seasonRepository,
            provider,
            releaseDetector);

        await service.EstablishAsync(follow);

        Assert.Equal(0, provider.GetSeasonCallCount);
        Assert.True(follow.IsBaselineEstablished);
    }

    [Fact]
    public async Task EstablishAsync_ProviderFailure_LeavesBaselineUnestablished()
    {
        var follow = CreateFollow();
        var partialSeason = CreateSeasonSummary(1, new DateOnly(2026, 8, 1), 10);
        var repository = new FakeTvShowFollowRepository(follow);
        var tvShowRepository = new FakeTvShowRepository(CreateTvShow([partialSeason]));
        var seasonHydrator = new FakeSeasonSummaryHydrator();
        var catalogRepository = new FakeReleaseDetectionCatalogRepository([partialSeason]);
        var seasonRepository = new FakeSeasonRepository();
        var provider = new FakeTvShowDataProvider { FailSeason = true };
        var releaseDetector = new FakeReleaseDetector();

        var service = CreateService(
            repository,
            tvShowRepository,
            seasonHydrator,
            catalogRepository,
            seasonRepository,
            provider,
            releaseDetector);

        await Assert.ThrowsAsync<TvShowFollowBaselineException>(() => service.EstablishAsync(follow));

        Assert.False(follow.IsBaselineEstablished);
        Assert.Equal(0, releaseDetector.ScanCallCount);
    }

    [Fact]
    public async Task EstablishAsync_NoRegularSeasons_EstablishesBaseline()
    {
        var follow = CreateFollow();
        var repository = new FakeTvShowFollowRepository(follow);
        var tvShowRepository = new FakeTvShowRepository(CreateTvShow([CreateSeasonSummary(0, new DateOnly(2026, 8, 1), 5)]));
        var seasonHydrator = new FakeSeasonSummaryHydrator();
        var catalogRepository = new FakeReleaseDetectionCatalogRepository([CreateSeasonSummary(0, new DateOnly(2026, 8, 1), 5)]);
        var seasonRepository = new FakeSeasonRepository();
        var provider = new FakeTvShowDataProvider();
        var releaseDetector = new FakeReleaseDetector();

        var service = CreateService(
            repository,
            tvShowRepository,
            seasonHydrator,
            catalogRepository,
            seasonRepository,
            provider,
            releaseDetector);

        await service.EstablishAsync(follow);

        Assert.True(follow.IsBaselineEstablished);
        Assert.Equal(0, provider.GetSeasonCallCount);
    }

    private static TvShowFollowBaselineService CreateService(
        FakeTvShowFollowRepository followRepository,
        FakeTvShowRepository tvShowRepository,
        FakeSeasonSummaryHydrator seasonHydrator,
        FakeReleaseDetectionCatalogRepository catalogRepository,
        FakeSeasonRepository seasonRepository,
        FakeTvShowDataProvider provider,
        FakeReleaseDetector releaseDetector) =>
        new(
            followRepository,
            tvShowRepository,
            seasonHydrator,
            catalogRepository,
            seasonRepository,
            provider,
            new FakeExternalIdResolver(),
            new FakeCatalogSyncStateService(),
            releaseDetector);

    private static CatalogFollow CreateFollow()
    {
        var follow = CatalogFollow.CreateTvFollow(UserId, TvShowId, true, true, NotifyFromUtc);
        follow.SetNotifyFromUtc(NotifyFromUtc, NotifyFromUtc);
        return follow;
    }

    private static TvShow CreateTvShowWithoutSeasons() =>
        new()
        {
            Id = TvShowId,
            TmdbId = 900101,
            Title = "Test Show",
            Seasons = [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static TvShow CreateTvShowWithSummariesOnly() =>
        CreateTvShow(
        [
            CreateSeasonSummary(1, new DateOnly(2026, 8, 1), 3),
            CreateSeasonSummary(2, new DateOnly(2026, 10, 1), 2)
        ]);

    private static TvShow CreateFullyHydratedTvShow() =>
        CreateTvShow(
        [
            CreateSeasonSummary(
                1,
                new DateOnly(2026, 8, 1),
                2,
                CreateEpisode(1, new DateOnly(2026, 8, 1)),
                CreateEpisode(2, new DateOnly(2026, 8, 8))),
            CreateSeasonSummary(
                2,
                new DateOnly(2026, 9, 1),
                1,
                CreateEpisode(1, new DateOnly(2026, 9, 1)))
        ]);

    private static TvShow CreateTvShow(IReadOnlyList<Season> seasons) =>
        new()
        {
            Id = TvShowId,
            TmdbId = 900101,
            Title = "Test Show",
            Seasons = seasons.ToList(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static Season CreateSeasonSummary(
        int seasonNumber,
        DateOnly? airDate,
        int episodeCount,
        params Episode[] episodes) =>
        new()
        {
            Id = Guid.NewGuid(),
            TvShowId = TvShowId,
            SeasonNumber = seasonNumber,
            AirDate = airDate,
            EpisodeCount = episodeCount,
            Episodes = episodes.ToList(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static Episode CreateEpisode(int episodeNumber, DateOnly airDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            EpisodeNumber = episodeNumber,
            AirDate = airDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private sealed class FakeTvShowFollowRepository(CatalogFollow follow) : ITvShowFollowRepository
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CatalogFollow?> GetForUserAndTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CatalogFollow?>(follow);

        public Task<CatalogFollow?> GetForUserAndTvShowForUpdateAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CatalogFollow?>(follow);

        public Task<bool> TryAddAsync(CatalogFollow follow, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> RemoveForTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(IReadOnlyList<CatalogFollow> Follows, int TotalCount)> GetUserFollowsAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<CatalogFollow>, int)>(([], 0));
    }

    private sealed class FakeTvShowRepository : ITvShowRepository
    {
        private readonly TvShow _initial;
        private readonly TvShow? _afterSummaryHydration;
        private int _getByIdCallCount;

        public FakeTvShowRepository(TvShow initial, TvShow? afterSummaryHydration = null)
        {
            _initial = initial;
            _afterSummaryHydration = afterSummaryHydration;
        }

        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _getByIdCallCount++;
            if (_afterSummaryHydration is not null && _getByIdCallCount > 1)
            {
                return Task.FromResult<TvShow?>(_afterSummaryHydration);
            }

            return Task.FromResult<TvShow?>(_initial);
        }

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<TvShow> UpsertFromProviderAsync(TvShowProviderDetails details, CancellationToken cancellationToken = default) =>
            Task.FromResult(_afterSummaryHydration ?? _initial);
    }

    private sealed class FakeSeasonSummaryHydrator : ITvShowSeasonSummaryHydrator
    {
        public int CallCount { get; private set; }

        public Task<TvShowSeasonSummaryHydrationResult> EnsureSeasonSummariesAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new TvShowSeasonSummaryHydrationResult(
                new TvShow
                {
                    Id = tvShowId,
                    TmdbId = 900101,
                    Title = "Hydrated",
                    Seasons = [],
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                ProviderCatalogRefreshed: true));
        }
    }

    private sealed class FakeCatalogSyncStateService : ITvShowCatalogSyncStateService
    {
        public List<(Guid TvShowId, TvShowCatalogRefreshReason Reason, DateTime RefreshedAtUtc)> Calls { get; } = [];

        public Task MarkRefreshedAsync(
            Guid tvShowId,
            TvShowCatalogRefreshReason reason,
            DateTime refreshedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((tvShowId, reason, refreshedAtUtc));
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

    private sealed class FakeReleaseDetectionCatalogRepository(IReadOnlyList<Season> seasons)
        : IReleaseDetectionCatalogRepository
    {
        public Task<IReadOnlyList<Season>> GetSeasonsWithEpisodesAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(seasons);
    }

    private sealed class FakeSeasonRepository : ISeasonRepository
    {
        public int UpsertCallCount { get; private set; }

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
            UpsertCallCount++;
            return Task.FromResult(new Season { SeasonNumber = details.SeasonNumber });
        }

        public Task<Season> UpsertSummaryFromProviderAsync(
            Guid tvShowId,
            SeasonProviderSummary summary,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Season { SeasonNumber = summary.SeasonNumber });
    }

    private sealed class FakeTvShowDataProvider : ITvShowDataProvider
    {
        public bool FailSeason { get; init; }

        public int GetTvShowCallCount { get; private set; }

        public int GetSeasonCallCount { get; private set; }

        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderDetails?> GetTvShowAsync(string externalId, CancellationToken cancellationToken = default)
        {
            GetTvShowCallCount++;
            return Task.FromResult<TvShowProviderDetails?>(null);
        }

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalTvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default)
        {
            GetSeasonCallCount++;
            return Task.FromResult<SeasonProviderDetails?>(
                FailSeason
                    ? null
                    : new SeasonProviderDetails(
                        externalTvShowId,
                        1,
                        null,
                        seasonNumber,
                        $"Season {seasonNumber}",
                        null,
                        new DateOnly(2026, 8, 1),
                        3,
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

    private sealed class FakeExternalIdResolver : ITvShowExternalIdResolver
    {
        public string? Resolve(int? tmdbId, int? tvdbId, string? imdbId) => "fake-tv-900101";
    }

    private sealed class FakeReleaseDetector : IReleaseDetector
    {
        public int ScanCallCount { get; private set; }

        public ReleaseDetectionMode? LastMode { get; private set; }

        public DateOnly? LastBoundary { get; private set; }

        public Task<ReleaseDetectionResult> ScanTvShowAsync(
            Guid tvShowId,
            ReleaseDetectionMode mode,
            DateOnly boundary,
            CancellationToken cancellationToken = default)
        {
            ScanCallCount++;
            LastMode = mode;
            LastBoundary = boundary;
            return Task.FromResult(ReleaseDetectionResult.Empty);
        }
    }
}

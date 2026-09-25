using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.ReleaseDetection;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.TvShowFollows;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.TvShows;

public sealed class TvShowCatalogHydrationSyncStateTests
{
    private static readonly Guid TvShowId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTime NotifyFromUtc = new(2026, 9, 14, 21, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetSeasonService_DbOnlyRead_DoesNotUpdateSyncState()
    {
        var syncState = new TrackingCatalogSyncStateService();
        var season = CreateSeasonWithEpisodes();
        var service = new GetSeasonService(
            new FakeTvShowRepository(CreateTvShow()),
            new FakeSeasonRepository(season),
            new FakeTvShowDataProvider(),
            new FakeExternalIdResolver(),
            syncState,
            new NoOpCacheService());

        await service.GetSeasonAsync(TvShowId, 1);

        Assert.Empty(syncState.Calls);
    }

    [Fact]
    public async Task GetSeasonService_ProviderBackedHydration_WritesDetailHydration()
    {
        var syncState = new TrackingCatalogSyncStateService();
        var service = new GetSeasonService(
            new FakeTvShowRepository(CreateTvShow()),
            new FakeSeasonRepository(null),
            new FakeTvShowDataProvider(),
            new FakeExternalIdResolver(),
            syncState,
            new NoOpCacheService());

        await service.GetSeasonAsync(TvShowId, 1);

        Assert.Single(syncState.Calls);
        Assert.Equal(TvShowId, syncState.Calls[0].TvShowId);
        Assert.Equal(TvShowCatalogRefreshReason.DetailHydration, syncState.Calls[0].Reason);
    }

    [Fact]
    public async Task GetTvShowByIdService_ProviderBackedSummaryHydration_WritesDetailHydration()
    {
        var syncState = new TrackingCatalogSyncStateService();
        var service = new GetTvShowByIdService(
            new FakeSeasonSummaryHydrator(providerCatalogRefreshed: true),
            syncState,
            new NoOpCacheService());

        await service.GetByIdAsync(TvShowId);

        Assert.Single(syncState.Calls);
        Assert.Equal(TvShowCatalogRefreshReason.DetailHydration, syncState.Calls[0].Reason);
    }

    [Fact]
    public async Task GetTvShowByIdService_DbOnlyRead_DoesNotUpdateSyncState()
    {
        var syncState = new TrackingCatalogSyncStateService();
        var cachedSeason = new SeasonSummaryResult(
            Guid.NewGuid(),
            1,
            "Season 1",
            new DateOnly(2008, 1, 20),
            3,
            null);
        var cachedResult = new TvShowDetailsResult(
            TvShowId,
            900101,
            null,
            null,
            "Breaking Bad",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0m,
            0,
            "Ended",
            [],
            [cachedSeason],
            CanFollow: false);

        var cache = new NoOpCacheService();
        await cache.SetAsync(
            TvShowDetailsCacheKeys.Create(TvShowId),
            new TvShowDetailsCacheEntry { Result = cachedResult },
            TimeSpan.FromMinutes(15));

        var service = new GetTvShowByIdService(
            new FakeSeasonSummaryHydrator(providerCatalogRefreshed: true),
            syncState,
            cache);

        await service.GetByIdAsync(TvShowId);

        Assert.Empty(syncState.Calls);
    }

    [Fact]
    public async Task FollowBaseline_ZeroProviderCalls_DoesNotFakeRefresh()
    {
        var syncState = new TrackingCatalogSyncStateService();
        var follow = CatalogFollow.CreateTvFollow(Guid.NewGuid(), TvShowId, true, true, NotifyFromUtc);
        follow.SetNotifyFromUtc(NotifyFromUtc, NotifyFromUtc);

        var service = new TvShowFollowBaselineService(
            new FakeTvShowFollowRepository(follow),
            new FakeTvShowRepository(CreateFullyHydratedTvShow()),
            new FakeSeasonSummaryHydrator(providerCatalogRefreshed: false),
            new FakeReleaseDetectionCatalogRepository(CreateFullyHydratedTvShow().Seasons.ToList()),
            CreateScopeFactory(new FakeSeasonRepository(null)),
            new FakeTvShowDataProvider(),
            new FakeExternalIdResolver(),
            syncState,
            new FakeReleaseDetector(),
            NullLogger<TvShowFollowBaselineService>.Instance);

        await service.EstablishAsync(follow);

        Assert.Empty(syncState.Calls);
    }

    [Fact]
    public async Task FollowBaseline_WithProviderCalls_WritesFollowBaseline()
    {
        var syncState = new TrackingCatalogSyncStateService();
        var follow = CatalogFollow.CreateTvFollow(Guid.NewGuid(), TvShowId, true, true, NotifyFromUtc);
        follow.SetNotifyFromUtc(NotifyFromUtc, NotifyFromUtc);
        var partialSeason = CreateSeasonSummary(1, new DateOnly(2026, 8, 1), 10);

        var service = new TvShowFollowBaselineService(
            new FakeTvShowFollowRepository(follow),
            new FakeTvShowRepository(CreateTvShow([partialSeason])),
            new FakeSeasonSummaryHydrator(providerCatalogRefreshed: false),
            new FakeReleaseDetectionCatalogRepository([partialSeason]),
            CreateScopeFactory(new FakeSeasonRepository(null)),
            new FakeTvShowDataProvider(),
            new FakeExternalIdResolver(),
            syncState,
            new FakeReleaseDetector(),
            NullLogger<TvShowFollowBaselineService>.Instance);

        await service.EstablishAsync(follow);

        Assert.Single(syncState.Calls);
        Assert.Equal(TvShowCatalogRefreshReason.FollowBaseline, syncState.Calls[0].Reason);
    }

    [Fact]
    public async Task GetSeasonService_ProviderFailure_DoesNotUpdateSyncState()
    {
        var syncState = new TrackingCatalogSyncStateService();
        var service = new GetSeasonService(
            new FakeTvShowRepository(CreateTvShow()),
            new FakeSeasonRepository(null),
            new FakeTvShowDataProvider { ReturnNullSeason = true },
            new FakeExternalIdResolver(),
            syncState,
            new NoOpCacheService());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetSeasonAsync(TvShowId, 1));

        Assert.Empty(syncState.Calls);
    }

    [Fact]
    public async Task GetSeasonService_CatalogUpsertFailure_DoesNotUpdateSyncState()
    {
        var syncState = new TrackingCatalogSyncStateService();
        var service = new GetSeasonService(
            new FakeTvShowRepository(CreateTvShow()),
            new FakeSeasonRepository(null) { ThrowOnUpsert = true },
            new FakeTvShowDataProvider(),
            new FakeExternalIdResolver(),
            syncState,
            new NoOpCacheService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetSeasonAsync(TvShowId, 1));

        Assert.Empty(syncState.Calls);
    }

    private static TvShow CreateTvShow(IReadOnlyList<Season>? seasons = null) =>
        new()
        {
            Id = TvShowId,
            TmdbId = 900101,
            Title = "Test Show",
            Seasons = seasons?.ToList() ?? [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static TvShow CreateFullyHydratedTvShow()
    {
        var season = CreateSeasonWithEpisodes();
        return CreateTvShow([season]);
    }

    private static Season CreateSeasonWithEpisodes()
    {
        var seasonId = Guid.NewGuid();
        return new Season
        {
            Id = seasonId,
            TvShowId = TvShowId,
            SeasonNumber = 1,
            EpisodeCount = 1,
            Episodes =
            [
                new Episode
                {
                    Id = Guid.NewGuid(),
                    SeasonId = seasonId,
                    EpisodeNumber = 1,
                    AirDate = new DateOnly(2026, 8, 1),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            ],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static Season CreateSeasonSummary(int seasonNumber, DateOnly airDate, int episodeCount) =>
        new()
        {
            Id = Guid.NewGuid(),
            TvShowId = TvShowId,
            SeasonNumber = seasonNumber,
            AirDate = airDate,
            EpisodeCount = episodeCount,
            Episodes = [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private sealed class TrackingCatalogSyncStateService : ITvShowCatalogSyncStateService
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

    private sealed class FakeSeasonSummaryHydrator(bool providerCatalogRefreshed) : ITvShowSeasonSummaryHydrator
    {
        public Task<TvShowSeasonSummaryHydrationResult> EnsureSeasonSummariesAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TvShowSeasonSummaryHydrationResult(CreateTvShow(), providerCatalogRefreshed));
    }

    private sealed class FakeExternalIdResolver : ITvShowExternalIdResolver
    {
        public string? Resolve(int? tmdbId, int? tvdbId, string? imdbId) => "fake-tv-900101";
    }

    private sealed class FakeTvShowDataProvider : ITvShowDataProvider
    {
        public bool ReturnNullSeason { get; init; }

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

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalTvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SeasonProviderDetails?>(
                ReturnNullSeason
                    ? null
                    : new SeasonProviderDetails(
                        externalTvShowId,
                        1,
                        null,
                        seasonNumber,
                        $"Season {seasonNumber}",
                        null,
                        new DateOnly(2026, 8, 1),
                        1,
                        null,
                        [new EpisodeProviderDetails(
                            "fake-tv-900101",
                            1,
                            null,
                            null,
                            1,
                            1,
                            "Pilot",
                            null,
                            new DateOnly(2026, 8, 1),
                            null,
                            null,
                            0m,
                            0)]));

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalTvShowId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTvShowRepository(TvShow tvShow) : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(tvShow);

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

    private sealed class FakeSeasonRepository(Season? season) : ISeasonRepository
    {
        public bool ThrowOnUpsert { get; init; }

        public Task<Season?> GetByTvShowIdAndSeasonNumberAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(season);

        public Task<Season> UpsertFromProviderAsync(
            Guid tvShowId,
            SeasonProviderDetails details,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnUpsert)
            {
                throw new InvalidOperationException("Upsert failed.");
            }

            return Task.FromResult(CreateSeasonWithEpisodes());
        }

        public async Task<SeasonBatchUpsertPersistenceMetrics> UpsertSeasonsFromProviderAsync(
            Guid tvShowId,
            IReadOnlyList<SeasonProviderDetails> details,
            CancellationToken cancellationToken = default)
        {
            foreach (var detail in details)
            {
                await UpsertFromProviderAsync(tvShowId, detail, cancellationToken);
            }

            return SeasonBatchUpsertPersistenceMetrics.Empty;
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

    private sealed class FakeTvShowFollowRepository(CatalogFollow follow) : ITvShowFollowRepository
    {
        public Task<CatalogFollow?> GetForUserAndTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CatalogFollow?>(follow);

        public Task<CatalogFollow?> GetForUserAndTvShowForUpdateAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CatalogFollow?>(follow);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

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

    private sealed class FakeReleaseDetectionCatalogRepository(IReadOnlyList<Season> seasons)
        : IReleaseDetectionCatalogRepository
    {
        public Task<IReadOnlyList<Season>> GetSeasonsWithEpisodesAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(seasons);
    }

    private sealed class FakeReleaseDetector : IReleaseDetector
    {
        public Task<ReleaseDetectionResult> ScanTvShowAsync(
            Guid tvShowId,
            ReleaseDetectionMode mode,
            DateOnly boundary,
            IReadOnlyList<Season>? seasons = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ReleaseDetectionResult.Empty);
    }

    private sealed class NoOpCacheService : ICacheService
    {
        private readonly Dictionary<string, object?> _entries = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult(_entries.TryGetValue(key, out var value) ? value as T : null);

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class
        {
            _entries[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }

    private static IServiceScopeFactory CreateScopeFactory(ISeasonRepository seasonRepository)
    {
        var services = new ServiceCollection();
        services.AddScoped<ISeasonRepository>(_ => seasonRepository);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }
}

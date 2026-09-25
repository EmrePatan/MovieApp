using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Models.Credits;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Models.WatchHistory;
using MovieApp.Application.Services.Credits;
using MovieApp.Application.Services.Movies;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Providers;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Detail;

public sealed class DetailChildEndpointPerformanceTests
{
    private static readonly Guid MovieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TvShowId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task GetMovieCreditsAsyncSkipsRepositoryWhenCacheHit()
    {
        var repository = new TrackingMovieRepository(
            new CatalogProviderLookup(FakeMovieDataProvider.InterstellarTmdbId, "en"));
        var cache = new InMemoryCacheService();
        var cachedCredits = new CreditsResult(
            global::MovieApp.Infrastructure.Providers.FakeCreditsProvider.InterstellarCast,
            global::MovieApp.Infrastructure.Providers.FakeCreditsProvider.InterstellarCrew);
        await cache.SetAsync(
            MovieCreditsCacheKeys.Create(MovieId),
            new CreditsCacheEntry { Result = cachedCredits },
            TimeSpan.FromHours(24));

        var service = new GetMovieCreditsService(
            repository,
            new ThrowingCreditsProvider(),
            cache);

        var result = await service.GetCreditsAsync(MovieId);

        Assert.Equal(cachedCredits.Cast.Count, result.Cast.Count);
        Assert.Equal(0, repository.LookupCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task GetMovieCreditsAsyncUsesProviderLookupOnCacheMiss()
    {
        var repository = new TrackingMovieRepository(
            new CatalogProviderLookup(FakeMovieDataProvider.InterstellarTmdbId, "en"));
        var service = new GetMovieCreditsService(
            repository,
            new StubCreditsProvider(),
            new InMemoryCacheService());

        await service.GetCreditsAsync(MovieId);

        Assert.Equal(1, repository.LookupCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task GetSeasonAsyncUsesExternalIdsInsteadOfTvShowGraph()
    {
        var repository = new ExternalIdOnlyTvShowRepository();
        var service = new GetSeasonService(
            repository,
            new ReadySeasonRepository(),
            new CountingTvShowDataProvider(),
            new FakeExternalIdResolver(),
            new NoOpCatalogSyncStateService(),
            new InMemoryCacheService());

        var season = await service.GetSeasonAsync(TvShowId, 1);

        Assert.Equal(0, repository.GetByIdCallCount);
        Assert.Equal(1, season.SeasonNumber);
    }

    [Fact]
    public async Task GetEpisodeAsyncReusesProviderSeasonWhenSeasonAndEpisodeAreHydratedInOneRequest()
    {
        var provider = new CountingTvShowDataProvider();
        var service = new GetEpisodeService(
            new FakeTvShowRepository(CreateTvShow()),
            new MissingThenHydratedSeasonRepository(),
            new DeferredEpisodeRepository(),
            provider,
            new FakeExternalIdResolver(),
            new NoOpCatalogSyncStateService(),
            new InMemoryCacheService());

        var result = await service.GetEpisodeAsync(TvShowId, 1, 2);

        Assert.Equal(2, result.EpisodeNumber);
        Assert.Equal(1, provider.SeasonCallCount);
    }

    private static TvShow CreateTvShow() =>
        new()
        {
            Id = TvShowId,
            TmdbId = 900101,
            Title = "Test Show",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    private sealed class TrackingMovieRepository(CatalogProviderLookup lookup) : IMovieRepository
    {
        public int LookupCallCount { get; private set; }

        public int GetByIdCallCount { get; private set; }

        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            GetByIdCallCount += 1;
            return Task.FromResult<Movie?>(null);
        }

        public Task<CatalogProviderLookup?> GetProviderLookupByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            LookupCallCount += 1;
            return Task.FromResult(id == MovieId ? lookup : null);
        }

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(MovieProviderDetails details, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingCreditsProvider : ICreditsProvider
    {
        public Task<CreditsResult> GetMovieCreditsAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new TmdbApiException(System.Net.HttpStatusCode.ServiceUnavailable, "Should not call provider.");

        public Task<CreditsResult> GetTvShowCreditsAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubCreditsProvider : ICreditsProvider
    {
        public Task<CreditsResult> GetMovieCreditsAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CreditsResult(
                global::MovieApp.Infrastructure.Providers.FakeCreditsProvider.InterstellarCast,
                global::MovieApp.Infrastructure.Providers.FakeCreditsProvider.InterstellarCrew));

        public Task<CreditsResult> GetTvShowCreditsAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CreditsResult([], []));
    }

    private sealed class ExternalIdOnlyTvShowRepository : ITvShowRepository
    {
        public int GetByIdCallCount { get; private set; }

        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            throw new InvalidOperationException("Season reads must not load the TV show graph.");
        }

        public Task<TvShowExternalIds?> GetExternalIdsByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShowExternalIds?>(new TvShowExternalIds(900101, null, null));

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ReadySeasonRepository : ISeasonRepository
    {
        public Task<Season?> GetByTvShowIdAndSeasonNumberAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Season?>(new Season
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                TvShowId = tvShowId,
                SeasonNumber = seasonNumber,
                Name = "Season 1",
                EpisodeCount = 1,
                Episodes =
                [
                    new Episode
                    {
                        Id = Guid.NewGuid(),
                        EpisodeNumber = 1,
                        Name = "Pilot",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }
                ],
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        public Task<Season> UpsertFromProviderAsync(
            Guid tvShowId,
            SeasonProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpsertSeasonsFromProviderAsync(
            Guid tvShowId,
            IReadOnlyList<SeasonProviderDetails> details,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

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
            Task.FromResult(true);
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

    private sealed class MissingThenHydratedSeasonRepository : ISeasonRepository
    {
        private static readonly Guid HydratedSeasonId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        public Task<Season?> GetByTvShowIdAndSeasonNumberAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Season?>(null);

        public Task<Season> UpsertFromProviderAsync(
            Guid tvShowId,
            SeasonProviderDetails details,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Season
            {
                Id = HydratedSeasonId,
                TvShowId = tvShowId,
                SeasonNumber = details.SeasonNumber,
                EpisodeCount = details.Episodes.Count,
                Episodes = [],
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

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
            Task.FromResult(true);
    }

    private sealed class DeferredEpisodeRepository : IEpisodeRepository
    {
        private static readonly Guid HydratedSeasonId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private int _lookupCount;

        public Task<Episode?> GetBySeasonIdAndEpisodeNumberAsync(
            Guid seasonId,
            int episodeNumber,
            CancellationToken cancellationToken = default)
        {
            _lookupCount += 1;
            if (seasonId == HydratedSeasonId && episodeNumber == 2 && _lookupCount >= 2)
            {
                return Task.FromResult<Episode?>(new Episode
                {
                    Id = Guid.NewGuid(),
                    SeasonId = seasonId,
                    EpisodeNumber = episodeNumber,
                    Name = "Second",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }

            return Task.FromResult<Episode?>(null);
        }

        public Task<Episode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CountByTvShowIdAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CountByTvShowIdAndSeasonNumberAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SeasonEpisodeCountResult>> GetEpisodeCountsBySeasonAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Episode?> GetFirstUnwatchedForTvShowAsync(
            Guid tvShowId,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Episode?> GetFirstUnwatchedForSeasonAsync(
            Guid tvShowId,
            int seasonNumber,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Episode> UpsertFromProviderAsync(
            Guid seasonId,
            EpisodeProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsBelongingToTvShowAsync(
            Guid tvShowId,
            IReadOnlyList<Guid> episodeIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsForTvShowUpToEpisodeAsync(
            Guid tvShowId,
            Guid targetEpisodeId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsForSeasonAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsForTvShowAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsForRegularSeasonsAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CountingTvShowDataProvider : ITvShowDataProvider
    {
        public int SeasonCallCount { get; private set; }

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
            CancellationToken cancellationToken = default)
        {
            SeasonCallCount += 1;
            return Task.FromResult<SeasonProviderDetails?>(CreateSeasonDetails(seasonNumber));
        }

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalTvShowId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private static SeasonProviderDetails CreateSeasonDetails(int seasonNumber) =>
            new(
                "fake-tv-900101",
                1,
                null,
                seasonNumber,
                $"Season {seasonNumber}",
                null,
                new DateOnly(2026, 8, 1),
                2,
                null,
                [
                    new EpisodeProviderDetails("fake-tv-900101", 1, null, null, 1, 1, "Pilot", null, new DateOnly(2026, 8, 1), null, null, 0m, 0),
                    new EpisodeProviderDetails("fake-tv-900101", 1, null, null, 1, 2, "Second", null, new DateOnly(2026, 8, 8), null, null, 0m, 0),
                ]);
    }

    private sealed class FakeExternalIdResolver : ITvShowExternalIdResolver
    {
        public string? Resolve(int? tmdbId, int? tvdbId, string? imdbId) => "fake-tv-900101";
    }

    private sealed class NoOpCatalogSyncStateService : ITvShowCatalogSyncStateService
    {
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
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateNextHotCheckAsync(
            Guid tvShowId,
            DateTime? nextHotCheckAtUtc,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class InMemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            if (_entries.TryGetValue(key, out var value))
            {
                return Task.FromResult((T?)value);
            }

            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
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
}

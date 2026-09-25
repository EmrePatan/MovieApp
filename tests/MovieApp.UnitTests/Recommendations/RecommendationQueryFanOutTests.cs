using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Recommendations;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Recommendations;

public sealed class RecommendationQueryFanOutTests
{
    [Fact]
    public async Task GetHomeRecommendationsForCurrentUserAsyncSkipsColdStartDiscoveryWhenDisabled()
    {
        var discovery = new CountingDiscoveryService();
        var repository = new ColdStartRecommendationRepository();
        var service = CreateService(repository, discovery);

        var sections = await service.GetHomeRecommendationsForCurrentUserAsync(
            includeColdStartDiscoverySections: false);

        Assert.Empty(sections);
        Assert.Equal(0, discovery.CallCount);
        Assert.Equal(1, repository.LastMinimumInteractionsForEnrichment);
    }

    [Fact]
    public async Task GetHomeRecommendationsForCurrentUserAsyncPassesPersonalizationThresholdToRepository()
    {
        var repository = new ColdStartRecommendationRepository();
        var service = CreateService(repository, new CountingDiscoveryService());

        await service.GetHomeRecommendationsForCurrentUserAsync(includeColdStartDiscoverySections: false);

        Assert.Equal(1, repository.GetUserContextCount);
        Assert.Equal(1, repository.LastMinimumInteractionsForEnrichment);
    }

    [Fact]
    public async Task GetHomeRecommendationsForCurrentUserAsyncLoadsColdStartDiscoveryWhenEnabled()
    {
        var discovery = new CountingDiscoveryService();
        var service = CreateService(
            new ColdStartRecommendationRepository(),
            discovery);

        var sections = await service.GetHomeRecommendationsForCurrentUserAsync(
            includeColdStartDiscoverySections: true);

        Assert.Equal(3, sections.Count);
        Assert.Equal(3, discovery.CallCount);
    }

    [Fact]
    public async Task PersonalizedHomeSectionsOverlapWhenIsolatedScopesAreConfigured()
    {
        var gate = new ParallelSectionGate();
        var repository = new CountingRecommendationRepository { SectionGate = gate };
        var service = CreateService(
            repository,
            new CountingDiscoveryService(),
            scopeFactory: new RecommendationScopeFactory(repository));

        await service.GetHomeRecommendationsForCurrentUserAsync(includeColdStartDiscoverySections: false);

        Assert.True(gate.MaxInFlight >= 2);
    }

    [Fact]
    public async Task BuildSimilarSectionUsesBatchSimilarityRepositoryCalls()
    {
        var repository = new CountingRecommendationRepository();
        var service = CreateService(repository, new CountingDiscoveryService());

        await service.GetHomeRecommendationsForCurrentUserAsync();

        Assert.Equal(1, repository.GetUserContextCount);
        Assert.Equal(1, repository.GetMovieProfilesBatchCount);
        Assert.Equal(1, repository.GetMovieCandidateIdsForSourcesCount);
        Assert.Equal(1, repository.GetMovieCandidatesByIdsCount);
        Assert.Equal(0, repository.GetMovieSimilarityProfileCount);
        Assert.Equal(0, repository.GetSimilarMovieCandidatesCount);
    }

    [Fact]
    public void RecommendationServiceDoesNotDependOnKeywordsProvider()
    {
        var parameterTypes = typeof(RecommendationService)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType);

        Assert.DoesNotContain(parameterTypes, type => type.Name.Contains("KeywordsProvider", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SimilarityBatchCallsDoNotScaleWithSignalCount()
    {
        var repository = new CountingRecommendationRepository();
        var service = CreateService(repository, new CountingDiscoveryService());

        repository.WatchedSignalCount = 1;
        await service.GetHomeRecommendationsForCurrentUserAsync();
        var oneSignalMovieBatchCalls = repository.GetMovieCandidateIdsForSourcesCount;
        var oneSignalMovieProfileCalls = repository.GetMovieProfilesBatchCount;

        repository.ResetCounts();
        repository.WatchedSignalCount = 3;
        await service.GetHomeRecommendationsForCurrentUserAsync();

        Assert.Equal(oneSignalMovieBatchCalls, repository.GetMovieCandidateIdsForSourcesCount);
        Assert.Equal(oneSignalMovieProfileCalls, repository.GetMovieProfilesBatchCount);
        Assert.Equal(0, repository.GetMovieSimilarityProfileCount);
    }

    [Fact]
    public async Task WatchMutationInvalidationMakesCachedHomeRecommendationsRebuild()
    {
        var repository = new CountingRecommendationRepository();
        var cache = new InMemoryCacheService();
        var service = CreateService(repository, new CountingDiscoveryService(), cache);
        var invalidator = new UserAnalyticsCacheInvalidator(
            new ProfileStatisticsCache(cache),
            new InsightsCache(cache),
            cache);

        await service.GetHomeRecommendationsForCurrentUserAsync(includeColdStartDiscoverySections: false);
        await service.GetHomeRecommendationsForCurrentUserAsync(includeColdStartDiscoverySections: false);
        Assert.Equal(1, repository.GetUserContextCount);

        await invalidator.InvalidateForUserAsync(CurrentUserId);
        await service.GetHomeRecommendationsForCurrentUserAsync(includeColdStartDiscoverySections: false);

        Assert.Equal(2, repository.GetUserContextCount);
    }

    private static readonly Guid CurrentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static RecommendationService CreateService(
        IRecommendationRepository repository,
        IDiscoveryService discoveryService,
        ICacheService? cacheService = null,
        IServiceScopeFactory? scopeFactory = null) =>
        new(
            repository,
            discoveryService,
            new FakeCurrentUser(CurrentUserId),
            cacheService ?? new PassthroughCacheService(),
            new MovieApp.UnitTests.Search.SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            Options.Create(new RecommendationOptions
            {
                MinimumPersonalizationInteractions = 1,
                HomeSectionItemCount = 10,
                MaximumCandidates = 500
            }),
            NullLogger<RecommendationService>.Instance,
            scopeFactory);

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class PassthroughCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
            Task.FromResult<T?>(null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class InMemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
            Task.FromResult(_entries.TryGetValue(key, out var value) ? (T?)value : null);

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

    private sealed class CountingDiscoveryService : IDiscoveryService
    {
        public int CallCount { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(EmptyResult(criteria));
        }

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(EmptyResult(criteria));
        }

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(EmptyResult(criteria));

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(EmptyResult(criteria));
        }

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(EmptyResult(criteria));

        private static PaginatedResult<SearchItem> EmptyResult(DiscoveryCriteria criteria) =>
            new([], criteria.Page, criteria.PageSize, 0, 0);
    }

    private sealed class ColdStartRecommendationRepository : IRecommendationRepository
    {
        public int GetUserContextCount { get; private set; }

        public int LastMinimumInteractionsForEnrichment { get; private set; }

        public Task<UserRecommendationContext> GetUserRecommendationContextAsync(
            Guid userId,
            int minimumInteractionsForEnrichment = 0,
            CancellationToken cancellationToken = default)
        {
            GetUserContextCount++;
            LastMinimumInteractionsForEnrichment = minimumInteractionsForEnrichment;

            return Task.FromResult(new UserRecommendationContext(
                [],
                new HashSet<Guid>(),
                new HashSet<Guid>(),
                0));
        }

        public Task<bool> MovieExistsAsync(Guid movieId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> TvShowExistsAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<SimilaritySourceProfile?> GetMovieSimilarityProfileAsync(
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SimilaritySourceProfile?>(null);

        public Task<SimilaritySourceProfile?> GetTvShowSimilarityProfileAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SimilaritySourceProfile?>(null);

        public Task<IReadOnlyDictionary<Guid, SimilaritySourceProfile>> GetMovieSimilarityProfilesAsync(
            IReadOnlyList<Guid> movieIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, SimilaritySourceProfile>>(new Dictionary<Guid, SimilaritySourceProfile>());

        public Task<IReadOnlyDictionary<Guid, SimilaritySourceProfile>> GetTvShowSimilarityProfilesAsync(
            IReadOnlyList<Guid> tvShowIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, SimilaritySourceProfile>>(new Dictionary<Guid, SimilaritySourceProfile>());

        public Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarMovieCandidatesAsync(
            Guid sourceMovieId,
            IReadOnlyList<Guid> genreIds,
            int maxCandidates,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SimilarityCandidateProfile>>([]);

        public Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarTvShowCandidatesAsync(
            Guid sourceTvShowId,
            IReadOnlyList<Guid> genreIds,
            int maxCandidates,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SimilarityCandidateProfile>>([]);

        public Task<IReadOnlyList<Guid>> GetSimilarMovieCandidateIdsAsync(
            Guid sourceMovieId,
            IReadOnlyList<Guid> genreIds,
            int maxCandidates,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyList<Guid>> GetSimilarTvShowCandidateIdsAsync(
            Guid sourceTvShowId,
            IReadOnlyList<Guid> genreIds,
            int maxCandidates,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetSimilarMovieCandidateIdsForSourcesAsync(
            IReadOnlyList<SimilaritySourceGenreRequest> sources,
            int maxCandidates,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(new Dictionary<Guid, IReadOnlyList<Guid>>());

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetSimilarTvShowCandidateIdsForSourcesAsync(
            IReadOnlyList<SimilaritySourceGenreRequest> sources,
            int maxCandidates,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(new Dictionary<Guid, IReadOnlyList<Guid>>());

        public Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarMovieCandidatesByIdsAsync(
            IReadOnlyList<Guid> movieIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SimilarityCandidateProfile>>([]);

        public Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarTvShowCandidatesByIdsAsync(
            IReadOnlyList<Guid> tvShowIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SimilarityCandidateProfile>>([]);

        public Task<IReadOnlyList<PersonalizedCandidateProfile>> GetPersonalizedCandidatesAsync(
            RecommendationContentType type,
            IReadOnlyList<Guid> preferredGenreIds,
            IReadOnlySet<Guid> excludedMovieIds,
            IReadOnlySet<Guid> excludedTvShowIds,
            int maxCandidates,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PersonalizedCandidateProfile>>([]);
    }

    private sealed class CountingRecommendationRepository : IRecommendationRepository
    {
        private static readonly Guid MovieSourceId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid GenreId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid CandidateId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        public int WatchedSignalCount { get; set; } = 1;

        public ParallelSectionGate? SectionGate { get; init; }

        public int GetUserContextCount { get; private set; }

        public int GetMovieProfilesBatchCount { get; private set; }

        public int GetMovieCandidateIdsForSourcesCount { get; private set; }

        public int GetMovieCandidatesByIdsCount { get; private set; }

        public int GetMovieSimilarityProfileCount { get; private set; }

        public int GetSimilarMovieCandidatesCount { get; private set; }

        public void ResetCounts()
        {
            GetUserContextCount = 0;
            GetMovieProfilesBatchCount = 0;
            GetMovieCandidateIdsForSourcesCount = 0;
            GetMovieCandidatesByIdsCount = 0;
            GetMovieSimilarityProfileCount = 0;
            GetSimilarMovieCandidatesCount = 0;
        }

        public Task<UserRecommendationContext> GetUserRecommendationContextAsync(
            Guid userId,
            int minimumInteractionsForEnrichment = 0,
            CancellationToken cancellationToken = default)
        {
            GetUserContextCount++;
            _ = minimumInteractionsForEnrichment;
            var watchedSignals = Enumerable.Range(0, WatchedSignalCount)
                .Select(index => new UserBehaviorSignal(
                    Guid.Parse($"bbbbbbbb-bbbb-bbbb-bbbb-{index:D012}"),
                    "movie",
                    UserBehaviorSignalTypes.Watched,
                    $"Source {index}",
                    null,
                    DateTime.UtcNow,
                    [GenreId],
                    new Dictionary<Guid, string> { [GenreId] = "Action" },
                    []))
                .ToList();

            return Task.FromResult(new UserRecommendationContext(
                watchedSignals,
                watchedSignals.Select(signal => signal.ContentId).ToHashSet(),
                new HashSet<Guid>(),
                3));
        }

        public Task<bool> MovieExistsAsync(Guid movieId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> TvShowExistsAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<SimilaritySourceProfile?> GetMovieSimilarityProfileAsync(
            Guid movieId,
            CancellationToken cancellationToken = default)
        {
            GetMovieSimilarityProfileCount++;
            return Task.FromResult<SimilaritySourceProfile?>(CreateSourceProfile(movieId));
        }

        public Task<SimilaritySourceProfile?> GetTvShowSimilarityProfileAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SimilaritySourceProfile?>(null);

        public async Task<IReadOnlyDictionary<Guid, SimilaritySourceProfile>> GetMovieSimilarityProfilesAsync(
            IReadOnlyList<Guid> movieIds,
            CancellationToken cancellationToken = default)
        {
            GetMovieProfilesBatchCount++;
            await EnterSectionGateAsync();
            return movieIds.ToDictionary(id => id, CreateSourceProfile);
        }

        public Task<IReadOnlyDictionary<Guid, SimilaritySourceProfile>> GetTvShowSimilarityProfilesAsync(
            IReadOnlyList<Guid> tvShowIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, SimilaritySourceProfile>>(new Dictionary<Guid, SimilaritySourceProfile>());

        public Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarMovieCandidatesAsync(
            Guid sourceMovieId,
            IReadOnlyList<Guid> genreIds,
            int maxCandidates,
            CancellationToken cancellationToken = default)
        {
            GetSimilarMovieCandidatesCount++;
            return Task.FromResult<IReadOnlyList<SimilarityCandidateProfile>>([CreateCandidateProfile()]);
        }

        public Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarTvShowCandidatesAsync(
            Guid sourceTvShowId,
            IReadOnlyList<Guid> genreIds,
            int maxCandidates,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SimilarityCandidateProfile>>([]);

        public Task<IReadOnlyList<Guid>> GetSimilarMovieCandidateIdsAsync(
            Guid sourceMovieId,
            IReadOnlyList<Guid> genreIds,
            int maxCandidates,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([CandidateId]);

        public Task<IReadOnlyList<Guid>> GetSimilarTvShowCandidateIdsAsync(
            Guid sourceTvShowId,
            IReadOnlyList<Guid> genreIds,
            int maxCandidates,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetSimilarMovieCandidateIdsForSourcesAsync(
            IReadOnlyList<SimilaritySourceGenreRequest> sources,
            int maxCandidates,
            CancellationToken cancellationToken = default)
        {
            GetMovieCandidateIdsForSourcesCount++;
            return Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(
                sources.ToDictionary(
                    source => source.SourceId,
                    source => (IReadOnlyList<Guid>)[CandidateId]));
        }

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetSimilarTvShowCandidateIdsForSourcesAsync(
            IReadOnlyList<SimilaritySourceGenreRequest> sources,
            int maxCandidates,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(new Dictionary<Guid, IReadOnlyList<Guid>>());

        public Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarMovieCandidatesByIdsAsync(
            IReadOnlyList<Guid> movieIds,
            CancellationToken cancellationToken = default)
        {
            GetMovieCandidatesByIdsCount++;
            return Task.FromResult<IReadOnlyList<SimilarityCandidateProfile>>([CreateCandidateProfile()]);
        }

        public Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarTvShowCandidatesByIdsAsync(
            IReadOnlyList<Guid> tvShowIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SimilarityCandidateProfile>>([]);

        public async Task<IReadOnlyList<PersonalizedCandidateProfile>> GetPersonalizedCandidatesAsync(
            RecommendationContentType type,
            IReadOnlyList<Guid> preferredGenreIds,
            IReadOnlySet<Guid> excludedMovieIds,
            IReadOnlySet<Guid> excludedTvShowIds,
            int maxCandidates,
            CancellationToken cancellationToken = default)
        {
            await EnterSectionGateAsync();
            return
            [
                new PersonalizedCandidateProfile(
                    CandidateId,
                    "movie",
                    "Candidate",
                    null,
                    null,
                    null,
                    null,
                    null,
                    8m,
                    100,
                    2020,
                    [GenreId],
                    new Dictionary<Guid, string> { [GenreId] = "Action" },
                    [],
                    null)
            ];
        }

        private async Task EnterSectionGateAsync()
        {
            if (SectionGate is not null)
            {
                await SectionGate.EnterAsync();
            }
        }

        private static SimilaritySourceProfile CreateSourceProfile(Guid movieId) =>
            new(
                movieId,
                "movie",
                "Source",
                [GenreId],
                new Dictionary<Guid, string> { [GenreId] = "Action" },
                [],
                8m,
                2020);

        private static SimilarityCandidateProfile CreateCandidateProfile() =>
            new(
                CandidateId,
                "movie",
                "Candidate",
                null,
                null,
                null,
                null,
                null,
                8m,
                100,
                2020,
                [GenreId],
                new Dictionary<Guid, string> { [GenreId] = "Action" },
                []);
    }

    private sealed class ParallelSectionGate
    {
        private int _inFlight;

        public int MaxInFlight { get; private set; }

        public async Task EnterAsync()
        {
            var current = Interlocked.Increment(ref _inFlight);
            lock (this)
            {
                if (current > MaxInFlight)
                {
                    MaxInFlight = current;
                }
            }

            try
            {
                await Task.Delay(40);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }
    }

    private sealed class RecommendationScopeFactory(IRecommendationRepository repository) : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IRecommendationService>(
                CreateService(repository, new CountingDiscoveryService()));
            return services.BuildServiceProvider().CreateScope();
        }
    }
}

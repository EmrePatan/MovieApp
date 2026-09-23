using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Recommendations;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.UnitTests.Search;

namespace MovieApp.UnitTests.Discovery;

public sealed class PickSomethingServiceTests
{
    private static readonly Guid SciFiGenreId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid WatchedMovieId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid WatchlistMovieId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid CandidateMovieId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid TrendingMovieId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid TrendingTvId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

    [Fact]
    public async Task PickAsyncUsesCatalogTrendingForGuestColdStart()
    {
        var discovery = new RecordingDiscoveryService
        {
            TrendingItems =
            [
                CreateSearchItem(TrendingMovieId, "movie", "Trending Movie", 1000, 8.5m),
                CreateSearchItem(TrendingTvId, "tv", "Trending Show", 900, 8.0m)
            ]
        };
        var service = CreateService(new StubRecommendationRepository(), discovery, new GuestCurrentUser());

        var pick = await service.PickAsync(CreateCriteria(RecommendationContentType.All), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotNull(pick);
        Assert.Equal(1, discovery.TrendingCallCount);
        Assert.Equal(0, discovery.PopularCallCount);
        Assert.Contains(pick.Id, new[] { TrendingMovieId, TrendingTvId });
        Assert.Equal("Trending right now", pick.Reason);
    }

    [Fact]
    public async Task PickAsyncFallsBackToPopularWhenTrendingIsEmpty()
    {
        var discovery = new RecordingDiscoveryService
        {
            PopularItems = [CreateSearchItem(TrendingMovieId, "movie", "Popular Movie", 800, 7.5m)]
        };
        var service = CreateService(new StubRecommendationRepository(), discovery, new GuestCurrentUser());

        var pick = await service.PickAsync(CreateCriteria(RecommendationContentType.Movie), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotNull(pick);
        Assert.Equal(TrendingMovieId, pick.Id);
        Assert.Equal("Popular right now", pick.Reason);
        Assert.Equal(1, discovery.PopularCallCount);
    }

    [Fact]
    public async Task PickAsyncReturnsNullWhenCatalogIsEmpty()
    {
        var service = CreateService(
            new StubRecommendationRepository(),
            new RecordingDiscoveryService(),
            new GuestCurrentUser());

        var pick = await service.PickAsync(CreateCriteria(RecommendationContentType.All), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Null(pick);
    }

    [Theory]
    [InlineData(RecommendationContentType.Movie, "movie")]
    [InlineData(RecommendationContentType.Tv, "tv")]
    public async Task PickAsyncRestrictsColdStartMediaType(
        RecommendationContentType mediaType,
        string expectedType)
    {
        var discovery = new RecordingDiscoveryService
        {
            TrendingItems =
            [
                CreateSearchItem(TrendingMovieId, "movie", "Trending Movie", 1000, 8.5m),
                CreateSearchItem(TrendingTvId, "tv", "Trending Show", 900, 8.0m)
            ]
        };
        var service = CreateService(new StubRecommendationRepository(), discovery, new GuestCurrentUser());

        var pick = await service.PickAsync(CreateCriteria(mediaType), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotNull(pick);
        Assert.Equal(expectedType, pick.Type);
    }

    [Fact]
    public async Task PickAsyncExcludesSessionIdsFromColdStart()
    {
        var discovery = new RecordingDiscoveryService
        {
            TrendingItems =
            [
                CreateSearchItem(TrendingMovieId, "movie", "Trending Movie", 1000, 8.5m),
                CreateSearchItem(TrendingTvId, "tv", "Trending Show", 900, 8.0m)
            ]
        };
        var service = CreateService(new StubRecommendationRepository(), discovery, new GuestCurrentUser());

        var pick = await service.PickAsync(CreateCriteria(
            RecommendationContentType.All,
            [TrendingMovieId]), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotNull(pick);
        Assert.Equal(TrendingTvId, pick.Id);
    }

    [Fact]
    public async Task PickAsyncUsesPersonalizedPathForEligibleUser()
    {
        var repository = new StubRecommendationRepository
        {
            Context = CreatePersonalizedContext(),
            Candidates = [CreateCandidate(CandidateMovieId, "movie", "Candidate Movie", [SciFiGenreId])]
        };
        var discovery = new RecordingDiscoveryService();
        var service = CreateService(repository, discovery, new AuthenticatedCurrentUser(UserId));

        var pick = await service.PickAsync(CreateCriteria(RecommendationContentType.Movie), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotNull(pick);
        Assert.Equal(CandidateMovieId, pick.Id);
        Assert.Equal(1, repository.GetUserContextCount);
        Assert.Equal(1, repository.GetPersonalizedCandidatesCount);
        Assert.Equal(0, discovery.TrendingCallCount);
    }

    [Fact]
    public async Task PickAsyncDoesNotEmitComedyReasonForNonDominantGenreMatch()
    {
        var comedyGenreId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var dramaGenreId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var repository = new StubRecommendationRepository
        {
            Context = CreatePersonalizedContextWithComedyPreference(comedyGenreId),
            Candidates =
            [
                CreateCandidate(
                    CandidateMovieId,
                    "movie",
                    "The Arrival",
                    [dramaGenreId, SciFiGenreId, comedyGenreId])
            ]
        };
        var service = CreateService(repository, new RecordingDiscoveryService(), new AuthenticatedCurrentUser(UserId));

        var pick = await service.PickAsync(CreateCriteria(RecommendationContentType.Movie), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotNull(pick);
        Assert.DoesNotContain("Comedy", pick.Reason ?? string.Empty);
    }

    [Fact]
    public async Task PickAsyncKeepsWatchlistTitlesEligibleAndBoostsReason()
    {
        var repository = new StubRecommendationRepository
        {
            Context = CreatePersonalizedContext(includeWatchlist: true),
            Candidates = [CreateCandidate(WatchlistMovieId, "movie", "Watchlist Movie", [SciFiGenreId])]
        };
        var service = CreateService(repository, new RecordingDiscoveryService(), new AuthenticatedCurrentUser(UserId));

        var pick = await service.PickAsync(CreateCriteria(RecommendationContentType.Movie), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotNull(pick);
        Assert.Equal(WatchlistMovieId, pick.Id);
        Assert.Equal("From your watchlist", pick.Reason);
        Assert.DoesNotContain(WatchlistMovieId, repository.LastExcludedMovieIds);
    }

    [Fact]
    public async Task PickAsyncExcludesWatchedTitlesFromPersonalizedCandidates()
    {
        var repository = new StubRecommendationRepository
        {
            Context = CreatePersonalizedContext(),
            Candidates =
            [
                CreateCandidate(WatchedMovieId, "movie", "Watched Movie", [SciFiGenreId]),
                CreateCandidate(CandidateMovieId, "movie", "Candidate Movie", [SciFiGenreId])
            ]
        };
        var service = CreateService(repository, new RecordingDiscoveryService(), new AuthenticatedCurrentUser(UserId));

        var pick = await service.PickAsync(CreateCriteria(RecommendationContentType.Movie), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotNull(pick);
        Assert.Equal(CandidateMovieId, pick.Id);
        Assert.Contains(WatchedMovieId, repository.LastExcludedMovieIds);
    }

    [Fact]
    public async Task PickAsyncPassesSessionExclusionsToPersonalizedRepository()
    {
        var repository = new StubRecommendationRepository
        {
            Context = CreatePersonalizedContext(),
            Candidates = [CreateCandidate(CandidateMovieId, "movie", "Candidate Movie", [SciFiGenreId])]
        };
        var service = CreateService(repository, new RecordingDiscoveryService(), new AuthenticatedCurrentUser(UserId));
        var sessionExcludedId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        await service.PickAsync(CreateCriteria(
            RecommendationContentType.Movie,
            [sessionExcludedId]), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Contains(sessionExcludedId, repository.LastExcludedMovieIds);
    }

    [Fact]
    public async Task PickAsyncDoesNotRepeatSessionExcludedPick()
    {
        var discovery = new RecordingDiscoveryService
        {
            TrendingItems =
            [
                CreateSearchItem(TrendingMovieId, "movie", "Trending Movie", 1000, 8.5m)
            ]
        };
        var service = CreateService(new StubRecommendationRepository(), discovery, new GuestCurrentUser());

        var pick = await service.PickAsync(CreateCriteria(
            RecommendationContentType.Movie,
            [TrendingMovieId]), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Null(pick);
    }

    private static PickSomethingService CreateService(
        IRecommendationRepository repository,
        IDiscoveryService discoveryService,
        ICurrentUser currentUser) =>
        new(
            repository,
            discoveryService,
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            currentUser,
            Options.Create(new RecommendationOptions
            {
                MinimumPersonalizationInteractions = 1,
                MaximumCandidates = 50
            }));

    private static PickSomethingCriteria CreateCriteria(
        RecommendationContentType mediaType,
        IReadOnlyCollection<Guid>? sessionExcludedIds = null) =>
        new(mediaType, sessionExcludedIds?.ToHashSet() ?? new HashSet<Guid>());

    private static UserRecommendationContext CreatePersonalizedContextWithComedyPreference(Guid comedyGenreId)
    {
        var signals = new List<UserBehaviorSignal>
        {
            CreateSignal(
                WatchedMovieId,
                "movie",
                UserBehaviorSignalTypes.Favorite,
                9,
                DateTime.UtcNow.AddDays(-2),
                [comedyGenreId],
                new Dictionary<Guid, string> { [comedyGenreId] = "Comedy" }),
            CreateSignal(
                Guid.Parse("11111111-1111-1111-1111-111111111112"),
                "movie",
                UserBehaviorSignalTypes.Favorite,
                9,
                DateTime.UtcNow.AddDays(-3),
                [comedyGenreId],
                new Dictionary<Guid, string> { [comedyGenreId] = "Comedy" }),
            CreateSignal(
                Guid.Parse("11111111-1111-1111-1111-111111111113"),
                "movie",
                UserBehaviorSignalTypes.Favorite,
                9,
                DateTime.UtcNow.AddDays(-4),
                [comedyGenreId],
                new Dictionary<Guid, string> { [comedyGenreId] = "Comedy" })
        };

        return new UserRecommendationContext(
            signals,
            new HashSet<Guid> { WatchedMovieId },
            new HashSet<Guid>(),
            3);
    }

    private static UserRecommendationContext CreatePersonalizedContext(bool includeWatchlist = false)
    {
        var signals = new List<UserBehaviorSignal>
        {
            CreateSignal(WatchedMovieId, "movie", UserBehaviorSignalTypes.Watched, 8, DateTime.UtcNow.AddDays(-3))
        };

        if (includeWatchlist)
        {
            signals.Add(CreateSignal(
                WatchlistMovieId,
                "movie",
                UserBehaviorSignalTypes.Watchlist,
                null,
                DateTime.UtcNow.AddDays(-1)));
        }

        var excludedMovieIds = new HashSet<Guid> { WatchedMovieId };
        if (includeWatchlist)
        {
            excludedMovieIds.Add(WatchlistMovieId);
        }

        return new UserRecommendationContext(
            signals,
            excludedMovieIds,
            new HashSet<Guid>(),
            1);
    }

    private static UserBehaviorSignal CreateSignal(
        Guid contentId,
        string contentType,
        string signalType,
        int? ratingScore,
        DateTime? signalAtUtc,
        IReadOnlyList<Guid>? genreIds = null,
        IReadOnlyDictionary<Guid, string>? genreNames = null) =>
        new(
            contentId,
            contentType,
            signalType,
            $"Title {contentId}",
            ratingScore,
            signalAtUtc,
            genreIds ?? [SciFiGenreId],
            genreNames ?? new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" },
            []);

    private static PersonalizedCandidateProfile CreateCandidate(
        Guid id,
        string type,
        string title,
        IReadOnlyList<Guid> genreIds) =>
        new(
            id,
            type,
            title,
            null,
            null,
            null,
            null,
            null,
            8.0m,
            500,
            2024,
            genreIds,
            new Dictionary<Guid, string> { [genreIds[0]] = "Science Fiction" },
            [],
            null);

    private static SearchItem CreateSearchItem(
        Guid id,
        string type,
        string title,
        int voteCount,
        decimal voteAverage) =>
        new(
            id,
            type,
            title,
            null,
            null,
            null,
            null,
            null,
            voteAverage,
            voteCount,
            2024);

    private sealed class GuestCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => false;

        public Guid? UserId => null;
    }

    private sealed class AuthenticatedCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class RecordingDiscoveryService : IDiscoveryService
    {
        public int TrendingCallCount { get; private set; }

        public int PopularCallCount { get; private set; }

        public IReadOnlyList<SearchItem> TrendingItems { get; init; } = [];

        public IReadOnlyList<SearchItem> PopularItems { get; init; } = [];

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            PopularCallCount++;
            return Task.FromResult(CreateResult(PopularItems, criteria));
        }

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            TrendingCallCount++;
            return Task.FromResult(CreateResult(TrendingItems, criteria));
        }

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResult([], criteria));

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResult([], criteria));

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResult([], criteria));

        private static PaginatedResult<SearchItem> CreateResult(
            IReadOnlyList<SearchItem> items,
            DiscoveryCriteria criteria) =>
            new(items.ToList(), criteria.Page, criteria.PageSize, items.Count, items.Count == 0 ? 0 : 1);
    }

    private static bool MatchesMediaType(RecommendationContentType mediaType, string candidateType) =>
        mediaType switch
        {
            RecommendationContentType.Movie => candidateType == "movie",
            RecommendationContentType.Tv => candidateType == "tv",
            _ => candidateType is "movie" or "tv"
        };

    private sealed class StubRecommendationRepository : IRecommendationRepository
    {
        public UserRecommendationContext Context { get; init; } =
            new([], new HashSet<Guid>(), new HashSet<Guid>(), 0);

        public IReadOnlyList<PersonalizedCandidateProfile> Candidates { get; init; } = [];

        public int GetUserContextCount { get; private set; }

        public int GetPersonalizedCandidatesCount { get; private set; }

        public IReadOnlySet<Guid> LastExcludedMovieIds { get; private set; } = new HashSet<Guid>();

        public Task<UserRecommendationContext> GetUserRecommendationContextAsync(
            Guid userId,
            int minimumInteractionsForEnrichment = 0,
            CancellationToken cancellationToken = default)
        {
            GetUserContextCount++;
            return Task.FromResult(Context);
        }

        public Task<IReadOnlyList<PersonalizedCandidateProfile>> GetPersonalizedCandidatesAsync(
            RecommendationContentType type,
            IReadOnlyList<Guid> preferredGenreIds,
            IReadOnlySet<Guid> excludedMovieIds,
            IReadOnlySet<Guid> excludedTvShowIds,
            int maxCandidates,
            CancellationToken cancellationToken = default)
        {
            GetPersonalizedCandidatesCount++;
            LastExcludedMovieIds = excludedMovieIds;
            IReadOnlyList<PersonalizedCandidateProfile> filtered = Candidates
                .Where(candidate =>
                    MatchesMediaType(type, candidate.Type) &&
                    (candidate.Type != "movie" || !excludedMovieIds.Contains(candidate.Id)) &&
                    (candidate.Type != "tv" || !excludedTvShowIds.Contains(candidate.Id)))
                .ToList();

            return Task.FromResult(filtered);
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
    }
}

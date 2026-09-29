using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Recommendations;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Application.Services.Search;
using MovieApp.UnitTests.Search;

namespace MovieApp.UnitTests.Recommendations;

public sealed class RecommendationPersonalizationGateTests
{
    [Fact]
    public async Task HomePersonalizedPathStaysColdWhenSignalsAreOnlyLowRatings()
    {
        var repository = new LowRatingContextRepository();
        var service = new RecommendationService(
            repository,
            new EmptyDiscoveryService(),
            new FakeCurrentUser(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            new PassthroughCacheService(),
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            Options.Create(new RecommendationOptions
            {
                MinimumPersonalizationInteractions = 3,
                HomeSectionItemCount = 10
            }),
            NullLogger<RecommendationService>.Instance);

        var sections = await service.GetHomeRecommendationsForCurrentUserAsync(
            includeColdStartDiscoverySections: false);

        Assert.Empty(sections);
        Assert.Equal(0, repository.CandidateFetchCount);
    }

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
            where T : class =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class EmptyDiscoveryService : IDiscoveryService
    {
        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 0, 0));

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 0, 0));

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 0, 0));

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 0, 0));

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 0, 0));
    }

    private sealed class LowRatingContextRepository : IRecommendationRepository
    {
        public int CandidateFetchCount { get; private set; }

        public Task<UserRecommendationContext> GetUserRecommendationContextAsync(
            Guid userId,
            int minimumInteractionsForEnrichment = 0,
            int mildRatingMinScore = 6,
            CancellationToken cancellationToken = default)
        {
            var signals = Enumerable.Range(0, 3)
                .Select(index => new UserBehaviorSignal(
                    Guid.NewGuid(),
                    "movie",
                    UserBehaviorSignalTypes.Rating,
                    $"Low {index}",
                    2,
                    DateTime.UtcNow,
                    [],
                    new Dictionary<Guid, string>(),
                    []))
                .ToList();

            return Task.FromResult(new UserRecommendationContext(
                signals,
                signals.Select(signal => signal.ContentId).ToHashSet(),
                new HashSet<Guid>(),
                3));
        }

        public Task<bool> MovieExistsAsync(Guid movieId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> TvShowExistsAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<SimilaritySourceProfile?> GetMovieSimilarityProfileAsync(Guid movieId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SimilaritySourceProfile?>(null);

        public Task<SimilaritySourceProfile?> GetTvShowSimilarityProfileAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SimilaritySourceProfile?>(null);

        public Task<IReadOnlyDictionary<Guid, SimilaritySourceProfile>> GetMovieSimilarityProfilesAsync(IReadOnlyList<Guid> movieIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, SimilaritySourceProfile>>(new Dictionary<Guid, SimilaritySourceProfile>());

        public Task<IReadOnlyDictionary<Guid, SimilaritySourceProfile>> GetTvShowSimilarityProfilesAsync(IReadOnlyList<Guid> tvShowIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, SimilaritySourceProfile>>(new Dictionary<Guid, SimilaritySourceProfile>());

        public Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarMovieCandidatesAsync(Guid sourceMovieId, IReadOnlyList<Guid> genreIds, int maxCandidates, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SimilarityCandidateProfile>>([]);

        public Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarTvShowCandidatesAsync(Guid sourceTvShowId, IReadOnlyList<Guid> genreIds, int maxCandidates, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SimilarityCandidateProfile>>([]);

        public Task<IReadOnlyList<Guid>> GetSimilarMovieCandidateIdsAsync(Guid sourceMovieId, IReadOnlyList<Guid> genreIds, int maxCandidates, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyList<Guid>> GetSimilarTvShowCandidateIdsAsync(Guid sourceTvShowId, IReadOnlyList<Guid> genreIds, int maxCandidates, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetSimilarMovieCandidateIdsForSourcesAsync(IReadOnlyList<SimilaritySourceGenreRequest> sources, int maxCandidates, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(new Dictionary<Guid, IReadOnlyList<Guid>>());

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetSimilarTvShowCandidateIdsForSourcesAsync(IReadOnlyList<SimilaritySourceGenreRequest> sources, int maxCandidates, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(new Dictionary<Guid, IReadOnlyList<Guid>>());

        public Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarMovieCandidatesByIdsAsync(IReadOnlyList<Guid> movieIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SimilarityCandidateProfile>>([]);

        public Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarTvShowCandidatesByIdsAsync(IReadOnlyList<Guid> tvShowIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SimilarityCandidateProfile>>([]);

        public Task<IReadOnlyList<PersonalizedCandidateProfile>> GetPersonalizedCandidatesAsync(
            RecommendationContentType type,
            IReadOnlyList<Guid> preferredGenreIds,
            IReadOnlySet<Guid> excludedMovieIds,
            IReadOnlySet<Guid> excludedTvShowIds,
            int maxCandidates,
            CancellationToken cancellationToken = default)
        {
            CandidateFetchCount++;
            return Task.FromResult<IReadOnlyList<PersonalizedCandidateProfile>>([]);
        }
    }
}

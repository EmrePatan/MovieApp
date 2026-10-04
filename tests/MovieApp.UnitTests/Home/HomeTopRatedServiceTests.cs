using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Home;

public sealed class HomeTopRatedServiceTests
{
    private static readonly Guid AnimationGenreId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task GetItemsAsyncOverFetchesCandidatesBeforeApplyingAnimationCap()
    {
        var discovery = new RecordingDiscoveryService();
        var service = CreateService(discovery);

        await service.GetItemsAsync(SearchContentType.All, 10, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(50, discovery.LastCriteria?.PageSize);
    }

    [Fact]
    public async Task GetItemsAsyncUsesCatalogAnimationGenreId()
    {
        var discovery = new RecordingDiscoveryService(
        [
            new SearchItem(
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                "movie",
                "Candidate",
                null,
                null,
                null,
                null,
                null,
                8m,
                1000,
                null)
        ]);
        var genreRepository = new FakeGenreReadRepository(AnimationGenreId);
        var searchRepository = new FakeSearchRepository();
        var service = CreateService(discovery, genreRepository, searchRepository);

        await service.GetItemsAsync(SearchContentType.All, 10, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal("Animation", genreRepository.LastRequestedGenreName);
    }

    [Fact]
    public async Task GetItemsAsyncExcludesTitlesBelowHomeVoteFloors()
    {
        var lowMovie = CreateItem("movie", 11, 999);
        var qualifiedMovie = CreateItem("movie", 12, 1000);
        var lowTv = CreateItem("tv", 13, 499);
        var qualifiedTv = CreateItem("tv", 14, 500);
        var discovery = new RecordingDiscoveryService([lowMovie, qualifiedMovie, lowTv, qualifiedTv]);
        var service = CreateService(discovery);

        var result = await service.GetItemsAsync(SearchContentType.All, 10, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal([qualifiedMovie.Id, qualifiedTv.Id], result.Select(item => item.Id).ToArray());
    }

    [Fact]
    public void TopRatedOptionsDefaultToHomeFloorsAndHigherBayesianConfidence()
    {
        var options = new TopRatedOptions();

        Assert.Equal(300, options.MinimumVoteConfidence);
        Assert.Equal(1000, options.MinVoteCountMovie);
        Assert.Equal(500, options.MinVoteCountTv);
    }

    private static SearchItem CreateItem(string type, int seed, int voteCount) =>
        new(
            Guid.Parse($"dddddddd-dddd-dddd-dddd-{seed:D012}"),
            type,
            $"{type}-{seed}",
            null,
            null,
            null,
            null,
            new DateOnly(2020, 1, 1),
            8m,
            voteCount,
            2020);

    private static HomeTopRatedService CreateService(
        IDiscoveryService discovery,
        IGenreReadRepository? genreRepository = null,
        ISearchRepository? searchRepository = null) =>
        new(
            discovery,
            genreRepository ?? new FakeGenreReadRepository(null),
            searchRepository ?? new FakeSearchRepository(),
            Options.Create(new TopRatedOptions
            {
                MinimumVoteConfidence = 100,
                HomeRailAnimationGenreName = "Animation",
                HomeRailMaxAnimationItems = 3,
                HomeRailCandidateFetchSize = 50
            }));

    private sealed class RecordingDiscoveryService(IReadOnlyList<SearchItem>? items = null) : IDiscoveryService
    {
        private readonly IReadOnlyList<SearchItem> _items = items ?? [];

        public DiscoveryCriteria? LastCriteria { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            LastCriteria = criteria;
            return Task.FromResult(new PaginatedResult<SearchItem>(
                _items.Take(criteria.PageSize).ToList(),
                criteria.Page,
                criteria.PageSize,
                _items.Count,
                _items.Count == 0 ? 0 : 1));
        }

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
            string genreName,
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeGenreReadRepository(Guid? animationGenreId) : IGenreReadRepository
    {
        public string? LastRequestedGenreName { get; private set; }

        public Task<IReadOnlyList<(Guid Id, string Name)>> GetAllOrderedByNameAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(Guid Id, string Name)>>([]);

        public Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
            IReadOnlyList<Guid> genreIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

        public Task<Guid?> GetIdByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            LastRequestedGenreName = name;
            return Task.FromResult(animationGenreId);
        }

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByMovieIdsAsync(
            IReadOnlyList<Guid> movieIds,
            int maxGenresPerItem,
            CancellationToken cancellationToken = default) =>
            MovieApp.UnitTests.Persistence.GenreReadRepositoryTestDefaults.EmptyMovieGenresAsync(
                movieIds,
                maxGenresPerItem,
                cancellationToken);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByTvShowIdsAsync(
            IReadOnlyList<Guid> tvShowIds,
            int maxGenresPerItem,
            CancellationToken cancellationToken = default) =>
            MovieApp.UnitTests.Persistence.GenreReadRepositoryTestDefaults.EmptyTvGenresAsync(
                tvShowIds,
                maxGenresPerItem,
                cancellationToken);
    }

    private sealed class FakeSearchRepository : ISearchRepository
    {
        public Task<PaginatedResult<SearchItem>> SearchAsync(
            SearchCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(string query, int limit, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<decimal> GetCatalogMeanVoteAverageAsync(
            SearchContentType type,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(6.0m);

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithGenreAsync(
            IReadOnlyList<SearchItem> items,
            Guid genreId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<CatalogContentKey>>(new HashSet<CatalogContentKey>());

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithAnyGenreAsync(
            IReadOnlyList<SearchItem> items,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<CatalogContentKey>>(
                items.Select(item => new CatalogContentKey(item.Id, item.Type)).ToHashSet());

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
            string genreName,
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

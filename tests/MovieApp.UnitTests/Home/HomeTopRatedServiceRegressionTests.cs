using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Home;

public sealed class HomeTopRatedServiceRegressionTests
{
    private static readonly Guid AnimationGenreId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task GetItemsAsyncLimitsAnimationToThreeWhenSixHighRankingAnimationExist()
    {
        var candidates = CreateMixedCandidates(animationCount: 6, dramaCount: 10);
        var service = CreateService(candidates, ConfigureAllWithDramaGenre, ConfigureAnimationGenre);

        var result = await service.GetItemsAsync(SearchContentType.All, 10, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(10, result.Count);
        Assert.Equal(3, CountAnimation(result));
    }

    [Fact]
    public async Task GetItemsAsyncLimitsAnimationAcrossMixedMovieAndTvCandidates()
    {
        var candidates = new List<SearchItem>
        {
            CreateItem("animation-movie-0", "movie", 0, isAnimation: true),
            CreateItem("animation-movie-1", "movie", 1, isAnimation: true),
            CreateItem("animation-tv-0", "tv", 2, isAnimation: true),
            CreateItem("animation-tv-1", "tv", 3, isAnimation: true),
            CreateItem("drama-movie-0", "movie", 4, isAnimation: false),
            CreateItem("drama-tv-0", "tv", 5, isAnimation: false),
            CreateItem("drama-movie-1", "movie", 6, isAnimation: false),
            CreateItem("drama-tv-1", "tv", 7, isAnimation: false),
            CreateItem("drama-movie-2", "movie", 8, isAnimation: false),
            CreateItem("drama-tv-2", "tv", 9, isAnimation: false),
            CreateItem("drama-movie-3", "movie", 10, isAnimation: false),
            CreateItem("drama-tv-3", "tv", 11, isAnimation: false),
        };

        var service = CreateService(
            candidates,
            items => items.Select(item => new CatalogContentKey(item.Id, item.Type)).ToHashSet(),
            items => items
                .Where(item => item.Title.StartsWith("animation", StringComparison.Ordinal))
                .Select(item => new CatalogContentKey(item.Id, item.Type))
                .ToHashSet());

        var result = await service.GetItemsAsync(SearchContentType.All, 10, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(10, result.Count);
        Assert.Equal(3, CountAnimation(result));
    }

    [Fact]
    public async Task GetItemsAsyncExcludesGenreLessHighRankedTitle()
    {
        var genreLess = new SearchItem(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            "movie",
            "genre-less",
            null,
            null,
            null,
            null,
            new DateOnly(2020, 1, 1),
            10m,
            1000,
            2020);
        var candidates = new List<SearchItem> { genreLess };
        candidates.AddRange(CreateMixedCandidates(animationCount: 0, dramaCount: 12));

        var service = CreateService(
            candidates,
            items => items
                .Where(item => item.Title != "genre-less")
                .Select(item => new CatalogContentKey(item.Id, item.Type))
                .ToHashSet(),
            _ => new HashSet<CatalogContentKey>());

        var result = await service.GetItemsAsync(SearchContentType.All, 10, ContentLocaleResolver.EnglishUnitedStates);

        Assert.DoesNotContain(result, item => item.Title == "genre-less");
    }

    [Fact]
    public async Task GetItemsAsyncExcludesGenreLessTitleEvenWithExcellentVotes()
    {
        var genreLess = new SearchItem(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            "movie",
            "genre-less-hit",
            null,
            null,
            null,
            null,
            new DateOnly(2020, 1, 1),
            10m,
            10_000,
            2020);
        var candidates = new List<SearchItem> { genreLess };
        candidates.AddRange(CreateMixedCandidates(animationCount: 0, dramaCount: 12));

        var service = CreateService(
            candidates,
            items => items
                .Where(item => item.Title != "genre-less-hit")
                .Select(item => new CatalogContentKey(item.Id, item.Type))
                .ToHashSet(),
            _ => new HashSet<CatalogContentKey>());

        var result = await service.GetItemsAsync(SearchContentType.All, 10, ContentLocaleResolver.EnglishUnitedStates);

        Assert.DoesNotContain(result, item => item.Title == "genre-less-hit");
    }

    [Fact]
    public async Task GetItemsAsyncPreservesHighestRankedAnimationTitles()
    {
        var candidates = CreateMixedCandidates(animationCount: 6, dramaCount: 10);
        var service = CreateService(candidates, ConfigureAllWithDramaGenre, ConfigureAnimationGenre);

        var result = await service.GetItemsAsync(SearchContentType.All, 10, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(
            ["animation-0", "animation-1", "animation-2"],
            result.Where(CountAsAnimation).Select(item => item.Title).ToList());
    }

    [Fact]
    public async Task GetItemsAsyncPreservesNonAnimationBayesianOrder()
    {
        var candidates = CreateMixedCandidates(animationCount: 6, dramaCount: 10);
        var service = CreateService(candidates, ConfigureAllWithDramaGenre, ConfigureAnimationGenre);

        var result = await service.GetItemsAsync(SearchContentType.All, 10, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(
            ["drama-0", "drama-1", "drama-2", "drama-3", "drama-4", "drama-5", "drama-6"],
            result.Where(item => !CountAsAnimation(item)).Select(item => item.Title).ToList());
    }

    [Fact]
    public async Task GetItemsAsyncReturnsFewerResultsWhenInsufficientNonAnimationCandidatesExist()
    {
        var candidates = CreateMixedCandidates(animationCount: 6, dramaCount: 2);
        var service = CreateService(candidates, ConfigureAllWithDramaGenre, ConfigureAnimationGenre);

        var result = await service.GetItemsAsync(SearchContentType.All, 10, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(5, result.Count);
        Assert.Equal(3, CountAnimation(result));
    }

    [Fact]
    public async Task GetItemsAsyncReturnsFewerResultsWhenInsufficientGenreQualifiedCandidatesExist()
    {
        var candidates = CreateMixedCandidates(animationCount: 2, dramaCount: 3);
        var service = CreateService(
            candidates,
            items => items
                .Where(item => item.Title.StartsWith("drama", StringComparison.Ordinal))
                .Select(item => new CatalogContentKey(item.Id, item.Type))
                .ToHashSet(),
            ConfigureAnimationGenre);

        var result = await service.GetItemsAsync(SearchContentType.All, 10, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(3, result.Count);
        Assert.All(result, item => Assert.StartsWith("drama", item.Title, StringComparison.Ordinal));
    }

    private static HomeTopRatedService CreateService(
        IReadOnlyList<SearchItem> candidates,
        Func<IReadOnlyList<SearchItem>, IReadOnlySet<CatalogContentKey>> genreQualifiedKeys,
        Func<IReadOnlyList<SearchItem>, IReadOnlySet<CatalogContentKey>> animationKeys) =>
        new(
            new RecordingDiscoveryService(candidates),
            new FakeGenreReadRepository(AnimationGenreId),
            new ConfigurableSearchRepository(genreQualifiedKeys, animationKeys),
            Options.Create(new TopRatedOptions
            {
                MinimumVoteConfidence = 100,
                HomeRailAnimationGenreName = "Animation",
                HomeRailMaxAnimationItems = 3,
                HomeRailCandidateFetchSize = 50
            }));

    private static IReadOnlySet<CatalogContentKey> ConfigureAllWithDramaGenre(IReadOnlyList<SearchItem> items) =>
        items.Select(item => new CatalogContentKey(item.Id, item.Type)).ToHashSet();

    private static IReadOnlySet<CatalogContentKey> ConfigureAnimationGenre(IReadOnlyList<SearchItem> items) =>
        items
            .Where(item => item.Title.StartsWith("animation", StringComparison.Ordinal))
            .Select(item => new CatalogContentKey(item.Id, item.Type))
            .ToHashSet();

    private static List<SearchItem> CreateMixedCandidates(int animationCount, int dramaCount)
    {
        var candidates = new List<SearchItem>(animationCount + dramaCount);

        for (var index = 0; index < animationCount; index++)
        {
            candidates.Add(CreateItem($"animation-{index}", index % 2 == 0 ? "movie" : "tv", index, true));
        }

        for (var index = 0; index < dramaCount; index++)
        {
            candidates.Add(CreateItem($"drama-{index}", index % 2 == 0 ? "movie" : "tv", animationCount + index, false));
        }

        return candidates;
    }

    private static SearchItem CreateItem(string title, string type, int rank, bool isAnimation)
    {
        _ = isAnimation;
        var seed = rank + 1;
        return new SearchItem(
            Guid.Parse($"bbbbbbbb-bbbb-bbbb-bbbb-{seed:D012}"),
            type,
            title,
            null,
            null,
            null,
            null,
            new DateOnly(2020, 1, 1),
            10m - (rank * 0.1m),
            1000 - rank,
            2020);
    }

    private static bool CountAsAnimation(SearchItem item) =>
        item.Title.StartsWith("animation", StringComparison.Ordinal);

    private static int CountAnimation(IReadOnlyList<SearchItem> items) =>
        items.Count(CountAsAnimation);

    private sealed class RecordingDiscoveryService(IReadOnlyList<SearchItem> items) : IDiscoveryService
    {
        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>(
                items.Take(criteria.PageSize).ToList(),
                criteria.Page,
                criteria.PageSize,
                items.Count,
                1));

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeGenreReadRepository(Guid? animationGenreId) : IGenreReadRepository
    {
        public Task<IReadOnlyList<(Guid Id, string Name)>> GetAllOrderedByNameAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(Guid Id, string Name)>>([]);

        public Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
            IReadOnlyList<Guid> genreIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

        public Task<Guid?> GetIdByNameAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(animationGenreId);
    }

    private sealed class ConfigurableSearchRepository(
        Func<IReadOnlyList<SearchItem>, IReadOnlySet<CatalogContentKey>> genreQualifiedKeys,
        Func<IReadOnlyList<SearchItem>, IReadOnlySet<CatalogContentKey>> animationKeys) : ISearchRepository
    {
        public Task<PaginatedResult<SearchItem>> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<decimal> GetCatalogMeanVoteAverageAsync(
            SearchContentType type,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(6.0m);

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithGenreAsync(
            IReadOnlyList<SearchItem> items,
            Guid genreId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(animationKeys(items));

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithAnyGenreAsync(
            IReadOnlyList<SearchItem> items,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(genreQualifiedKeys(items));

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

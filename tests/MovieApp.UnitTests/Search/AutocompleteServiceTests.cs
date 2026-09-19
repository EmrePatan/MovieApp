using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieApp.UnitTests.Search;

public sealed class AutocompleteServiceTests
{
    [Fact]
    public async Task GetSuggestionsAsyncReturnsCachedItems()
    {
        var suggestions = new List<SearchSuggestion>
        {
            new(Guid.NewGuid(), "movie", "Batman Begins", "/fake/batman-poster.jpg"),
        };
        var service = new AutocompleteService(
            new FakeSearchRepository(suggestions),
            new FakeProviderIngestionService(),
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            new FakeCacheService(suggestions),
            NullLogger<AutocompleteService>.Instance);

        var result = await service.GetSuggestionsAsync("bat", ContentLocaleResolver.EnglishUnitedStates);

        Assert.Single(result);
        Assert.Equal("/fake/batman-poster.jpg", result[0].PosterUrl);
    }

    [Fact]
    public async Task GetSuggestionsAsyncReturnsProviderSuggestionsOnCacheMiss()
    {
        var provider = new FakeProviderIngestionService();
        var service = new AutocompleteService(
            new FakeSearchRepository([]),
            provider,
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            new FakeCacheService(null),
            NullLogger<AutocompleteService>.Instance);

        var result = await service.GetSuggestionsAsync("ava", ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(2, result.Count);
        Assert.Equal("/poster.jpg", result[0].PosterUrl);
        Assert.Equal(1, provider.AutocompleteCount);
    }

    [Fact]
    public async Task GetSuggestionsAsyncFallsBackToDatabaseWhenProviderFails()
    {
        var suggestions = new List<SearchSuggestion>
        {
            new(Guid.NewGuid(), "movie", "Avatar", "/fake/avatar-poster.jpg"),
            new(Guid.NewGuid(), "tv", "Avatar: The Last Airbender", null),
        };
        var repository = new FakeSearchRepository(suggestions);
        var service = new AutocompleteService(
            repository,
            new FakeProviderIngestionService { ThrowOnAutocomplete = true },
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            new FakeCacheService(null),
            NullLogger<AutocompleteService>.Instance);

        var result = await service.GetSuggestionsAsync("ava", ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(2, result.Count);
        Assert.Equal("/fake/avatar-poster.jpg", result[0].PosterUrl);
        Assert.Null(result[1].PosterUrl);
        Assert.Equal(1, repository.AutocompleteCount);
    }

    [Fact]
    public async Task GetSuggestionsAsyncThrowsForShortQuery()
    {
        var service = new AutocompleteService(
            new FakeSearchRepository([]),
            new FakeProviderIngestionService(),
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            new FakeCacheService(null),
            NullLogger<AutocompleteService>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() => service.GetSuggestionsAsync("a", ContentLocaleResolver.EnglishUnitedStates));
    }

    private sealed class FakeProviderIngestionService : IUnifiedSearchProviderIngestionService
    {
        public int AutocompleteCount { get; private set; }

        public bool ThrowOnAutocomplete { get; set; }

        public Task<UnifiedSearchProviderIngestionResult> IngestAsync(SearchCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(UnifiedSearchProviderIngestionResult.NotRequired());

        public Task<IReadOnlyList<SearchSuggestion>> GetAutocompleteSuggestionsAsync(string query, int limit, string contentLocale, CancellationToken cancellationToken = default)
        {
            AutocompleteCount++;

            if (ThrowOnAutocomplete)
            {
                throw new InvalidOperationException("provider unavailable");
            }

            return Task.FromResult<IReadOnlyList<SearchSuggestion>>(
            [
                new(Guid.NewGuid(), "movie", "Avatar", "/poster.jpg"),
                new(Guid.NewGuid(), "tv", "Avatar: The Last Airbender", null)
            ]);
        }
    }

    private sealed class FakeSearchRepository(IReadOnlyList<SearchSuggestion> suggestions) : ISearchRepository
    {
        public int AutocompleteCount { get; private set; }

        public Task<PaginatedResult<SearchItem>> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));

        public Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default)
        {
            AutocompleteCount++;
            return Task.FromResult(suggestions);
        }

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));

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
            Task.FromResult<IReadOnlySet<CatalogContentKey>>(new HashSet<CatalogContentKey>());

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));
    }

    private sealed class FakeCacheService(IReadOnlyList<SearchSuggestion>? suggestions) : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (suggestions is not null && typeof(T) == typeof(Application.Caching.SearchAutocompleteCacheEntry))
            {
                return Task.FromResult<T?>((T)(object)new Application.Caching.SearchAutocompleteCacheEntry
                {
                    Items = suggestions
                });
            }

            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

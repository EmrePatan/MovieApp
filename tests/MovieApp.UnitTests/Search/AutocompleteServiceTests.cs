using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class AutocompleteServiceTests
{
    [Fact]
    public async Task GetSuggestionsAsyncReturnsCachedItems()
    {
        var suggestions = new List<SearchSuggestion> { new(Guid.NewGuid(), "movie", "Batman Begins") };
        var service = new AutocompleteService(
            new FakeSearchRepository(suggestions),
            new FakeCacheService(suggestions));

        var result = await service.GetSuggestionsAsync("bat");

        Assert.Single(result);
    }

    [Fact]
    public async Task GetSuggestionsAsyncThrowsForShortQuery()
    {
        var service = new AutocompleteService(new FakeSearchRepository([]), new FakeCacheService(null));

        await Assert.ThrowsAsync<ValidationException>(() => service.GetSuggestionsAsync("a"));
    }

    private sealed class FakeSearchRepository(IReadOnlyList<SearchSuggestion> suggestions) : ISearchRepository
    {
        public Task<PaginatedResult<SearchItem>> SearchAsync(
            SearchCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));

        public Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(suggestions);

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
            string genreName,
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
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

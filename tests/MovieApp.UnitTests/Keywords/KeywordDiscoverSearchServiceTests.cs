using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Keywords;

public sealed class KeywordDiscoverSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_ReusesTheCachedPage()
    {
        var repository = new CountingKeywordRepository();
        var service = new KeywordDiscoverSearchService(
            repository,
            new DictionaryCacheService(),
            new KeywordDiscoverSearchLoadCoordinator());

        var first = await service.SearchAsync("aşk", ContentLocaleResolver.TurkishTurkey, 1, 20);
        var second = await service.SearchAsync("aşk", ContentLocaleResolver.TurkishTurkey, 1, 20);

        Assert.Equal(1, repository.SearchCount);
        Assert.Equal(first.Items[0].KeywordId, second.Items[0].KeywordId);
        Assert.Equal(first.TotalCount, second.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_ConcurrentMissesShareOneRepositoryCall()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new CountingKeywordRepository
        {
            Entered = entered,
            Release = release.Task,
        };
        var service = new KeywordDiscoverSearchService(
            repository,
            new DictionaryCacheService(),
            new KeywordDiscoverSearchLoadCoordinator());

        var first = service.SearchAsync("aşk", ContentLocaleResolver.TurkishTurkey, 1, 20);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = service.SearchAsync("aşk", ContentLocaleResolver.TurkishTurkey, 1, 20);
        release.TrySetResult();

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, repository.SearchCount);
        Assert.Equal(results[0].Items[0].KeywordId, results[1].Items[0].KeywordId);
    }

    private sealed class CountingKeywordRepository : IKeywordDiscoverReadRepository
    {
        public int SearchCount { get; private set; }

        public TaskCompletionSource? Entered { get; init; }

        public Task? Release { get; init; }

        public async Task<PaginatedResult<KeywordDiscoverItem>> SearchAsync(
            string query,
            string contentLocale,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            SearchCount++;
            Entered?.TrySetResult();
            if (Release is not null)
            {
                await Release;
            }

            return new PaginatedResult<KeywordDiscoverItem>(
                [new KeywordDiscoverItem(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), query)],
                page,
                pageSize,
                1,
                1);
        }

        public Task<IReadOnlyList<int>> ResolveTmdbKeywordIdsAsync(
            IReadOnlyList<Guid> keywordIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<int>>([]);
    }

    private sealed class DictionaryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
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
}

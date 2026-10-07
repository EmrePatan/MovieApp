using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class DiscoveryCatalogListLocalizationTests
{
    [Fact]
    public async Task CatalogListRailsSkipDetailOverlayWhileHomeRailsKeepFullOverlay()
    {
        var repository = new CountingSearchRepository();
        var overlay = new SearchTestDoubles.RecordingSummaryLocalizationOverlayService();
        var cache = new MemoryCache();
        var service = new DiscoveryService(
            repository,
            cache,
            overlay,
            new SearchTestDoubles.InMemorySearchRefreshLockService(),
            new DiscoveryCacheLoadCoordinator(),
            NullLogger<DiscoveryService>.Instance);
        var criteria = new DiscoveryCriteria(SearchContentType.Movie, 1, 10);

        await service.GetNewReleasesAsync(criteria, ContentLocaleResolver.TurkishTurkey);
        Assert.Equal(SearchListLocalizationMode.Full, overlay.LastLocalizationMode);

        await service.GetCatalogListNewReleasesAsync(criteria, ContentLocaleResolver.TurkishTurkey);
        Assert.Equal(SearchListLocalizationMode.CatalogTitlesOnly, overlay.LastLocalizationMode);
        Assert.Equal(2, repository.NewReleasesLoads);
        Assert.Equal(2, overlay.ApplyToSearchItemsCount);

        await service.GetCatalogListNewReleasesAsync(criteria, ContentLocaleResolver.TurkishTurkey);
        Assert.Equal(2, repository.NewReleasesLoads);

        await service.GetTopRatedAsync(criteria, ContentLocaleResolver.TurkishTurkey);
        Assert.Equal(SearchListLocalizationMode.Full, overlay.LastLocalizationMode);

        await service.GetCatalogListTopRatedAsync(criteria, ContentLocaleResolver.TurkishTurkey);
        Assert.Equal(SearchListLocalizationMode.CatalogTitlesOnly, overlay.LastLocalizationMode);

        var hiddenGems = DiscoverTitleRailCriteria.Create(DiscoverBrowseMode.HiddenGems, 1, 10);
        await service.GetHiddenGemsAsync(hiddenGems, ContentLocaleResolver.TurkishTurkey);
        Assert.Equal(SearchListLocalizationMode.CatalogTitlesOnly, overlay.LastLocalizationMode);

        var filteredTopRated = DiscoverTitleRailCriteria.Create(
            DiscoverBrowseMode.TopRated,
            1,
            10) with
        {
            GenreIds = [Guid.NewGuid()]
        };
        await service.GetTopRatedBrowseAsync(filteredTopRated, ContentLocaleResolver.TurkishTurkey);
        Assert.Equal(SearchListLocalizationMode.CatalogTitlesOnly, overlay.LastLocalizationMode);
        Assert.Equal(1, repository.FilteredTopRatedLoads);
    }

    private sealed class CountingSearchRepository : ISearchRepository
    {
        public int NewReleasesLoads { get; private set; }

        public int TopRatedLoads { get; private set; }

        public int HiddenGemsLoads { get; private set; }

        public int FilteredTopRatedLoads { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            NewReleasesLoads++;
            return Task.FromResult(Page());
        }

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            TopRatedLoads++;
            return Task.FromResult(Page());
        }

        public Task<PaginatedResult<SearchItem>> GetHiddenGemsAsync(
            DiscoverBrowseCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            HiddenGemsLoads++;
            return Task.FromResult(Page());
        }

        public Task<PaginatedResult<SearchItem>> GetFilteredTopRatedAsync(
            DiscoverBrowseCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            FilteredTopRatedLoads++;
            return Task.FromResult(Page());
        }

        public Task<PaginatedResult<SearchItem>> SearchAsync(
            SearchCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(
            string query,
            int limit,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<decimal> GetCatalogMeanVoteAverageAsync(
            SearchContentType type,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithGenreAsync(
            IReadOnlyList<SearchItem> items,
            Guid genreId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithAnyGenreAsync(
            IReadOnlyList<SearchItem> items,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
            string genreName,
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private static PaginatedResult<SearchItem> Page() =>
            new(
                [new SearchItem(Guid.NewGuid(), "movie", "Title", null, "Overview", null, null, null, 8m, 100, 2024, 10)],
                1,
                10,
                1,
                1);
    }

    private sealed class MemoryCache : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult(_entries.TryGetValue(key, out var value) ? value as T : null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class
        {
            _entries[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }
}

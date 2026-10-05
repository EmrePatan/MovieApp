using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.UnitTests.Localization;
using MovieApp.UnitTests.Persistence;

namespace MovieApp.UnitTests.Search;

public sealed class DiscoverBrowseDisplayTitleTests
{
    private const int SpiderManTmdbId = 55_001;

    [Fact]
    public async Task BrowseAsync_TurkishLocale_ProviderPath_UsesCanonicalTitlesBeforeEnrichment()
    {
        const string canonicalTitle = "Spider-Man";
        const string localizedMarketingTitle = "Örümcek Adam";
        var movieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var canonicalSummary = CreateMovieSummary(SpiderManTmdbId, canonicalTitle, canonicalTitle);
        var localizedSummary = CreateMovieSummary(SpiderManTmdbId, localizedMarketingTitle, canonicalTitle);

        var movieRepository = new FixedMovieRepository(
            movieId,
            new ContentProductionContext("en", "US"));
        var localizedTitles = new Dictionary<CatalogContentKey, string>
        {
            [new CatalogContentKey(movieId, "movie")] = localizedMarketingTitle
        };

        var service = new DiscoverBrowseService(
            new NoOpDiscoveryService(),
            new SinglePageMovieProvider(canonicalSummary),
            new FakeTvShowDataProvider(new TvShowDataProviderCallTracker()),
            new SinglePageLocalizedMovieProvider(localizedSummary),
            movieRepository,
            new EmptyTvShowRepository(),
            new EmptyGenreReadRepository(),
            new EmptyKeywordDiscoverReadRepository(),
            new NullCacheService(),
            NullLogger<DiscoverBrowseService>.Instance,
            new SearchItemCatalogMetadataEnricher(new EmptyGenreReadRepository()),
            new CatalogSearchItemDisplayTitleEnricher(
                new ConfiguredSummaryLocalizationMetadataReadRepository(
                    localizedTitles,
                    new Dictionary<Guid, ContentProductionContext> { [movieId] = new ContentProductionContext("en", "US") })),
            new NoOpTrendingWeekListService(),
            NoOpTrendingSnapshotService.Instance);

        var criteria = new DiscoverBrowseCriteria(
            DiscoverBrowseMode.Popular,
            SearchContentType.Movie,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            null,
            1,
            20);

        var result = await service.BrowseAsync(criteria, ContentLocaleResolver.TurkishTurkey);

        Assert.Single(result.Items);
        Assert.Equal(canonicalTitle, result.Items[0].Title);
        Assert.Equal(localizedMarketingTitle, result.Items[0].OriginalTitle);
    }

    private static MovieProviderSummary CreateMovieSummary(int tmdbId, string title, string originalTitle) =>
        new(
            $"movie-{tmdbId}",
            tmdbId,
            null,
            originalTitle,
            title,
            "Overview",
            new DateOnly(2002, 5, 3),
            "/poster.jpg",
            7.5m,
            10_000);

    private sealed class SinglePageMovieProvider(MovieProviderSummary summary) : IMovieDataProvider
    {
        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MovieProviderSearchResult(
                [summary],
                criteria.Page,
                20,
                1,
                1));

        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SinglePageLocalizedMovieProvider(MovieProviderSummary summary) : ILocalizedListDataProvider
    {
        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MovieProviderSearchResult(
                [summary],
                criteria.Page,
                20,
                1,
                1));

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PersonProviderSearchResult> SearchPersonsAsync(
            string query,
            int page,
            int pageSize,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderSearchResult> AdvancedDiscoverMoviesAsync(
            AdvancedDiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> AdvancedDiscoverTvShowsAsync(
            AdvancedDiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedMovieRepository(Guid movieId, ContentProductionContext context) : IMovieRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId == SpiderManTmdbId)
                    .ToDictionary(_ => SpiderManTmdbId, _ => movieId));

        public Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
            IReadOnlyList<int> tmdbIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, ContentProductionContext>> GetProductionContextsByIdsAsync(
            IReadOnlyList<Guid> movieIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, ContentProductionContext>>(
                movieIds.Contains(movieId)
                    ? new Dictionary<Guid, ContentProductionContext> { [movieId] = context }
                    : new Dictionary<Guid, ContentProductionContext>());
    }

    private sealed class FixedContentSearchTitleReadRepository(
        IReadOnlyDictionary<CatalogContentKey, string> titles) : IContentSearchTitleReadRepository
    {
        public Task<IReadOnlyDictionary<CatalogContentKey, string>> GetLocaleDisplayTitlesAsync(
            CatalogContentType contentType,
            IReadOnlyList<Guid> contentIds,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(titles);
    }

    private sealed class EmptyTvShowRepository : ITvShowRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());

        public Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
            IReadOnlyList<int> tmdbIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, ContentProductionContext>> GetProductionContextsByIdsAsync(
            IReadOnlyList<Guid> tvShowIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, ContentProductionContext>>(
                new Dictionary<Guid, ContentProductionContext>());
    }

    private sealed class EmptyGenreReadRepository : IGenreReadRepository
    {
        public Task<IReadOnlyList<(Guid Id, string Name)>> GetAllOrderedByNameAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(Guid Id, string Name)>>([]);

        public Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
            IReadOnlyList<Guid> genreIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

        public Task<Guid?> GetIdByNameAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByMovieIdsAsync(
            IReadOnlyList<Guid> movieIds,
            int maxGenresPerItem,
            CancellationToken cancellationToken = default) =>
            GenreReadRepositoryTestDefaults.EmptyMovieGenresAsync(movieIds, maxGenresPerItem, cancellationToken);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByTvShowIdsAsync(
            IReadOnlyList<Guid> tvShowIds,
            int maxGenresPerItem,
            CancellationToken cancellationToken = default) =>
            GenreReadRepositoryTestDefaults.EmptyTvGenresAsync(tvShowIds, maxGenresPerItem, cancellationToken);
    }

    private sealed class EmptyKeywordDiscoverReadRepository : IKeywordDiscoverReadRepository
    {
        public Task<PaginatedResult<Application.Models.Discovery.KeywordDiscoverItem>> SearchAsync(
            string query,
            string contentLocale,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<Application.Models.Discovery.KeywordDiscoverItem>(
                [], page, pageSize, 0, 0));

        public Task<IReadOnlyList<int>> ResolveTmdbKeywordIdsAsync(
            IReadOnlyList<Guid> keywordIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<int>>([]);
    }

    private sealed class NullCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult<T?>(null);

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpDiscoveryService : IDiscoveryService
    {
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
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
            string genreName,
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetHiddenGemsAsync(
            DiscoverBrowseCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTopRatedBrowseAsync(
            DiscoverBrowseCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpTrendingWeekListService : ITrendingWeekListService
    {
        public Task<PaginatedResult<SearchItem>> GetPageAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpTrendingSnapshotService : IHotThisWeekTrendingSnapshotService
    {
        public static NoOpTrendingSnapshotService Instance { get; } = new();

        public Task<HotThisWeekTrendingSnapshotEntry?> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<HotThisWeekTrendingSnapshotEntry?>(null);

        public Task<HotThisWeekTrendingSnapshotRefreshResult> RefreshAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Validation;
using MovieApp.Application.Mapping;

namespace MovieApp.Application.Services.Search;

public sealed class DiscoverBrowseService(
    IDiscoveryService discoveryService,
    IMovieDataProvider movieDataProvider,
    ITvShowDataProvider tvShowDataProvider,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    IGenreReadRepository genreReadRepository,
    IKeywordDiscoverReadRepository keywordDiscoverReadRepository,
    ICacheService cacheService,
    DiscoveryCacheLoadCoordinator loadCoordinator,
    ILogger<DiscoverBrowseService> logger,
    SearchItemCatalogMetadataEnricher searchItemCatalogMetadataEnricher,
    CatalogSearchItemDisplayTitleEnricher catalogSearchItemDisplayTitleEnricher,
    ITrendingWeekListService trendingWeekListService,
    IHotThisWeekTrendingSnapshotService trendingSnapshotService) : IDiscoverBrowseService
{
    private static readonly TimeSpan BrowseCacheTtl = TimeSpan.FromMinutes(60);

    public async Task<PaginatedResult<SearchItem>> BrowseAsync(
        DiscoverBrowseCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var validation = DiscoverBrowseValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var weeklySnapshotGeneration = await ResolveWeeklyTrendingSnapshotGenerationAsync(criteria, cancellationToken);
        var cacheKey = DiscoveryBrowseCacheKeys.Create(criteria, contentLocale, weeklySnapshotGeneration);
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        return await loadCoordinator.RunInFlightAsync(
                cacheKey,
                () => LoadBrowseAsync(criteria, contentLocale, cacheKey, cancellationToken))
            .WaitAsync(cancellationToken);
    }

    private async Task<PaginatedResult<SearchItem>> LoadBrowseAsync(
        DiscoverBrowseCriteria criteria,
        string contentLocale,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        PaginatedResult<SearchItem> result;
        var applyDisplayTitleEnrichment = false;
        var genresAlreadyEnriched = false;

        if (criteria.Mode == DiscoverBrowseMode.Trending &&
            !DiscoverBrowseValidator.HasSupplementalFilters(criteria))
        {
            result = await trendingWeekListService.GetPageAsync(
                new DiscoveryCriteria(criteria.Type, criteria.Page, criteria.PageSize),
                contentLocale,
                cancellationToken);
        }
        else if (criteria.Mode == DiscoverBrowseMode.NewReleases &&
                 !DiscoverBrowseValidator.HasSupplementalFilters(criteria))
        {
            result = await discoveryService.GetCatalogListNewReleasesAsync(
                new DiscoveryCriteria(criteria.Type, criteria.Page, criteria.PageSize),
                contentLocale,
                cancellationToken);
            genresAlreadyEnriched = true;
        }
        else if (criteria.Mode == DiscoverBrowseMode.TopRated)
        {
            result = await discoveryService.GetTopRatedBrowseAsync(
                criteria,
                contentLocale,
                cancellationToken);
            genresAlreadyEnriched = true;
        }
        else if (criteria.Mode == DiscoverBrowseMode.HiddenGems)
        {
            result = await discoveryService.GetHiddenGemsAsync(
                criteria,
                contentLocale,
                cancellationToken);
            genresAlreadyEnriched = true;
        }
        else
        {
            var providerCriteria = await BuildProviderCriteriaAsync(criteria, cancellationToken);
            if (DiscoverKeywordFilterGuard.IsUnresolvedKeywordFilter(
                    criteria.KeywordIds,
                    providerCriteria.KeywordTmdbIds))
            {
                result = DiscoverKeywordFilterGuard.CreateEmptyBrowseResult(
                    criteria.Page,
                    criteria.PageSize);
            }
            else
            {
                result = criteria.Type switch
                {
                    SearchContentType.Movie => await BrowseMoviesAsync(
                        criteria,
                        providerCriteria,
                        cancellationToken),
                    SearchContentType.Tv => await BrowseTvShowsAsync(
                        criteria,
                        providerCriteria,
                        cancellationToken),
                    _ => await BrowseAllAsync(criteria, cancellationToken)
                };
                applyDisplayTitleEnrichment = true;
            }
        }

        if (!genresAlreadyEnriched)
        {
            result = await searchItemCatalogMetadataEnricher.EnrichGenresAsync(result, cancellationToken);
        }
        if (applyDisplayTitleEnrichment)
        {
            result = await catalogSearchItemDisplayTitleEnricher.EnrichAsync(result, contentLocale, cancellationToken);
        }

        await cacheService.SetAsync(
            cacheKey,
            new DiscoveryCacheEntry { Result = result },
            BrowseCacheTtl,
            cancellationToken);

        return result;
    }

    private async Task<long> ResolveWeeklyTrendingSnapshotGenerationAsync(
        DiscoverBrowseCriteria criteria,
        CancellationToken cancellationToken)
    {
        if (criteria.Mode != DiscoverBrowseMode.Trending ||
            DiscoverBrowseValidator.HasSupplementalFilters(criteria))
        {
            return 0;
        }

        var snapshot = await trendingSnapshotService.GetSnapshotAsync(cancellationToken);
        return HotThisWeekCacheKeys.ResolveSnapshotGeneration(snapshot);
    }

    private async Task<DiscoverProviderCriteria> BuildProviderCriteriaAsync(
        DiscoverBrowseCriteria criteria,
        SearchContentType genreMappingType,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<int> genreTmdbIds = [];

        if (criteria.GenreIds.Count > 0)
        {
            var genreNamesById = await genreReadRepository.GetNamesByIdsAsync(criteria.GenreIds, cancellationToken);
            var genreNames = criteria.GenreIds
                .Where(genreNamesById.ContainsKey)
                .Select(id => genreNamesById[id])
                .ToList();

            genreTmdbIds = genreMappingType switch
            {
                SearchContentType.Tv => TmdbGenreIdMap.MapGenreNamesToTvIds(genreNames),
                _ => TmdbGenreIdMap.MapGenreNamesToMovieIds(genreNames)
            };
        }

        IReadOnlyList<int> keywordTmdbIds = [];
        if (criteria.KeywordIds.Count > 0)
        {
            keywordTmdbIds = await keywordDiscoverReadRepository.ResolveTmdbKeywordIdsAsync(
                criteria.KeywordIds,
                cancellationToken);
        }

        var providerCriteria = new DiscoverProviderCriteria(
            criteria.Mode,
            criteria.Page,
            genreTmdbIds,
            criteria.Year,
            criteria.YearFrom,
            criteria.YearTo,
            criteria.MinRating,
            criteria.MinVoteCount,
            criteria.MinRuntimeMinutes,
            criteria.MaxRuntimeMinutes,
            criteria.Language?.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(criteria.OriginCountry)
                ? null
                : criteria.OriginCountry.Trim().ToUpperInvariant(),
            keywordTmdbIds,
            criteria.TvStatuses,
            criteria.Sort);

        if (criteria.Mode == DiscoverBrowseMode.Popular)
        {
            var floor = genreMappingType == SearchContentType.Tv
                ? PopularDiscoverQuality.MinimumVoteCountTv
                : PopularDiscoverQuality.MinimumVoteCountMovie;
            var minVoteCount = providerCriteria.MinVoteCount is int requested
                ? Math.Max(requested, floor)
                : floor;
            providerCriteria = providerCriteria with { MinVoteCount = minVoteCount };
        }

        return providerCriteria;
    }

    private Task<DiscoverProviderCriteria> BuildProviderCriteriaAsync(
        DiscoverBrowseCriteria criteria,
        CancellationToken cancellationToken) =>
        BuildProviderCriteriaAsync(criteria, criteria.Type, cancellationToken);

    private async Task<PaginatedResult<SearchItem>> BrowseMoviesAsync(
        DiscoverBrowseCriteria criteria,
        DiscoverProviderCriteria providerCriteria,
        CancellationToken cancellationToken)
    {
        var items = new List<SearchItem>();
        var totalCount = 0;
        var totalPages = 0;
        var providerPage = 1;
        var requiredPrefixSize = criteria.Page * criteria.PageSize;

        while (providerPage <= 500 && items.Count < requiredPrefixSize)
        {
            var pageCriteria = providerCriteria with { Page = providerPage };
            // Canonical discover is enough: localized TMDB titles are not mapped onto cards.
            // tr-TR display titles come from the catalog title enricher after this loop.
            var searchResult = await DiscoverMoviesSafeAsync(pageCriteria, cancellationToken);

            totalCount = searchResult.TotalCount;
            totalPages = searchResult.TotalPages;
            var providerResultCount = searchResult.Results.Count;

            if (RequiresPoster(criteria.Mode))
            {
                searchResult = searchResult with
                {
                    Results = searchResult.Results
                        .Where(summary => !string.IsNullOrWhiteSpace(summary.PosterPath))
                        .ToList()
                };
            }

            var movieIds = await movieRepository.EnsureFromSummariesAsync(
                searchResult.Results,
                cancellationToken);
            items.AddRange(MapMovieResults(searchResult.Results, movieIds));

            if (providerPage >= totalPages || providerResultCount == 0)
            {
                break;
            }

            providerPage++;
        }

        return CreatePagedProviderResult(
            items,
            criteria.Page,
            criteria.PageSize,
            totalCount,
            totalPages);
    }

    private async Task<PaginatedResult<SearchItem>> BrowseTvShowsAsync(
        DiscoverBrowseCriteria criteria,
        DiscoverProviderCriteria providerCriteria,
        CancellationToken cancellationToken)
    {
        var items = new List<SearchItem>();
        var totalCount = 0;
        var totalPages = 0;
        var providerPage = 1;
        var requiredPrefixSize = criteria.Page * criteria.PageSize;

        while (providerPage <= 500 && items.Count < requiredPrefixSize)
        {
            var pageCriteria = providerCriteria with { Page = providerPage };
            var searchResult = await DiscoverTvShowsSafeAsync(pageCriteria, cancellationToken);

            totalCount = searchResult.TotalCount;
            totalPages = searchResult.TotalPages;
            var providerResultCount = searchResult.Results.Count;

            if (RequiresPoster(criteria.Mode))
            {
                searchResult = searchResult with
                {
                    Results = searchResult.Results
                        .Where(summary => !string.IsNullOrWhiteSpace(summary.PosterPath))
                        .ToList()
                };
            }

            searchResult = searchResult with
            {
                Results = DiscoverPopularTvEligibility.FilterEligible(criteria.Mode, searchResult.Results)
            };

            var tvIds = await tvShowRepository.EnsureFromSummariesAsync(
                searchResult.Results,
                cancellationToken);
            items.AddRange(MapTvResults(searchResult.Results, tvIds));

            if (providerPage >= totalPages || providerResultCount == 0)
            {
                break;
            }

            providerPage++;
        }

        return CreatePagedProviderResult(
            items,
            criteria.Page,
            criteria.PageSize,
            totalCount,
            totalPages);
    }

    private static PaginatedResult<SearchItem> CreatePagedProviderResult(
        IReadOnlyList<SearchItem> items,
        int page,
        int pageSize,
        int totalCount,
        int totalPages)
    {
        var clientTotalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);
        var skip = Math.Max(0, (page - 1) * pageSize);
        var pageItems = items
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        return new PaginatedResult<SearchItem>(
            pageItems,
            page,
            pageSize,
            totalCount,
            clientTotalPages);
    }

    private async Task<PaginatedResult<SearchItem>> BrowseAllAsync(
        DiscoverBrowseCriteria criteria,
        CancellationToken cancellationToken)
    {
        var movieProviderCriteria = await BuildProviderCriteriaAsync(
            criteria,
            SearchContentType.Movie,
            cancellationToken);
        var tvProviderCriteria = await BuildProviderCriteriaAsync(
            criteria,
            SearchContentType.Tv,
            cancellationToken);

        var movieItems = new List<SearchItem>();
        var tvItems = new List<SearchItem>();
        var movieTotalCount = 0;
        var tvTotalCount = 0;
        var providerPage = 1;
        var movieExhausted = false;
        var tvExhausted = false;
        var requiredPrefixSize = criteria.Page * criteria.PageSize;

        // Keep paging only while a side still needs items and its provider is not exhausted.
        // "Not exhausted" alone walks TMDB discover's 500-page cap, so trending type=all never returns.
        while (providerPage <= 500 &&
               ((!movieExhausted && movieItems.Count < requiredPrefixSize) ||
                (!tvExhausted && tvItems.Count < requiredPrefixSize)))
        {
            var moviePageCriteria = movieProviderCriteria with { Page = providerPage };
            var tvPageCriteria = tvProviderCriteria with { Page = providerPage };

            MovieProviderSearchResult movieSearchResult = new([], providerPage, 20, 0, 0);
            TvShowProviderSearchResult tvSearchResult = new([], providerPage, 20, 0, 0);

            if (!movieExhausted && !tvExhausted)
            {
                var movieTask = DiscoverMoviesSafeAsync(moviePageCriteria, cancellationToken);
                var tvTask = DiscoverTvShowsSafeAsync(tvPageCriteria, cancellationToken);
                await Task.WhenAll(movieTask, tvTask);

                movieSearchResult = await movieTask;
                tvSearchResult = await tvTask;
            }
            else if (!movieExhausted)
            {
                movieSearchResult = await DiscoverMoviesSafeAsync(moviePageCriteria, cancellationToken);
            }
            else if (!tvExhausted)
            {
                tvSearchResult = await DiscoverTvShowsSafeAsync(tvPageCriteria, cancellationToken);
            }

            if (!movieExhausted)
            {
                movieTotalCount = movieSearchResult.TotalCount;
                var movieIngest = FilterPosters(criteria.Mode, movieSearchResult.Results);
                var movieIds = await movieRepository.EnsureFromSummariesAsync(
                    movieIngest,
                    cancellationToken);
                movieItems.AddRange(MapMovieResults(movieIngest, movieIds));
                movieExhausted = movieSearchResult.Results.Count == 0 ||
                                 providerPage >= movieSearchResult.TotalPages;
            }

            if (!tvExhausted)
            {
                tvTotalCount = tvSearchResult.TotalCount;
                var tvIngest = FilterPosters(criteria.Mode, tvSearchResult.Results);
                tvIngest = DiscoverPopularTvEligibility.FilterEligible(criteria.Mode, tvIngest);
                var tvIds = await tvShowRepository.EnsureFromSummariesAsync(
                    tvIngest,
                    cancellationToken);
                tvItems.AddRange(MapTvResults(tvIngest, tvIds));
                tvExhausted = tvSearchResult.Results.Count == 0 ||
                              providerPage >= tvSearchResult.TotalPages;
            }

            providerPage++;
        }

        return DiscoverBrowseMerger.Merge(
            criteria,
            movieItems,
            tvItems,
            movieTotalCount,
            tvTotalCount);
    }

    private async Task<MovieProviderSearchResult> DiscoverMoviesSafeAsync(
        DiscoverProviderCriteria criteria,
        CancellationToken cancellationToken)
    {
        try
        {
            return await movieDataProvider.DiscoverMoviesAsync(criteria, cancellationToken);
        }
        catch (Exception exception) when (ProviderFailureFilter.IsProviderFailure(exception, cancellationToken))
        {
            DiscoverBrowseLogMessages.LogMovieDiscoverFailed(logger, criteria.Page, exception);
            throw new SearchProviderUnavailableException();
        }
    }

    private async Task<TvShowProviderSearchResult> DiscoverTvShowsSafeAsync(
        DiscoverProviderCriteria criteria,
        CancellationToken cancellationToken)
    {
        try
        {
            return await tvShowDataProvider.DiscoverTvShowsAsync(criteria, cancellationToken);
        }
        catch (Exception exception) when (ProviderFailureFilter.IsProviderFailure(exception, cancellationToken))
        {
            DiscoverBrowseLogMessages.LogTvDiscoverFailed(logger, criteria.Page, exception);
            throw new SearchProviderUnavailableException();
        }
    }

    private static bool RequiresPoster(DiscoverBrowseMode mode) =>
        mode is DiscoverBrowseMode.NewReleases or DiscoverBrowseMode.Popular;

    private static IReadOnlyList<T> FilterPosters<T>(DiscoverBrowseMode mode, IReadOnlyList<T> summaries)
        where T : class
    {
        if (!RequiresPoster(mode))
        {
            return summaries;
        }

        return summaries
            .Where(summary => !string.IsNullOrWhiteSpace(PosterPathOf(summary)))
            .ToList();
    }

    private static string? PosterPathOf<T>(T summary) =>
        summary switch
        {
            MovieProviderSummary movie => movie.PosterPath,
            TvShowProviderSummary tvShow => tvShow.PosterPath,
            _ => null
        };

    private static List<SearchItem> MapMovieResults(
        IReadOnlyList<MovieProviderSummary> summaries,
        IReadOnlyDictionary<int, Guid> movieIds)
    {
        var items = new List<SearchItem>();

        foreach (var summary in summaries)
        {
            if (summary.TmdbId is null ||
                !movieIds.TryGetValue(summary.TmdbId.Value, out var id))
            {
                continue;
            }

            items.Add(ProviderSearchMapper.ToSearchItem(summary, id));
        }

        return items;
    }

    private static List<SearchItem> MapTvResults(
        IReadOnlyList<TvShowProviderSummary> summaries,
        IReadOnlyDictionary<int, Guid> tvIds)
    {
        var items = new List<SearchItem>();

        foreach (var summary in summaries)
        {
            if (summary.TmdbId is null ||
                !tvIds.TryGetValue(summary.TmdbId.Value, out var id))
            {
                continue;
            }

            items.Add(ProviderSearchMapper.ToSearchItem(summary, id));
        }

        return items;
    }
}

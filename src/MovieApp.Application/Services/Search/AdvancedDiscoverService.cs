using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

public sealed class AdvancedDiscoverService(
    IMovieDataProvider movieDataProvider,
    ITvShowDataProvider tvShowDataProvider,
    ILocalizedListDataProvider localizedListDataProvider,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    IGenreReadRepository genreReadRepository,
    IKeywordDiscoverReadRepository keywordDiscoverReadRepository,
    ICacheService cacheService,
    ILogger<AdvancedDiscoverService> logger,
    SearchItemCatalogMetadataEnricher searchItemCatalogMetadataEnricher,
    CatalogSearchItemDisplayTitleEnricher catalogSearchItemDisplayTitleEnricher,
    ITransactionalStreamOfferFilter? transactionalStreamOfferFilter = null) : IAdvancedDiscoverService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public async Task<PaginatedResult<SearchItem>> DiscoverAsync(
        AdvancedDiscoverCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var validation = AdvancedDiscoverValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var cacheKey = AdvancedDiscoverCacheKeys.Create(criteria, contentLocale);
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var providerCriteria = await BuildProviderCriteriaAsync(criteria, cancellationToken);
        if (DiscoverKeywordFilterGuard.IsUnresolvedKeywordFilter(
                criteria.KeywordIds,
                providerCriteria.KeywordTmdbIds))
        {
            return DiscoverKeywordFilterGuard.CreateEmptyBrowseResult(
                criteria.Page,
                criteria.PageSize);
        }

        PaginatedResult<SearchItem> result = criteria.MediaType switch
        {
            SearchContentType.Movie => await DiscoverMoviesAsync(
                criteria,
                providerCriteria,
                contentLocale,
                cancellationToken),
            SearchContentType.Tv => await DiscoverTvShowsAsync(
                criteria,
                providerCriteria,
                contentLocale,
                cancellationToken),
            _ => throw new ValidationException("Media type must be movie or tv.")
        };

        result = await searchItemCatalogMetadataEnricher.EnrichGenresAsync(result, cancellationToken);
        result = await catalogSearchItemDisplayTitleEnricher.EnrichAsync(result, contentLocale, cancellationToken);

        await cacheService.SetAsync(
            cacheKey,
            new DiscoveryCacheEntry { Result = result },
            CacheTtl,
            cancellationToken);

        return result;
    }

    private async Task<AdvancedDiscoverProviderCriteria> BuildProviderCriteriaAsync(
        AdvancedDiscoverCriteria criteria,
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

            genreTmdbIds = criteria.MediaType switch
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

        return new AdvancedDiscoverProviderCriteria(
            criteria.Page,
            genreTmdbIds,
            criteria.GenreMatch,
            criteria.Year,
            criteria.YearFrom,
            criteria.YearTo,
            criteria.MinRating,
            criteria.MaxRating,
            criteria.MinVoteCount,
            criteria.MinRuntimeMinutes,
            criteria.MaxRuntimeMinutes,
            criteria.OriginalLanguage?.Trim().ToLowerInvariant(),
            criteria.OriginCountry?.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(criteria.Certification)
                ? null
                : criteria.Certification.Trim(),
            string.IsNullOrWhiteSpace(criteria.CertificationCountry)
                ? null
                : DiscoverMovieCertificationCatalog.NormalizeCountry(criteria.CertificationCountry),
            criteria.ReleaseTypes,
            string.IsNullOrWhiteSpace(criteria.WatchRegion)
                ? null
                : WatchProviderRegionValidator.Normalize(criteria.WatchRegion),
            criteria.WatchProviderIds,
            criteria.WatchMonetizationTypes,
            keywordTmdbIds,
            criteria.TvStatuses,
            criteria.Sort);
    }

    private async Task<PaginatedResult<SearchItem>> DiscoverMoviesAsync(
        AdvancedDiscoverCriteria criteria,
        AdvancedDiscoverProviderCriteria providerCriteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var useLocalizedDisplay = ContentLocaleResolver.RequiresLocalization(contentLocale);
        var allItems = new List<SearchItem>();
        var totalCount = 0;
        var totalPages = 0;
        var providerPage = 1;
        var requiredPrefixSize = criteria.Page * criteria.PageSize;

        while (providerPage <= 500 && allItems.Count < requiredPrefixSize)
        {
            var pageCriteria = providerCriteria with { Page = providerPage };
            MovieProviderSearchResult searchResult;
            MovieProviderSearchResult ingestResult;

            if (useLocalizedDisplay)
            {
                var localizedTask = DiscoverMoviesLocalizedSafeAsync(pageCriteria, contentLocale, cancellationToken);
                var canonicalTask = DiscoverMoviesSafeAsync(pageCriteria, cancellationToken);
                await Task.WhenAll(localizedTask, canonicalTask);
                searchResult = await localizedTask;
                ingestResult = await canonicalTask;
            }
            else
            {
                searchResult = await DiscoverMoviesSafeAsync(pageCriteria, cancellationToken);
                ingestResult = searchResult;
            }

            totalCount = searchResult.TotalCount;
            totalPages = searchResult.TotalPages;
            var providerResultCount = searchResult.Results.Count;

            var allowedMovieIds = await ResolveAllowedTmdbIdsAsync(
                criteria,
                searchResult.Results.Select(summary => summary.TmdbId)
                    .Concat(ingestResult.Results.Select(summary => summary.TmdbId)),
                cancellationToken);

            if (allowedMovieIds is not null)
            {
                searchResult = FilterMovies(searchResult, allowedMovieIds);
                ingestResult = ReferenceEquals(searchResult, ingestResult)
                    ? searchResult
                    : FilterMovies(ingestResult, allowedMovieIds);
            }

            if (criteria.RequiresPoster)
            {
                searchResult = FilterMoviesWithoutPoster(searchResult);
                ingestResult = ReferenceEquals(searchResult, ingestResult)
                    ? searchResult
                    : FilterMoviesWithoutPoster(ingestResult);
            }

            var movieIds = await movieRepository.EnsureFromSummariesAsync(
                ingestResult.Results,
                cancellationToken);
            allItems.AddRange(MapMovieResults(ingestResult.Results, movieIds));

            if (providerPage >= totalPages || providerResultCount == 0)
            {
                break;
            }

            providerPage++;
        }

        return CreateFilteredPagedResult(
            allItems,
            criteria.Page,
            criteria.PageSize,
            totalCount);
    }

    private async Task<PaginatedResult<SearchItem>> DiscoverTvShowsAsync(
        AdvancedDiscoverCriteria criteria,
        AdvancedDiscoverProviderCriteria providerCriteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var useLocalizedDisplay = ContentLocaleResolver.RequiresLocalization(contentLocale);
        var allItems = new List<SearchItem>();
        var totalCount = 0;
        var totalPages = 0;
        var providerPage = 1;
        var requiredPrefixSize = criteria.Page * criteria.PageSize;

        while (providerPage <= 500 && allItems.Count < requiredPrefixSize)
        {
            var pageCriteria = providerCriteria with { Page = providerPage };
            TvShowProviderSearchResult searchResult;
            TvShowProviderSearchResult ingestResult;

            if (useLocalizedDisplay)
            {
                var localizedTask = DiscoverTvShowsLocalizedSafeAsync(pageCriteria, contentLocale, cancellationToken);
                var canonicalTask = DiscoverTvShowsSafeAsync(pageCriteria, cancellationToken);
                await Task.WhenAll(localizedTask, canonicalTask);
                searchResult = await localizedTask;
                ingestResult = await canonicalTask;
            }
            else
            {
                searchResult = await DiscoverTvShowsSafeAsync(pageCriteria, cancellationToken);
                ingestResult = searchResult;
            }

            totalCount = searchResult.TotalCount;
            totalPages = searchResult.TotalPages;
            var providerResultCount = searchResult.Results.Count;

            var allowedTvIds = await ResolveAllowedTmdbIdsAsync(
                criteria,
                searchResult.Results.Select(summary => summary.TmdbId)
                    .Concat(ingestResult.Results.Select(summary => summary.TmdbId)),
                cancellationToken);

            if (allowedTvIds is not null)
            {
                searchResult = FilterTvShows(searchResult, allowedTvIds);
                ingestResult = ReferenceEquals(searchResult, ingestResult)
                    ? searchResult
                    : FilterTvShows(ingestResult, allowedTvIds);
            }

            if (criteria.RequiresPoster)
            {
                searchResult = FilterTvShowsWithoutPoster(searchResult);
                ingestResult = ReferenceEquals(searchResult, ingestResult)
                    ? searchResult
                    : FilterTvShowsWithoutPoster(ingestResult);
            }

            var tvIds = await tvShowRepository.EnsureFromSummariesAsync(
                ingestResult.Results,
                cancellationToken);
            allItems.AddRange(MapTvResults(ingestResult.Results, tvIds));

            if (providerPage >= totalPages || providerResultCount == 0)
            {
                break;
            }

            providerPage++;
        }

        return CreateFilteredPagedResult(
            allItems,
            criteria.Page,
            criteria.PageSize,
            totalCount);
    }

    private static PaginatedResult<SearchItem> CreateFilteredPagedResult(
        IReadOnlyList<SearchItem> items,
        int page,
        int pageSize,
        int providerTotalCount)
    {
        var clientTotalPages = providerTotalCount == 0
            ? 0
            : (int)Math.Ceiling(providerTotalCount / (double)pageSize);
        var skip = Math.Max(0, (page - 1) * pageSize);
        var pageItems = items
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        // The transactional offer filter is evaluated against the fetched prefix only.
        // Keep TMDB's total as the pagination upper bound instead of subtracting drops
        // from a single provider page, which made later valid results unreachable.
        return new PaginatedResult<SearchItem>(
            pageItems,
            page,
            pageSize,
            providerTotalCount,
            clientTotalPages);
    }

    private async Task<MovieProviderSearchResult> DiscoverMoviesSafeAsync(
        AdvancedDiscoverProviderCriteria criteria,
        CancellationToken cancellationToken)
    {
        try
        {
            return await movieDataProvider.AdvancedDiscoverMoviesAsync(criteria, cancellationToken);
        }
        catch (Exception exception) when (ProviderFailureFilter.IsProviderFailure(exception, cancellationToken))
        {
            DiscoverBrowseLogMessages.LogMovieDiscoverFailed(logger, criteria.Page, exception);
            throw new SearchProviderUnavailableException();
        }
    }

    private async Task<TvShowProviderSearchResult> DiscoverTvShowsSafeAsync(
        AdvancedDiscoverProviderCriteria criteria,
        CancellationToken cancellationToken)
    {
        try
        {
            return await tvShowDataProvider.AdvancedDiscoverTvShowsAsync(criteria, cancellationToken);
        }
        catch (Exception exception) when (ProviderFailureFilter.IsProviderFailure(exception, cancellationToken))
        {
            DiscoverBrowseLogMessages.LogTvDiscoverFailed(logger, criteria.Page, exception);
            throw new SearchProviderUnavailableException();
        }
    }

    private async Task<MovieProviderSearchResult> DiscoverMoviesLocalizedSafeAsync(
        AdvancedDiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        try
        {
            return await localizedListDataProvider.AdvancedDiscoverMoviesAsync(
                criteria,
                contentLocale,
                cancellationToken);
        }
        catch (Exception exception) when (ProviderFailureFilter.IsProviderFailure(exception, cancellationToken))
        {
            DiscoverBrowseLogMessages.LogMovieDiscoverFailed(logger, criteria.Page, exception);
            throw new SearchProviderUnavailableException();
        }
    }

    private async Task<TvShowProviderSearchResult> DiscoverTvShowsLocalizedSafeAsync(
        AdvancedDiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        try
        {
            return await localizedListDataProvider.AdvancedDiscoverTvShowsAsync(
                criteria,
                contentLocale,
                cancellationToken);
        }
        catch (Exception exception) when (ProviderFailureFilter.IsProviderFailure(exception, cancellationToken))
        {
            DiscoverBrowseLogMessages.LogTvDiscoverFailed(logger, criteria.Page, exception);
            throw new SearchProviderUnavailableException();
        }
    }

    private async Task<IReadOnlySet<int>?> ResolveAllowedTmdbIdsAsync(
        AdvancedDiscoverCriteria criteria,
        IEnumerable<int?> tmdbIds,
        CancellationToken cancellationToken)
    {
        if (transactionalStreamOfferFilter is null)
        {
            return null;
        }

        var distinctIds = tmdbIds
            .Where(tmdbId => tmdbId.HasValue)
            .Select(tmdbId => tmdbId!.Value)
            .Distinct()
            .ToList();

        try
        {
            return await transactionalStreamOfferFilter.SelectMatchingTmdbIdsAsync(
                criteria.MediaType,
                criteria.WatchRegion,
                criteria.WatchProviderIds,
                criteria.WatchMonetizationTypes,
                distinctIds,
                cancellationToken);
        }
        catch (Exception exception) when (ProviderFailureFilter.IsProviderFailure(exception, cancellationToken))
        {
            throw new SearchProviderUnavailableException();
        }
    }

    private static MovieProviderSearchResult FilterMoviesWithoutPoster(MovieProviderSearchResult result)
    {
        var filtered = result.Results
            .Where(summary => !string.IsNullOrWhiteSpace(summary.PosterPath))
            .ToList();
        if (filtered.Count == result.Results.Count)
        {
            return result;
        }

        return result with { Results = filtered };
    }

    private static TvShowProviderSearchResult FilterTvShowsWithoutPoster(TvShowProviderSearchResult result)
    {
        var filtered = result.Results
            .Where(summary => !string.IsNullOrWhiteSpace(summary.PosterPath))
            .ToList();
        if (filtered.Count == result.Results.Count)
        {
            return result;
        }

        return result with { Results = filtered };
    }

    private static MovieProviderSearchResult FilterMovies(
        MovieProviderSearchResult result,
        IReadOnlySet<int> allowedTmdbIds)
    {
        var filtered = result.Results
            .Where(summary => summary.TmdbId is int tmdbId && allowedTmdbIds.Contains(tmdbId))
            .ToList();
        if (filtered.Count == result.Results.Count)
        {
            return result;
        }

        var dropped = result.Results.Count - filtered.Count;
        var totalCount = Math.Max(0, result.TotalCount - dropped);
        var pageSize = Math.Max(1, result.PageSize);
        return result with
        {
            Results = filtered,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    private static TvShowProviderSearchResult FilterTvShows(
        TvShowProviderSearchResult result,
        IReadOnlySet<int> allowedTmdbIds)
    {
        var filtered = result.Results
            .Where(summary => summary.TmdbId is int tmdbId && allowedTmdbIds.Contains(tmdbId))
            .ToList();
        if (filtered.Count == result.Results.Count)
        {
            return result;
        }

        var dropped = result.Results.Count - filtered.Count;
        var totalCount = Math.Max(0, result.TotalCount - dropped);
        var pageSize = Math.Max(1, result.PageSize);
        return result with
        {
            Results = filtered,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

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

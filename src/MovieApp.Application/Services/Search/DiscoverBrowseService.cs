using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Validation;
using MovieApp.Application.Mapping;

namespace MovieApp.Application.Services.Search;

public sealed class DiscoverBrowseService(
    IDiscoveryService discoveryService,
    IMovieDataProvider movieDataProvider,
    ITvShowDataProvider tvShowDataProvider,
    ILocalizedListDataProvider localizedListDataProvider,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    IGenreReadRepository genreReadRepository,
    ICacheService cacheService,
    ILogger<DiscoverBrowseService> logger) : IDiscoverBrowseService
{
    private static readonly TimeSpan BrowseCacheTtl = TimeSpan.FromMinutes(10);

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

        var cacheKey = DiscoveryBrowseCacheKeys.Create(criteria, contentLocale);
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        PaginatedResult<SearchItem> result;

        if (criteria.Mode == DiscoverBrowseMode.NewReleases && !HasSupplementalBrowseFilters(criteria))
        {
            result = await discoveryService.GetNewReleasesAsync(
                new DiscoveryCriteria(criteria.Type, criteria.Page, criteria.PageSize),
                contentLocale,
                cancellationToken);
        }
        else
        {
            var providerCriteria = await BuildProviderCriteriaAsync(criteria, cancellationToken);

            result = criteria.Type switch
            {
                SearchContentType.Movie => await BrowseMoviesAsync(
                    criteria,
                    providerCriteria,
                    contentLocale,
                    cancellationToken),
                SearchContentType.Tv => await BrowseTvShowsAsync(
                    criteria,
                    providerCriteria,
                    contentLocale,
                    cancellationToken),
                _ => await BrowseAllAsync(criteria, contentLocale, cancellationToken)
            };
        }

        await cacheService.SetAsync(
            cacheKey,
            new DiscoveryCacheEntry { Result = result },
            BrowseCacheTtl,
            cancellationToken);

        return result;
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

        return new DiscoverProviderCriteria(
            criteria.Mode,
            criteria.Page,
            genreTmdbIds,
            criteria.Year,
            criteria.MinRating,
            criteria.Language?.Trim().ToLowerInvariant(),
            criteria.Sort);
    }

    private Task<DiscoverProviderCriteria> BuildProviderCriteriaAsync(
        DiscoverBrowseCriteria criteria,
        CancellationToken cancellationToken) =>
        BuildProviderCriteriaAsync(criteria, criteria.Type, cancellationToken);

    private async Task<PaginatedResult<SearchItem>> BrowseMoviesAsync(
        DiscoverBrowseCriteria criteria,
        DiscoverProviderCriteria providerCriteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var useLocalizedDisplay = ContentLocaleResolver.RequiresLocalization(contentLocale);
        MovieProviderSearchResult searchResult;
        MovieProviderSearchResult ingestResult;

        if (useLocalizedDisplay)
        {
            var localizedTask = DiscoverMoviesLocalizedSafeAsync(providerCriteria, contentLocale, cancellationToken);
            var canonicalTask = DiscoverMoviesSafeAsync(providerCriteria, cancellationToken);
            await Task.WhenAll(localizedTask, canonicalTask);
            searchResult = await localizedTask;
            ingestResult = await canonicalTask;
        }
        else
        {
            searchResult = await DiscoverMoviesSafeAsync(providerCriteria, cancellationToken);
            ingestResult = searchResult;
        }

        var movieIds = await movieRepository.EnsureFromSummariesAsync(ingestResult.Results, cancellationToken);
        var items = MapMovieResults(searchResult.Results, movieIds);

        return DiscoverBrowseMerger.CreateSingleTypeResult(
            items,
            criteria.Page,
            criteria.PageSize,
            searchResult.TotalCount);
    }

    private async Task<PaginatedResult<SearchItem>> BrowseTvShowsAsync(
        DiscoverBrowseCriteria criteria,
        DiscoverProviderCriteria providerCriteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var useLocalizedDisplay = ContentLocaleResolver.RequiresLocalization(contentLocale);
        TvShowProviderSearchResult searchResult;
        TvShowProviderSearchResult ingestResult;

        if (useLocalizedDisplay)
        {
            var localizedTask = DiscoverTvShowsLocalizedSafeAsync(providerCriteria, contentLocale, cancellationToken);
            var canonicalTask = DiscoverTvShowsSafeAsync(providerCriteria, cancellationToken);
            await Task.WhenAll(localizedTask, canonicalTask);
            searchResult = await localizedTask;
            ingestResult = await canonicalTask;
        }
        else
        {
            searchResult = await DiscoverTvShowsSafeAsync(providerCriteria, cancellationToken);
            ingestResult = searchResult;
        }

        var tvIds = await tvShowRepository.EnsureFromSummariesAsync(ingestResult.Results, cancellationToken);
        var items = MapTvResults(searchResult.Results, tvIds);

        return DiscoverBrowseMerger.CreateSingleTypeResult(
            items,
            criteria.Page,
            criteria.PageSize,
            searchResult.TotalCount);
    }

    private async Task<PaginatedResult<SearchItem>> BrowseAllAsync(
        DiscoverBrowseCriteria criteria,
        string contentLocale,
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

        var useLocalizedDisplay = ContentLocaleResolver.RequiresLocalization(contentLocale);
        MovieProviderSearchResult movieSearchResult;
        TvShowProviderSearchResult tvSearchResult;
        MovieProviderSearchResult movieIngestResult;
        TvShowProviderSearchResult tvIngestResult;

        if (useLocalizedDisplay)
        {
            var movieLocalizedTask = DiscoverMoviesLocalizedSafeAsync(
                movieProviderCriteria,
                contentLocale,
                cancellationToken);
            var tvLocalizedTask = DiscoverTvShowsLocalizedSafeAsync(
                tvProviderCriteria,
                contentLocale,
                cancellationToken);
            var movieCanonicalTask = DiscoverMoviesSafeAsync(movieProviderCriteria, cancellationToken);
            var tvCanonicalTask = DiscoverTvShowsSafeAsync(tvProviderCriteria, cancellationToken);
            await Task.WhenAll(movieLocalizedTask, tvLocalizedTask, movieCanonicalTask, tvCanonicalTask);
            movieSearchResult = await movieLocalizedTask;
            tvSearchResult = await tvLocalizedTask;
            movieIngestResult = await movieCanonicalTask;
            tvIngestResult = await tvCanonicalTask;
        }
        else
        {
            var movieTask = DiscoverMoviesSafeAsync(movieProviderCriteria, cancellationToken);
            var tvTask = DiscoverTvShowsSafeAsync(tvProviderCriteria, cancellationToken);
            await Task.WhenAll(movieTask, tvTask);
            movieSearchResult = await movieTask;
            tvSearchResult = await tvTask;
            movieIngestResult = movieSearchResult;
            tvIngestResult = tvSearchResult;
        }

        var movieIds = await movieRepository.EnsureFromSummariesAsync(
            movieIngestResult.Results,
            cancellationToken);
        var tvIds = await tvShowRepository.EnsureFromSummariesAsync(
            tvIngestResult.Results,
            cancellationToken);

        var movieItems = MapMovieResults(movieSearchResult.Results, movieIds);
        var tvItems = MapTvResults(tvSearchResult.Results, tvIds);

        return DiscoverBrowseMerger.Merge(
            criteria,
            movieItems,
            tvItems,
            movieSearchResult.TotalCount,
            tvSearchResult.TotalCount);
    }

    private async Task<MovieProviderSearchResult> DiscoverMoviesSafeAsync(
        DiscoverProviderCriteria criteria,
        CancellationToken cancellationToken)
    {
        try
        {
            return await movieDataProvider.DiscoverMoviesAsync(criteria, cancellationToken);
        }
        catch (Exception exception)
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
        catch (Exception exception)
        {
            DiscoverBrowseLogMessages.LogTvDiscoverFailed(logger, criteria.Page, exception);
            throw new SearchProviderUnavailableException();
        }
    }

    private async Task<MovieProviderSearchResult> DiscoverMoviesLocalizedSafeAsync(
        DiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        try
        {
            return await localizedListDataProvider.DiscoverMoviesAsync(criteria, contentLocale, cancellationToken);
        }
        catch (Exception exception)
        {
            DiscoverBrowseLogMessages.LogMovieDiscoverFailed(logger, criteria.Page, exception);
            throw new SearchProviderUnavailableException();
        }
    }

    private async Task<TvShowProviderSearchResult> DiscoverTvShowsLocalizedSafeAsync(
        DiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        try
        {
            return await localizedListDataProvider.DiscoverTvShowsAsync(criteria, contentLocale, cancellationToken);
        }
        catch (Exception exception)
        {
            DiscoverBrowseLogMessages.LogTvDiscoverFailed(logger, criteria.Page, exception);
            throw new SearchProviderUnavailableException();
        }
    }

    private static bool HasSupplementalBrowseFilters(DiscoverBrowseCriteria criteria) =>
        criteria.GenreIds.Count > 0 ||
        criteria.Year.HasValue ||
        criteria.MinRating.HasValue ||
        !string.IsNullOrWhiteSpace(criteria.Language) ||
        (criteria.Sort.HasValue &&
         criteria.Sort != DiscoverBrowseValidator.GetDefaultSortForMode(criteria.Mode));

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

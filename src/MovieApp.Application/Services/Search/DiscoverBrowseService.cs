using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;
using MovieApp.Application.Mapping;

namespace MovieApp.Application.Services.Search;

public sealed class DiscoverBrowseService(
    IMovieDataProvider movieDataProvider,
    ITvShowDataProvider tvShowDataProvider,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    IGenreReadRepository genreReadRepository,
    ICacheService cacheService,
    ILogger<DiscoverBrowseService> logger) : IDiscoverBrowseService
{
    private static readonly TimeSpan BrowseCacheTtl = TimeSpan.FromMinutes(10);

    public async Task<PaginatedResult<SearchItem>> BrowseAsync(
        DiscoverBrowseCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var validation = DiscoverBrowseValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var cacheKey = DiscoveryBrowseCacheKeys.Create(criteria);
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var providerCriteria = await BuildProviderCriteriaAsync(criteria, cancellationToken);

        PaginatedResult<SearchItem> result = criteria.Type switch
        {
            SearchContentType.Movie => await BrowseMoviesAsync(criteria, providerCriteria, cancellationToken),
            SearchContentType.Tv => await BrowseTvShowsAsync(criteria, providerCriteria, cancellationToken),
            _ => await BrowseAllAsync(criteria, cancellationToken)
        };

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
        CancellationToken cancellationToken)
    {
        var searchResult = await DiscoverMoviesSafeAsync(providerCriteria, cancellationToken);
        var movieIds = await movieRepository.EnsureFromSummariesAsync(searchResult.Results, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var searchResult = await DiscoverTvShowsSafeAsync(providerCriteria, cancellationToken);
        var tvIds = await tvShowRepository.EnsureFromSummariesAsync(searchResult.Results, cancellationToken);
        var items = MapTvResults(searchResult.Results, tvIds);

        return DiscoverBrowseMerger.CreateSingleTypeResult(
            items,
            criteria.Page,
            criteria.PageSize,
            searchResult.TotalCount);
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

        var movieTask = DiscoverMoviesSafeAsync(movieProviderCriteria, cancellationToken);
        var tvTask = DiscoverTvShowsSafeAsync(tvProviderCriteria, cancellationToken);
        await Task.WhenAll(movieTask, tvTask);

        var movieSearchResult = await movieTask;
        var tvSearchResult = await tvTask;

        var movieIds = await movieRepository.EnsureFromSummariesAsync(
            movieSearchResult.Results,
            cancellationToken);
        var tvIds = await tvShowRepository.EnsureFromSummariesAsync(
            tvSearchResult.Results,
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

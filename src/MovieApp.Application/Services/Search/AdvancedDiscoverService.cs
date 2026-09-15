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
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

public sealed class AdvancedDiscoverService(
    IMovieDataProvider movieDataProvider,
    ITvShowDataProvider tvShowDataProvider,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    IGenreReadRepository genreReadRepository,
    ICacheService cacheService,
    ILogger<AdvancedDiscoverService> logger) : IAdvancedDiscoverService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public async Task<PaginatedResult<SearchItem>> DiscoverAsync(
        AdvancedDiscoverCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var validation = AdvancedDiscoverValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var cacheKey = AdvancedDiscoverCacheKeys.Create(criteria);
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var providerCriteria = await BuildProviderCriteriaAsync(criteria, cancellationToken);

        PaginatedResult<SearchItem> result = criteria.MediaType switch
        {
            SearchContentType.Movie => await DiscoverMoviesAsync(criteria, providerCriteria, cancellationToken),
            SearchContentType.Tv => await DiscoverTvShowsAsync(criteria, providerCriteria, cancellationToken),
            _ => throw new ValidationException("Media type must be movie or tv.")
        };

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

        return new AdvancedDiscoverProviderCriteria(
            criteria.Page,
            genreTmdbIds,
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
            criteria.Sort);
    }

    private async Task<PaginatedResult<SearchItem>> DiscoverMoviesAsync(
        AdvancedDiscoverCriteria criteria,
        AdvancedDiscoverProviderCriteria providerCriteria,
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

    private async Task<PaginatedResult<SearchItem>> DiscoverTvShowsAsync(
        AdvancedDiscoverCriteria criteria,
        AdvancedDiscoverProviderCriteria providerCriteria,
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

    private async Task<MovieProviderSearchResult> DiscoverMoviesSafeAsync(
        AdvancedDiscoverProviderCriteria criteria,
        CancellationToken cancellationToken)
    {
        try
        {
            return await movieDataProvider.AdvancedDiscoverMoviesAsync(criteria, cancellationToken);
        }
        catch (Exception exception)
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

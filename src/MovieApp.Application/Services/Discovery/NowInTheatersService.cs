using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Discovery;

public sealed class NowInTheatersService(
    INowInTheatersMovieCatalog nowInTheatersMovieCatalog,
    IMovieRepository movieRepository,
    ICacheService cacheService,
    ISummaryLocalizationOverlayService summaryLocalizationOverlayService,
    IOptions<ReleaseRegionOptions> releaseRegionOptions,
    ILogger<NowInTheatersService> logger) : INowInTheatersService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    public async Task<PaginatedResult<SearchItem>> GetNowInTheatersAsync(
        NowInTheatersCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var normalizedCriteria = NormalizeCriteria(criteria);
        var validation = NowInTheatersValidator.Validate(normalizedCriteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var cacheKey = NowInTheatersCacheKeys.Create(normalizedCriteria, contentLocale);
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        MovieProviderSearchResult searchResult;
        try
        {
            searchResult = await nowInTheatersMovieCatalog.GetNowPlayingMoviesAsync(
                normalizedCriteria.ReleaseRegion,
                normalizedCriteria.Page,
                cancellationToken);
        }
        catch (Exception exception) when (ProviderFailureFilter.IsProviderFailure(exception, cancellationToken))
        {
            NowInTheatersLogMessages.LogProviderFailed(
                logger,
                normalizedCriteria.ReleaseRegion,
                normalizedCriteria.Page,
                exception);
            throw new SearchProviderUnavailableException();
        }

        var movieIds = await movieRepository.EnsureFromSummariesAsync(searchResult.Results, cancellationToken);
        var items = MapMovieResults(searchResult.Results, movieIds);

        var canonical = DiscoverBrowseMerger.CreateSingleTypeResult(
            items,
            normalizedCriteria.Page,
            normalizedCriteria.PageSize,
            searchResult.TotalCount);
        var result = await summaryLocalizationOverlayService.ApplyToSearchItemsAsync(
            canonical,
            contentLocale,
            cancellationToken);

        await cacheService.SetAsync(
            cacheKey,
            new DiscoveryCacheEntry { Result = result },
            CacheTtl,
            cancellationToken);

        return result;
    }

    private NowInTheatersCriteria NormalizeCriteria(NowInTheatersCriteria criteria)
    {
        var releaseRegion = string.IsNullOrWhiteSpace(criteria.ReleaseRegion)
            ? WatchProviderRegionValidator.Normalize(releaseRegionOptions.Value.DefaultRegion)
            : WatchProviderRegionValidator.Normalize(criteria.ReleaseRegion);

        return criteria with { ReleaseRegion = releaseRegion };
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
}

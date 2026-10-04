using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

public sealed class TrendingWeekListService(
    ITrendingWeekDataProvider trendingWeekDataProvider,
    IHotThisWeekTrendingSnapshotService trendingSnapshotService,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    ICacheService cacheService,
    ISummaryLocalizationOverlayService summaryLocalizationOverlayService) : ITrendingWeekListService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<PaginatedResult<SearchItem>> GetPageAsync(
        DiscoveryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var validation = AdvancedSearchValidator.ValidatePagination(criteria.Page, criteria.PageSize);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var snapshot = await trendingSnapshotService.GetSnapshotAsync(cancellationToken);
        var cacheKey = DiscoveryTrendingCacheKeys.CreateWeekList(criteria, contentLocale, snapshot);
        var today = TrendingWeekReleaseEligibility.TodayUtc();
        var cached = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return ReapplyReleaseEligibility(cached.Result, today);
        }

        PaginatedResult<SearchItem> result;
        if (snapshot is { Items.Count: > 0 })
        {
            result = await BuildFromSnapshotAsync(snapshot, criteria, contentLocale, today, cancellationToken);
        }
        else
        {
            result = await BuildFromProviderAsync(criteria, contentLocale, today, cancellationToken);
        }

        await cacheService.SetAsync(
            cacheKey,
            new DiscoveryCacheEntry { Result = result },
            CacheTtl,
            cancellationToken);

        return result;
    }

    private async Task<PaginatedResult<SearchItem>> BuildFromSnapshotAsync(
        HotThisWeekTrendingSnapshotEntry snapshot,
        DiscoveryCriteria criteria,
        string contentLocale,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var released = TrendingWeekReleaseEligibility.FilterReleased(snapshot.Items, today);
        var filtered = HotThisWeekService.FilterAndTake(released, criteria.Type, released.Count);
        var totalCount = filtered.Count;
        var skip = Math.Max(0, (criteria.Page - 1) * criteria.PageSize);
        var pageItems = filtered.Skip(skip).Take(criteria.PageSize).ToList();
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)criteria.PageSize);

        var canonical = new PaginatedResult<SearchItem>(
            pageItems,
            criteria.Page,
            criteria.PageSize,
            totalCount,
            totalPages);

        return await summaryLocalizationOverlayService.ApplyToSearchItemsAsync(
            canonical,
            contentLocale,
            cancellationToken);
    }

    private async Task<PaginatedResult<SearchItem>> BuildFromProviderAsync(
        DiscoveryCriteria criteria,
        string contentLocale,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        TrendingWeekPage providerPage;
        try
        {
            providerPage = await trendingWeekDataProvider.GetTrendingWeekPageAsync(
                criteria.Page,
                cancellationToken);
        }
        catch (Exception exception) when (ProviderFailureFilter.IsProviderFailure(exception, cancellationToken))
        {
            throw new SearchProviderUnavailableException();
        }

        var titles = TrendingWeekReleaseEligibility.FilterReleased(
            providerPage.Items
                .Where(item => item.MediaType is "movie" or "tv")
                .Where(item => MatchesType(item.MediaType, criteria.Type))
                .ToList(),
            today);

        var movieSummaries = titles
            .Where(item => item.MediaType == "movie")
            .Select(TrendingWeekCatalogMapper.ToMovieSummary)
            .ToList();
        var tvSummaries = titles
            .Where(item => item.MediaType == "tv")
            .Select(TrendingWeekCatalogMapper.ToTvSummary)
            .ToList();

        var movieIds = await movieRepository.EnsureFromSummariesAsync(movieSummaries, cancellationToken);
        var tvIds = await tvShowRepository.EnsureFromSummariesAsync(tvSummaries, cancellationToken);
        var ordered = TrendingWeekCatalogMapper.MapOrderedItems(titles, movieIds, tvIds);
        var pageItems = ordered.Take(criteria.PageSize).ToList();

        var canonical = new PaginatedResult<SearchItem>(
            pageItems,
            criteria.Page,
            criteria.PageSize,
            Math.Max(0, providerPage.TotalResults),
            Math.Max(0, providerPage.TotalPages));

        return await summaryLocalizationOverlayService.ApplyToSearchItemsAsync(
            canonical,
            contentLocale,
            cancellationToken);
    }

    private static bool MatchesType(string mediaType, SearchContentType type) =>
        type switch
        {
            SearchContentType.Movie => mediaType == "movie",
            SearchContentType.Tv => mediaType == "tv",
            _ => mediaType is "movie" or "tv"
        };

    private static PaginatedResult<SearchItem> ReapplyReleaseEligibility(
        PaginatedResult<SearchItem> cached,
        DateOnly today)
    {
        var items = TrendingWeekReleaseEligibility.FilterReleased(cached.Items, today);
        return new PaginatedResult<SearchItem>(
            items,
            cached.Page,
            cached.PageSize,
            cached.TotalCount,
            cached.TotalPages);
    }
}

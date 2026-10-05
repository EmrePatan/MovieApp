using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.Catalog;

public sealed class DetailKeywordOverlayService(
    ICatalogTitleKeywordReadRepository keywordReadRepository,
    ICacheService cacheService) : IDetailKeywordOverlayService
{
    private static readonly TimeSpan DetailKeywordCacheTtl = TimeSpan.FromMinutes(15);

    public Task<MovieDetailsResult> ApplyMovieKeywordsAsync(
        Guid movieId,
        MovieDetailsResult result,
        string? contentLocale,
        CancellationToken cancellationToken = default)
    {
        var locale = DetailKeywordCacheKeys.ResolveContentLocale(contentLocale);
        var cacheKey = DetailKeywordCacheKeys.Movie(movieId, contentLocale);

        return ApplyKeywordsAsync(
            cacheKey,
            () => keywordReadRepository.GetLocalizedKeywordsForMovieAsync(
                movieId,
                locale,
                CatalogDisplayLimits.MaxDetailKeywords,
                cancellationToken),
            keywords => result with { Keywords = keywords },
            cancellationToken);
    }

    public Task<TvShowDetailsResult> ApplyTvShowKeywordsAsync(
        Guid tvShowId,
        TvShowDetailsResult result,
        string? contentLocale,
        CancellationToken cancellationToken = default)
    {
        var locale = DetailKeywordCacheKeys.ResolveContentLocale(contentLocale);
        var cacheKey = DetailKeywordCacheKeys.TvShow(tvShowId, contentLocale);

        return ApplyKeywordsAsync(
            cacheKey,
            () => keywordReadRepository.GetLocalizedKeywordsForTvShowAsync(
                tvShowId,
                locale,
                CatalogDisplayLimits.MaxDetailKeywords,
                cancellationToken),
            keywords => result with { Keywords = keywords },
            cancellationToken);
    }

    private async Task<T> ApplyKeywordsAsync<T>(
        string cacheKey,
        Func<Task<IReadOnlyList<CatalogKeywordSummary>>> fetchKeywords,
        Func<IReadOnlyList<CatalogKeywordSummary>, T> apply,
        CancellationToken cancellationToken)
    {
        var keywords = await GetOrLoadKeywordsAsync(cacheKey, fetchKeywords, cancellationToken);
        return apply(keywords);
    }

    private async Task<IReadOnlyList<CatalogKeywordSummary>> GetOrLoadKeywordsAsync(
        string cacheKey,
        Func<Task<IReadOnlyList<CatalogKeywordSummary>>> fetchKeywords,
        CancellationToken cancellationToken)
    {
        var cached = await cacheService.GetAsync<DetailKeywordCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            LocalizationOverlayPerfAmbient.RecordDetailKeywordCacheHit();
            return cached.Keywords;
        }

        LocalizationOverlayPerfAmbient.RecordDetailKeywordCacheMiss();

        var keywords = await fetchKeywords();
        await cacheService.SetAsync(
            cacheKey,
            new DetailKeywordCacheEntry { Keywords = keywords },
            DetailKeywordCacheTtl,
            cancellationToken);

        return keywords;
    }
}

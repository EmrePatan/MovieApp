using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Services.TvShows;

public sealed class GetTvShowByIdService(
    ITvShowSeasonSummaryHydrator seasonSummaryHydrator,
    ICacheService cacheService) : IGetTvShowByIdService
{
    private static readonly TimeSpan DetailsCacheTtl = TimeSpan.FromMinutes(15);

    public async Task<TvShowDetailsResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = TvShowDetailsCacheKeys.Create(id);
        var cachedEntry = await cacheService.GetAsync<TvShowDetailsCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null && cachedEntry.Result.Seasons.Count > 0)
        {
            return cachedEntry.Result;
        }

        var tvShow = await seasonSummaryHydrator.EnsureSeasonSummariesAsync(id, cancellationToken);
        var result = TvShowMapper.ToDetailsResult(tvShow);

        await cacheService.SetAsync(
            cacheKey,
            new TvShowDetailsCacheEntry { Result = result },
            DetailsCacheTtl,
            cancellationToken);

        return result;
    }
}

using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Videos;
using MovieApp.Application.Services.Videos;

namespace MovieApp.Application.Services.TvShows;

public sealed class GetTvShowVideosService(
    ITvShowRepository tvShowRepository,
    IVideoProvider videoProvider,
    ICacheService cacheService) : IGetTvShowVideosService
{
    private static readonly TimeSpan VideosCacheTtl = TimeSpan.FromHours(24);

    public async Task<VideosResult> GetVideosAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = TvShowVideosCacheKeys.Create(tvShowId);
        var cached = await cacheService.GetAsync<VideosCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        var lookup = await tvShowRepository.GetProviderLookupByIdAsync(tvShowId, cancellationToken);
        if (lookup is null)
        {
            throw new NotFoundException($"TV show with id '{tvShowId}' was not found.");
        }

        if (lookup.TmdbId is null)
        {
            return new VideosResult(null);
        }

        var videos = await videoProvider.GetTvShowVideosAsync(lookup.TmdbId.Value, cancellationToken);
        var result = new VideosResult(TrailerSelectionService.SelectPrimary(videos, lookup.OriginalLanguage));

        await cacheService.SetAsync(
            cacheKey,
            new VideosCacheEntry { Result = result },
            VideosCacheTtl,
            cancellationToken);

        return result;
    }
}

using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Videos;
using MovieApp.Application.Services.Videos;

namespace MovieApp.Application.Services.Movies;

public sealed class GetMovieVideosService(
    IMovieRepository movieRepository,
    IVideoProvider videoProvider,
    ICacheService cacheService) : IGetMovieVideosService
{
    private static readonly TimeSpan VideosCacheTtl = TimeSpan.FromHours(24);

    public async Task<VideosResult> GetVideosAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var movie = await movieRepository.GetByIdAsync(movieId, cancellationToken);
        if (movie is null)
        {
            throw new NotFoundException($"Movie with id '{movieId}' was not found.");
        }

        if (movie.TmdbId is null)
        {
            return new VideosResult(null);
        }

        var cacheKey = MovieVideosCacheKeys.Create(movieId);
        var cached = await cacheService.GetAsync<VideosCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        var videos = await videoProvider.GetMovieVideosAsync(movie.TmdbId.Value, cancellationToken);
        var result = new VideosResult(TrailerSelectionService.SelectPrimary(videos, movie.OriginalLanguage));

        await cacheService.SetAsync(
            cacheKey,
            new VideosCacheEntry { Result = result },
            VideosCacheTtl,
            cancellationToken);

        return result;
    }
}

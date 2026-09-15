using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Images;
using MovieApp.Application.Services.Images;

namespace MovieApp.Application.Services.Movies;

public sealed class GetMovieImagesService(
    IMovieRepository movieRepository,
    IImageProvider imageProvider,
    ICacheService cacheService) : IGetMovieImagesService
{
    public async Task<ImagesResult> GetImagesAsync(
        Guid movieId,
        string? language,
        CancellationToken cancellationToken = default)
    {
        var movie = await movieRepository.GetByIdAsync(movieId, cancellationToken);
        if (movie is null)
        {
            throw new NotFoundException($"Movie with id '{movieId}' was not found.");
        }

        if (movie.TmdbId is null)
        {
            return ImagesResult.Empty;
        }

        var cacheKey = MovieImagesCacheKeys.Create(movieId, language);

        return await ImageGalleryServiceHelper.GetOrLoadAsync(
            cacheKey,
            cacheService,
            () => imageProvider.GetMovieImagesAsync(movie.TmdbId.Value, language, cancellationToken),
            language,
            cancellationToken);
    }
}

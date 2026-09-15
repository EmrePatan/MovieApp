using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Images;
using MovieApp.Application.Services.Images;

namespace MovieApp.Application.Services.TvShows;

public sealed class GetTvShowImagesService(
    ITvShowRepository tvShowRepository,
    IImageProvider imageProvider,
    ICacheService cacheService) : IGetTvShowImagesService
{
    public async Task<ImagesResult> GetImagesAsync(
        Guid tvShowId,
        string? language,
        CancellationToken cancellationToken = default)
    {
        var tvShow = await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken);
        if (tvShow is null)
        {
            throw new NotFoundException($"TV show with id '{tvShowId}' was not found.");
        }

        if (tvShow.TmdbId is null)
        {
            return ImagesResult.Empty;
        }

        var cacheKey = TvShowImagesCacheKeys.Create(tvShowId, language);

        return await ImageGalleryServiceHelper.GetOrLoadAsync(
            cacheKey,
            cacheService,
            () => imageProvider.GetTvShowImagesAsync(tvShow.TmdbId.Value, language, cancellationToken),
            language,
            cancellationToken);
    }
}

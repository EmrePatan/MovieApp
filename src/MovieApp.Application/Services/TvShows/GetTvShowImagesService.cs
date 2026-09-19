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
        var cacheKey = TvShowImagesCacheKeys.Create(tvShowId, language);
        var cached = await cacheService.GetAsync<ImagesCacheEntry>(cacheKey, cancellationToken);
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
            return ImagesResult.Empty;
        }

        return await ImageGalleryServiceHelper.GetOrLoadAsync(
            cacheKey,
            cacheService,
            () => imageProvider.GetTvShowImagesAsync(lookup.TmdbId.Value, language, cancellationToken),
            language,
            cancellationToken);
    }
}

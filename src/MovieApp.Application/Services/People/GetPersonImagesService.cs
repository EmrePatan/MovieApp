using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Images;
using MovieApp.Application.Services.Images;

namespace MovieApp.Application.Services.People;

public sealed class GetPersonImagesService(
    IImageProvider imageProvider,
    ICacheService cacheService) : IGetPersonImagesService
{
    public async Task<ImagesResult> GetImagesAsync(
        int tmdbPersonId,
        CancellationToken cancellationToken = default)
    {
        if (tmdbPersonId <= 0)
        {
            throw new ValidationException("A valid TMDB person id is required.");
        }

        var cacheKey = PersonImagesCacheKeys.Create(tmdbPersonId);

        return await ImageGalleryServiceHelper.GetOrLoadAsync(
            cacheKey,
            cacheService,
            () => imageProvider.GetPersonImagesAsync(tmdbPersonId, cancellationToken),
            preferredLanguage: null,
            cancellationToken);
    }
}

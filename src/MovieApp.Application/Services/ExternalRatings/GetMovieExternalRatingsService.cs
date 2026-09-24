using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.ExternalRatings;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.ExternalRatings;

public sealed class GetMovieExternalRatingsService(
    IMovieRepository movieRepository,
    ExternalRatingsAccessService accessService) : IGetMovieExternalRatingsService
{
    public async Task<ExternalRatingsResult> GetAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var lookup = await movieRepository.GetProviderLookupByIdAsync(movieId, cancellationToken);
        if (lookup is null)
        {
            throw new NotFoundException($"Movie with id '{movieId}' was not found.");
        }

        return await accessService.GetAsync(CatalogContentType.Movie, lookup.TmdbId, cancellationToken);
    }
}

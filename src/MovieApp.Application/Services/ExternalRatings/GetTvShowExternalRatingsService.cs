using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.ExternalRatings;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.ExternalRatings;

public sealed class GetTvShowExternalRatingsService(
    ITvShowRepository tvShowRepository,
    ExternalRatingsAccessService accessService) : IGetTvShowExternalRatingsService
{
    public async Task<ExternalRatingsResult> GetAsync(Guid tvShowId, CancellationToken cancellationToken = default)
    {
        var lookup = await tvShowRepository.GetProviderLookupByIdAsync(tvShowId, cancellationToken);
        if (lookup is null)
        {
            throw new NotFoundException($"TV show with id '{tvShowId}' was not found.");
        }

        return await accessService.GetAsync(CatalogContentType.Tv, lookup.TmdbId, cancellationToken);
    }
}

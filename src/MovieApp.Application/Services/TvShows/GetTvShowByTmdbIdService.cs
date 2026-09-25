using System.Globalization;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.Keywords;

namespace MovieApp.Application.Services.TvShows;

public sealed class GetTvShowByTmdbIdService(
    ITvShowRepository tvShowRepository,
    ITvShowDataProvider tvShowDataProvider,
    ICatalogProviderUpsertService catalogProviderUpsertService,
    IGetTvShowByIdService getTvShowByIdService) : IGetTvShowByTmdbIdService
{
    public async Task<TvShowDetailsResult> GetAsync(int tmdbId, CancellationToken cancellationToken = default)
    {
        if (tmdbId <= 0)
        {
            throw new ValidationException("A valid TMDB TV show id is required.");
        }

        var tvShow = await tvShowRepository.GetByTmdbIdAsync(tmdbId, cancellationToken);
        if (tvShow is null)
        {
            var providerDetails = await tvShowDataProvider.GetTvShowAsync(
                tmdbId.ToString(CultureInfo.InvariantCulture),
                includeKeywords: true,
                cancellationToken);

            if (providerDetails is null)
            {
                throw new NotFoundException("The requested TV show was not found.");
            }

            tvShow = await catalogProviderUpsertService.UpsertTvShowFromProviderAsync(
                providerDetails,
                enrichKeywords: true,
                cancellationToken);
        }

        return await getTvShowByIdService.GetByIdAsync(tvShow.Id, cancellationToken);
    }
}

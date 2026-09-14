using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Exceptions;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.TvShows;

public sealed class TvShowSeasonSummaryHydrator(
    ITvShowRepository tvShowRepository,
    ITvShowDataProvider tvShowDataProvider,
    ITvShowExternalIdResolver externalIdResolver) : ITvShowSeasonSummaryHydrator
{
    public async Task<TvShow> EnsureSeasonSummariesAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var tvShow = await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken);
        if (tvShow is null)
        {
            throw new NotFoundException($"TV show with id '{tvShowId}' was not found.");
        }

        if (tvShow.Seasons.Any(season => season.SeasonNumber >= 1))
        {
            return tvShow;
        }

        var externalId = externalIdResolver.Resolve(tvShow.TmdbId, tvShow.TvdbId, tvShow.ImdbId);
        if (externalId is null)
        {
            return tvShow;
        }

        var details = await tvShowDataProvider.GetTvShowAsync(externalId, cancellationToken);
        if (details is null)
        {
            return tvShow;
        }

        return await tvShowRepository.UpsertFromProviderAsync(details, cancellationToken);
    }
}

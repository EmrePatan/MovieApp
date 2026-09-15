using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Services.Keywords;

namespace MovieApp.Application.Services.MovieChanges;

public sealed class MovieChangesTargetedRefreshService(
    IMovieRepository movieRepository,
    IMovieDataProvider movieDataProvider,
    ICatalogProviderUpsertService catalogProviderUpsertService) : IMovieChangesTargetedRefreshService
{
    public async Task<TmdbChangesTargetRefreshResult> RefreshRelevantMovieAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var movie = await movieRepository.GetByIdAsync(movieId, cancellationToken);
        if (movie is null)
        {
            return TmdbChangesTargetRefreshResult.SkippedNotFound();
        }

        if (!movie.TmdbId.HasValue)
        {
            return TmdbChangesTargetRefreshResult.SkippedUnavailable();
        }

        var providerDetails = await movieDataProvider.GetMovieAsync(
            movie.TmdbId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            cancellationToken);

        if (providerDetails is null)
        {
            return TmdbChangesTargetRefreshResult.SkippedUnavailable();
        }

        await catalogProviderUpsertService.UpsertMovieFromProviderAsync(
            providerDetails,
            enrichKeywords: true,
            cancellationToken);

        return TmdbChangesTargetRefreshResult.Refreshed([]);
    }
}

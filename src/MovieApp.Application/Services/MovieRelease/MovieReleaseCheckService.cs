using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Notifications;

namespace MovieApp.Application.Services.MovieRelease;

public sealed class MovieReleaseCheckService(
    ICatalogFollowRepository catalogFollowRepository,
    IMovieRepository movieRepository,
    IMovieDataProvider movieDataProvider,
    ICatalogProviderUpsertService catalogProviderUpsertService,
    ICatalogReleaseEventRepository catalogReleaseEventRepository) : IMovieReleaseCheckService
{
    private static readonly TimeSpan ReleaseDateRefreshThreshold = TimeSpan.FromHours(24);

    public async Task<MovieReleaseCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movieIds = await catalogFollowRepository.GetFollowedMovieIdsAsync(cancellationToken);

        var moviesChecked = 0;
        var releaseEventsCreated = 0;
        var skippedProviderFailures = 0;
        var skippedNotReleased = 0;

        foreach (var movieId in movieIds)
        {
            var movie = await movieRepository.GetByIdAsync(movieId, cancellationToken);
            if (movie is null)
            {
                continue;
            }

            moviesChecked++;

            var isReleaseCandidate = movie.ReleaseDate is not null && movie.ReleaseDate.Value <= today;

            if (isReleaseCandidate)
            {
                var verifiedMovie = await RefreshMovieFromProviderAsync(movie, cancellationToken);
                if (verifiedMovie is null)
                {
                    skippedProviderFailures++;
                    continue;
                }

                movie = verifiedMovie;
            }
            else if (ShouldRefreshReleaseDate(movie))
            {
                var refreshedMovie = await RefreshMovieFromProviderAsync(movie, cancellationToken);
                if (refreshedMovie is null)
                {
                    skippedProviderFailures++;
                    continue;
                }

                movie = refreshedMovie;
            }

            if (movie.ReleaseDate is null || movie.ReleaseDate.Value > today)
            {
                skippedNotReleased++;
                continue;
            }

            var releaseEvent = CatalogReleaseEventFactory.CreateMovieReleasedEvent(
                movie.Id,
                movie.ReleaseDate.Value,
                CatalogReleaseEventSource.BoundaryDetection,
                DateTime.UtcNow);

            var insertResult = await catalogReleaseEventRepository.TryAddEventsAsync(
                [releaseEvent],
                cancellationToken);

            releaseEventsCreated += insertResult.EventsCreated;
        }

        return new MovieReleaseCheckResult(
            moviesChecked,
            releaseEventsCreated,
            skippedProviderFailures,
            skippedNotReleased);
    }

    private async Task<Domain.Entities.Movie?> RefreshMovieFromProviderAsync(
        Domain.Entities.Movie movie,
        CancellationToken cancellationToken)
    {
        if (!movie.TmdbId.HasValue)
        {
            return null;
        }

        var providerDetails = await movieDataProvider.GetMovieAsync(
            movie.TmdbId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            cancellationToken);

        if (providerDetails is null)
        {
            return null;
        }

        return await catalogProviderUpsertService.UpsertMovieFromProviderAsync(
            providerDetails,
            cancellationToken: cancellationToken);
    }

    private static bool ShouldRefreshReleaseDate(Domain.Entities.Movie movie)
    {
        if (movie.ReleaseDate is null)
        {
            return true;
        }

        return DateTime.UtcNow - movie.UpdatedAt >= ReleaseDateRefreshThreshold;
    }
}

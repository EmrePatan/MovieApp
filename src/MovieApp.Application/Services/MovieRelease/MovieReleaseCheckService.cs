using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.RegionalRelease;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Notifications;

namespace MovieApp.Application.Services.MovieRelease;

public sealed class MovieReleaseCheckService(
    ICatalogFollowRepository catalogFollowRepository,
    IMovieRepository movieRepository,
    IMovieRegionalReleaseRepository movieRegionalReleaseRepository,
    IMovieDataProvider movieDataProvider,
    IMovieReleaseDatesProvider movieReleaseDatesProvider,
    ICatalogProviderUpsertService catalogProviderUpsertService,
    ICatalogReleaseEventRepository catalogReleaseEventRepository,
    IRegionalEffectiveReleaseResolver regionalEffectiveReleaseResolver,
    IOptions<ReleaseRegionOptions> releaseRegionOptions) : IMovieReleaseCheckService
{
    private static readonly TimeSpan ReleaseDateRefreshThreshold = TimeSpan.FromHours(24);

    public async Task<MovieReleaseCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var region = WatchProviderRegionValidator.Normalize(releaseRegionOptions.Value.DefaultRegion);
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

            var regionalRelease = await movieRegionalReleaseRepository.GetByMovieIdAndRegionAsync(
                movie.Id,
                region,
                cancellationToken);

            var releaseAlreadyEmitted = await catalogReleaseEventRepository.ExistsByDedupeKeyAsync(
                CatalogReleaseEventDedupeKey.ForMovieReleased(movie.Id),
                cancellationToken);

            var readableEffectiveDate = GetEffectiveReleaseDate(regionalRelease, movie.ReleaseDate);
            var requiresVerification = readableEffectiveDate is not null
                                       && readableEffectiveDate.Value <= today
                                       && !releaseAlreadyEmitted;

            var needsMovieRefresh = !releaseAlreadyEmitted
                                    && (requiresVerification || ShouldRefreshReleaseDate(movie));
            var needsRegionalRefresh = !releaseAlreadyEmitted
                                       && ShouldRefreshRegionalRelease(regionalRelease, readableEffectiveDate);

            if (needsMovieRefresh)
            {
                var refreshedMovie = await RefreshMovieFromProviderAsync(movie, cancellationToken);
                if (refreshedMovie is null)
                {
                    if (requiresVerification)
                    {
                        skippedProviderFailures++;
                        continue;
                    }

                    skippedProviderFailures++;
                }
                else
                {
                    movie = refreshedMovie;
                }
            }

            if (needsRegionalRefresh)
            {
                var refreshedRegionalRelease = await RefreshRegionalReleaseFromProviderAsync(
                    movie,
                    region,
                    cancellationToken);

                if (refreshedRegionalRelease is null)
                {
                    if (requiresVerification)
                    {
                        skippedProviderFailures++;
                        continue;
                    }
                }
                else
                {
                    regionalRelease = refreshedRegionalRelease;
                }
            }

            var verifiedEffectiveDate = GetEffectiveReleaseDate(regionalRelease, movie.ReleaseDate);
            if (verifiedEffectiveDate is null || verifiedEffectiveDate.Value > today)
            {
                skippedNotReleased++;
                continue;
            }

            var releaseEvent = CatalogReleaseEventFactory.CreateMovieReleasedEvent(
                movie.Id,
                verifiedEffectiveDate.Value,
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

    private async Task<Movie?> RefreshMovieFromProviderAsync(
        Movie movie,
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

    private async Task<MovieRegionalRelease?> RefreshRegionalReleaseFromProviderAsync(
        Movie movie,
        string region,
        CancellationToken cancellationToken)
    {
        if (!movie.TmdbId.HasValue)
        {
            return null;
        }

        IReadOnlyList<Application.Models.RegionalRelease.RegionalMovieReleaseEntry> releaseDateEntries;
        try
        {
            releaseDateEntries = await movieReleaseDatesProvider.GetMovieReleaseDatesAsync(
                movie.TmdbId.Value,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }

        var effectiveRelease = regionalEffectiveReleaseResolver.Resolve(
            region,
            releaseDateEntries,
            movie.ReleaseDate);

        var regionalRelease = new MovieRegionalRelease
        {
            MovieId = movie.Id,
            Region = region,
            EffectiveReleaseDate = effectiveRelease.EffectiveReleaseDate,
            EffectiveReleaseType = effectiveRelease.EffectiveReleaseType,
            Certification = effectiveRelease.Certification,
            IsFallbackGlobal = effectiveRelease.IsFallbackGlobal,
            SyncedAtUtc = DateTime.UtcNow
        };

        return await movieRegionalReleaseRepository.UpsertAsync(regionalRelease, cancellationToken);
    }

    private static DateOnly? GetEffectiveReleaseDate(
        MovieRegionalRelease? regionalRelease,
        DateOnly? globalReleaseDate)
    {
        if (regionalRelease is not null)
        {
            return regionalRelease.EffectiveReleaseDate;
        }

        return globalReleaseDate;
    }

    private static bool ShouldRefreshRegionalRelease(
        MovieRegionalRelease? regionalRelease,
        DateOnly? readableEffectiveDate)
    {
        if (regionalRelease is null)
        {
            return true;
        }

        if (readableEffectiveDate is not null
            && readableEffectiveDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return true;
        }

        if (DateTime.UtcNow - regionalRelease.SyncedAtUtc < ReleaseDateRefreshThreshold)
        {
            return false;
        }

        return readableEffectiveDate is null
               || readableEffectiveDate.Value > DateOnly.FromDateTime(DateTime.UtcNow)
               || regionalRelease.IsFallbackGlobal;
    }

    private static bool ShouldRefreshReleaseDate(Movie movie)
    {
        if (movie.ReleaseDate is null)
        {
            return true;
        }

        return DateTime.UtcNow - movie.UpdatedAt >= ReleaseDateRefreshThreshold;
    }
}

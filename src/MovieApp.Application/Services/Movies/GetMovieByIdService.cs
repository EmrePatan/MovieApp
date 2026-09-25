using System.Globalization;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.MovieFollows;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Movies;

public sealed class GetMovieByIdService(
    IMovieRepository movieRepository,
    IMovieRegionalReleaseRepository movieRegionalReleaseRepository,
    IOptions<ReleaseRegionOptions> releaseRegionOptions,
    ICatalogKeywordReadPathScheduler catalogKeywordReadPathScheduler,
    IMovieDataProvider movieDataProvider,
    ICatalogProviderUpsertService catalogProviderUpsertService,
    ICacheService cacheService,
    IDetailLocalizationOverlayService? detailLocalizationOverlayService = null) : IGetMovieByIdService
{
    private static readonly TimeSpan DetailsCacheTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CollectionProbeTtl = TimeSpan.FromHours(24);

    public Task<MovieDetailsResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetByIdCoreAsync(id, prefetchedMovie: null, contentLocale: null, cancellationToken);

    public Task<MovieDetailsResult> GetByIdAsync(
        Guid id,
        Movie? prefetchedMovie,
        CancellationToken cancellationToken = default) =>
        GetByIdCoreAsync(id, prefetchedMovie, contentLocale: null, cancellationToken);

    public Task<MovieDetailsResult> GetByIdAsync(
        Guid id,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        GetByIdCoreAsync(id, prefetchedMovie: null, contentLocale, cancellationToken);

    public Task<MovieDetailsResult> GetByIdAsync(
        Guid id,
        Movie? prefetchedMovie,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        GetByIdCoreAsync(id, prefetchedMovie, contentLocale, cancellationToken);

    private async Task<MovieDetailsResult> GetByIdCoreAsync(
        Guid id,
        Movie? prefetchedMovie,
        string? contentLocale,
        CancellationToken cancellationToken)
    {
        var cacheKey = MovieDetailsCacheKeys.Create(id);
        var cachedEntry = await cacheService.GetAsync<MovieDetailsCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return await ApplyOverlayAsync(cachedEntry.Result, contentLocale, cancellationToken);
        }

        var movie = prefetchedMovie ?? await movieRepository.GetByIdAsync(id, cancellationToken);
        if (movie is null || movie.Id != id)
        {
            throw new NotFoundException($"Movie with id '{id}' was not found.");
        }

        var overlayTask = StartMovieOverlay(movie.TmdbId, contentLocale, cancellationToken);
        var result = await BuildDetailsAsync(movie, cancellationToken);

        await cacheService.SetAsync(
            cacheKey,
            new MovieDetailsCacheEntry { Result = result },
            DetailsCacheTtl,
            cancellationToken);

        if (overlayTask is null)
        {
            return result;
        }

        return detailLocalizationOverlayService!.ApplyLoadedMovieOverlay(result, await overlayTask);
    }

    private Task<Models.Localization.MovieDetailLocalizationData?>? StartMovieOverlay(
        int? tmdbId,
        string? contentLocale,
        CancellationToken cancellationToken)
    {
        if (detailLocalizationOverlayService is null || contentLocale is null)
        {
            return null;
        }

        return detailLocalizationOverlayService.LoadMovieOverlayAsync(tmdbId, contentLocale, cancellationToken);
    }

    private async Task<MovieDetailsResult> ApplyOverlayAsync(
        MovieDetailsResult result,
        string? contentLocale,
        CancellationToken cancellationToken)
    {
        if (detailLocalizationOverlayService is null || contentLocale is null)
        {
            return result;
        }

        return await detailLocalizationOverlayService.ApplyMovieOverlayAsync(
            result,
            contentLocale,
            cancellationToken);
    }

    private async Task<MovieDetailsResult> BuildDetailsAsync(
        Movie movie,
        CancellationToken cancellationToken)
    {
        movie = await TryEnrichMissingCollectionAsync(movie, cancellationToken);

        if (movie.KeywordsSyncedAtUtc is null)
        {
            catalogKeywordReadPathScheduler.ScheduleMovie(movie.Id);
        }

        var details = MovieMapper.ToDetailsResult(movie);
        var region = WatchProviderRegionValidator.Normalize(releaseRegionOptions.Value.DefaultRegion);
        var regionalRelease = await movieRegionalReleaseRepository.GetByMovieIdAndRegionAsync(
            movie.Id,
            region,
            cancellationToken);
        var effectiveReleaseDate = MovieFollowReleaseDateResolver.Resolve(regionalRelease, movie.ReleaseDate);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var isReleased = MovieConsumptionReleaseGuardrail.IsReleasedForConsumption(effectiveReleaseDate, today);
        var canFollowForRelease = MovieFollowActionEligibility.CanFollowForRelease(effectiveReleaseDate, today);
        var canSetReleaseAlert = MovieFollowActionEligibility.CanSetReleaseAlert(effectiveReleaseDate, today);

        return details with
        {
            IsReleased = isReleased,
            CanFollowForRelease = canFollowForRelease,
            CanSetReleaseAlert = canSetReleaseAlert
        };
    }

    private async Task<Movie> TryEnrichMissingCollectionAsync(
        Movie movie,
        CancellationToken cancellationToken)
    {
        if (movie.TmdbCollectionId.HasValue || movie.TmdbId is not int tmdbId || tmdbId <= 0)
        {
            return movie;
        }

        var probeKey = MovieDetailsCacheKeys.CollectionProbe(movie.Id);
        if (await cacheService.GetAsync<MovieCollectionProbeCacheEntry>(probeKey, cancellationToken) is not null)
        {
            return movie;
        }

        var providerDetails = await movieDataProvider.GetMovieAsync(
            tmdbId.ToString(CultureInfo.InvariantCulture),
            includeKeywords: false,
            cancellationToken);

        if (providerDetails is null)
        {
            return movie;
        }

        if (providerDetails.TmdbCollectionId is null)
        {
            await cacheService.SetAsync(
                probeKey,
                new MovieCollectionProbeCacheEntry(),
                CollectionProbeTtl,
                cancellationToken);
            return movie;
        }

        return await catalogProviderUpsertService.UpsertMovieFromProviderAsync(
            providerDetails,
            enrichKeywords: false,
            cancellationToken);
    }
}

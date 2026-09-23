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
using MovieApp.Application.Services.MovieFollows;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Movies;

public sealed class GetMovieByIdService(
    IMovieRepository movieRepository,
    IMovieRegionalReleaseRepository movieRegionalReleaseRepository,
    IOptions<ReleaseRegionOptions> releaseRegionOptions,
    ICatalogKeywordIngestionService catalogKeywordIngestionService,
    IMovieDataProvider movieDataProvider,
    ICatalogProviderUpsertService catalogProviderUpsertService,
    ICacheService cacheService) : IGetMovieByIdService
{
    private static readonly TimeSpan DetailsCacheTtl = TimeSpan.FromMinutes(15);

    public Task<MovieDetailsResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, prefetchedMovie: null, cancellationToken);

    public async Task<MovieDetailsResult> GetByIdAsync(
        Guid id,
        Movie? prefetchedMovie,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = MovieDetailsCacheKeys.Create(id);
        var cachedEntry = await cacheService.GetAsync<MovieDetailsCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var movie = prefetchedMovie ?? await movieRepository.GetByIdAsync(id, cancellationToken);
        if (movie is null || movie.Id != id)
        {
            throw new NotFoundException($"Movie with id '{id}' was not found.");
        }

        var result = await BuildDetailsAsync(movie, cancellationToken);

        await cacheService.SetAsync(
            cacheKey,
            new MovieDetailsCacheEntry { Result = result },
            DetailsCacheTtl,
            cancellationToken);

        return result;
    }

    private async Task<MovieDetailsResult> BuildDetailsAsync(
        Movie movie,
        CancellationToken cancellationToken)
    {
        movie = await TryEnrichMissingCollectionAsync(movie, cancellationToken);

        await catalogKeywordIngestionService.TryEnrichMovieKeywordsAsync(
            movie.Id,
            refreshKeywords: false,
            cancellationToken: cancellationToken);

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

        var providerDetails = await movieDataProvider.GetMovieAsync(
            tmdbId.ToString(CultureInfo.InvariantCulture),
            includeKeywords: false,
            cancellationToken);

        if (providerDetails?.TmdbCollectionId is null)
        {
            return movie;
        }

        return await catalogProviderUpsertService.UpsertMovieFromProviderAsync(
            providerDetails,
            enrichKeywords: false,
            cancellationToken);
    }
}

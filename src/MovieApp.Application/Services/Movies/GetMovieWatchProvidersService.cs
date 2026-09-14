using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.WatchProviders;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Movies;

public sealed class GetMovieWatchProvidersService(
    IMovieRepository movieRepository,
    IWatchProviderService watchProviderService,
    ICacheService cacheService) : IGetMovieWatchProvidersService
{
    private static readonly TimeSpan WatchProvidersCacheTtl = TimeSpan.FromHours(6);

    public async Task<WatchProvidersResult> GetWatchProvidersAsync(
        Guid movieId,
        string? region,
        CancellationToken cancellationToken = default)
    {
        var regionValidation = WatchProviderRegionValidator.Validate(region);
        if (!regionValidation.IsValid)
        {
            throw new ValidationException(regionValidation.ErrorMessage!);
        }

        var normalizedRegion = WatchProviderRegionValidator.Normalize(region);

        var movie = await movieRepository.GetByIdAsync(movieId, cancellationToken);
        if (movie is null)
        {
            throw new NotFoundException($"Movie with id '{movieId}' was not found.");
        }

        if (movie.TmdbId is null)
        {
            return new WatchProvidersResult(normalizedRegion, [], null);
        }

        var cacheKey = MovieWatchProvidersCacheKeys.Create(movieId, normalizedRegion);
        var cached = await cacheService.GetAsync<WatchProvidersCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        var providers = await watchProviderService.GetMovieWatchProvidersAsync(
            movie.TmdbId.Value,
            normalizedRegion,
            cancellationToken);

        await cacheService.SetAsync(
            cacheKey,
            new WatchProvidersCacheEntry { Result = providers },
            WatchProvidersCacheTtl,
            cancellationToken);

        return providers;
    }
}

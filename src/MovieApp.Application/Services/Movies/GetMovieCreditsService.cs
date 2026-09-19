using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Credits;
using MovieApp.Application.Services.Credits;

namespace MovieApp.Application.Services.Movies;

public sealed class GetMovieCreditsService(
    IMovieRepository movieRepository,
    ICreditsProvider creditsProvider,
    ICacheService cacheService) : IGetMovieCreditsService
{
    private static readonly TimeSpan CreditsCacheTtl = TimeSpan.FromHours(24);

    public async Task<CreditsResult> GetCreditsAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = MovieCreditsCacheKeys.Create(movieId);
        var cached = await cacheService.GetAsync<CreditsCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        var lookup = await movieRepository.GetProviderLookupByIdAsync(movieId, cancellationToken);
        if (lookup is null)
        {
            throw new NotFoundException($"Movie with id '{movieId}' was not found.");
        }

        if (lookup.TmdbId is null)
        {
            return new CreditsResult([], []);
        }

        var credits = CreditsNormalizer.Normalize(
            await creditsProvider.GetMovieCreditsAsync(lookup.TmdbId.Value, cancellationToken));

        await cacheService.SetAsync(
            cacheKey,
            new CreditsCacheEntry { Result = credits },
            CreditsCacheTtl,
            cancellationToken);

        return credits;
    }
}

using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Credits;

namespace MovieApp.Application.Services.Movies;

public sealed class GetMovieCreditsService(
    IMovieRepository movieRepository,
    ICreditsProvider creditsProvider,
    ICacheService cacheService) : IGetMovieCreditsService
{
    private const int MaxCastMembers = 12;
    private static readonly TimeSpan CreditsCacheTtl = TimeSpan.FromHours(24);

    public async Task<CreditsResult> GetCreditsAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var movie = await movieRepository.GetByIdAsync(movieId, cancellationToken);
        if (movie is null)
        {
            throw new NotFoundException($"Movie with id '{movieId}' was not found.");
        }

        if (movie.TmdbId is null)
        {
            return new CreditsResult([]);
        }

        var cacheKey = MovieCreditsCacheKeys.Create(movieId);
        var cached = await cacheService.GetAsync<CreditsCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        var credits = await creditsProvider.GetMovieCreditsAsync(movie.TmdbId.Value, cancellationToken);
        var trimmed = TrimCast(credits);

        await cacheService.SetAsync(
            cacheKey,
            new CreditsCacheEntry { Result = trimmed },
            CreditsCacheTtl,
            cancellationToken);

        return trimmed;
    }

    private static CreditsResult TrimCast(CreditsResult credits)
    {
        var cast = credits.Cast
            .OrderBy(member => member.Order)
            .Take(MaxCastMembers)
            .ToList();

        return new CreditsResult(cast);
    }
}

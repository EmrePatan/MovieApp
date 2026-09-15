using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Credits;
using MovieApp.Application.Services.Credits;

namespace MovieApp.Application.Services.TvShows;

public sealed class GetTvShowCreditsService(
    ITvShowRepository tvShowRepository,
    ICreditsProvider creditsProvider,
    ICacheService cacheService) : IGetTvShowCreditsService
{
    private static readonly TimeSpan CreditsCacheTtl = TimeSpan.FromHours(24);

    public async Task<CreditsResult> GetCreditsAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var tvShow = await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken);
        if (tvShow is null)
        {
            throw new NotFoundException($"TV show with id '{tvShowId}' was not found.");
        }

        if (tvShow.TmdbId is null)
        {
            return new CreditsResult([], []);
        }

        var cacheKey = TvShowCreditsCacheKeys.Create(tvShowId);
        var cached = await cacheService.GetAsync<CreditsCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        var credits = CreditsNormalizer.Normalize(
            await creditsProvider.GetTvShowCreditsAsync(tvShow.TmdbId.Value, cancellationToken));

        await cacheService.SetAsync(
            cacheKey,
            new CreditsCacheEntry { Result = credits },
            CreditsCacheTtl,
            cancellationToken);

        return credits;
    }
}

using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Credits;

namespace MovieApp.Application.Services.TvShows;

public sealed class GetTvShowCreditsService(
    ITvShowRepository tvShowRepository,
    ICreditsProvider creditsProvider,
    ICacheService cacheService) : IGetTvShowCreditsService
{
    private const int MaxCastMembers = 12;
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
            return new CreditsResult([]);
        }

        var cacheKey = TvShowCreditsCacheKeys.Create(tvShowId);
        var cached = await cacheService.GetAsync<CreditsCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        var credits = await creditsProvider.GetTvShowCreditsAsync(tvShow.TmdbId.Value, cancellationToken);
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

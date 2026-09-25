using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Identity;

namespace MovieApp.Application.Services.TvShowFollows;

public sealed class RemoveTvShowFollowService(
    ICurrentUser currentUser,
    ITvShowFollowRepository tvShowFollowRepository,
    ICacheService cacheService,
    IServiceScopeFactory? recommendationScopeFactory = null) : IRemoveTvShowFollowService
{
    public async Task RemoveAsync(Guid tvShowId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var removed = await tvShowFollowRepository.RemoveForTvShowAsync(userId, tvShowId, cancellationToken);
        if (removed)
        {
            await BackgroundAnalyticsInvalidation.InvalidateRecommendationsAsync(
                cacheService,
                recommendationScopeFactory,
                userId,
                cancellationToken);
        }
    }
}

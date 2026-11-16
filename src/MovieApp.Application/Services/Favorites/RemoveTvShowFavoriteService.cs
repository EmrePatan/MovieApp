using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Caching;
using MovieApp.Application.Identity;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Favorites;

public sealed class RemoveTvShowFavoriteService(
    ICurrentUser currentUser,
    IFavoriteRepository favoriteRepository,
    IUserAnalyticsCacheInvalidator analyticsCacheInvalidator,
    IServiceScopeFactory? analyticsScopeFactory = null,
    ISocialActivityRecorder? socialActivityRecorder = null,
    IApplicationDbContext? applicationDbContext = null) : IRemoveTvShowFavoriteService
{
    public async Task RemoveAsync(Guid tvShowId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        if (applicationDbContext is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                await favoriteRepository.RemoveForTvShowAsync(userId, tvShowId, ct);
                if (socialActivityRecorder is not null)
                    await socialActivityRecorder.RetractAsync(userId, SocialActivityType.FavoritedTvShow, tvShowId, ct);
            }, cancellationToken);
        }
        else
        {
            await favoriteRepository.RemoveForTvShowAsync(userId, tvShowId, cancellationToken);
            if (socialActivityRecorder is not null)
                await socialActivityRecorder.RetractAsync(userId, SocialActivityType.FavoritedTvShow, tvShowId, cancellationToken);
        }
        await BackgroundAnalyticsInvalidation.RunAsync(
            analyticsCacheInvalidator,
            analyticsScopeFactory,
            userId,
            cancellationToken);
    }
}

using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Identity;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.MovieFollows;

public sealed class RemoveMovieFollowService(
    ICurrentUser currentUser,
    ICatalogFollowRepository catalogFollowRepository,
    ICacheService cacheService,
    IServiceScopeFactory? recommendationScopeFactory = null) : IRemoveMovieFollowService
{
    public async Task RemoveAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var removed = await catalogFollowRepository.RemoveAsync(
            userId,
            CatalogContentType.Movie,
            movieId,
            cancellationToken);

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

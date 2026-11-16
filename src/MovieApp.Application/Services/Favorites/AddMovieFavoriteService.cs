using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Favorites;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Favorites;

public sealed class AddMovieFavoriteService(
    ICurrentUser currentUser,
    IFavoriteRepository favoriteRepository,
    IMovieRepository movieRepository,
    IUserAnalyticsCacheInvalidator analyticsCacheInvalidator,
    IServiceScopeFactory? analyticsScopeFactory = null,
    ISocialActivityRecorder? socialActivityRecorder = null) : IAddMovieFavoriteService
{
    public async Task<FavoriteMutationResult> AddAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        if (await favoriteRepository.ExistsForMovieAsync(userId, movieId, cancellationToken))
        {
            return FavoriteMutationResult.AlreadyExists;
        }

        if (!await movieRepository.ExistsAsync(movieId, cancellationToken))
        {
            throw new NotFoundException("The requested movie was not found.");
        }

        var favorite = Favorite.CreateForMovie(userId, movieId, DateTime.UtcNow);

        var added = await favoriteRepository.TryAddAsync(favorite, cancellationToken);
        if (added)
        {
            if (socialActivityRecorder is not null)
                await socialActivityRecorder.RecordAsync(userId, SocialActivityType.FavoritedMovie, favorite.Id, movieId, "Movie", favorite.CreatedAt, cancellationToken);
            await BackgroundAnalyticsInvalidation.RunAsync(
                analyticsCacheInvalidator,
                analyticsScopeFactory,
                userId,
                cancellationToken);
            return FavoriteMutationResult.Created;
        }

        return FavoriteMutationResult.AlreadyExists;
    }
}

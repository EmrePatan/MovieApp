using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Recommendations;

public sealed class RecommendationDismissalService(
    ICurrentUser currentUser,
    IRecommendationDismissalRepository dismissalRepository,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    IUserAnalyticsCacheInvalidator analyticsCacheInvalidator,
    IServiceScopeFactory? analyticsScopeFactory = null) : IRecommendationDismissalService
{
    public async Task DismissMovieAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        if (!await movieRepository.ExistsAsync(movieId, cancellationToken))
        {
            throw new NotFoundException("The requested movie was not found.");
        }

        if (await dismissalRepository.ExistsForMovieAsync(userId, movieId, cancellationToken))
        {
            return;
        }

        var dismissal = RecommendationDismissal.CreateForMovie(userId, movieId, DateTime.UtcNow);
        if (await dismissalRepository.TryAddAsync(dismissal, cancellationToken))
        {
            await InvalidateAsync(userId, cancellationToken);
        }
    }

    public async Task DismissTvShowAsync(Guid tvShowId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        if (!await tvShowRepository.ExistsAsync(tvShowId, cancellationToken))
        {
            throw new NotFoundException("The requested TV show was not found.");
        }

        if (await dismissalRepository.ExistsForTvShowAsync(userId, tvShowId, cancellationToken))
        {
            return;
        }

        var dismissal = RecommendationDismissal.CreateForTvShow(userId, tvShowId, DateTime.UtcNow);
        if (await dismissalRepository.TryAddAsync(dismissal, cancellationToken))
        {
            await InvalidateAsync(userId, cancellationToken);
        }
    }

    private Task InvalidateAsync(Guid userId, CancellationToken cancellationToken) =>
        BackgroundAnalyticsInvalidation.RunAsync(
            analyticsCacheInvalidator,
            analyticsScopeFactory,
            userId,
            cancellationToken);
}

using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.MovieFollows;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.MovieFollows;

public sealed class UpsertMovieFollowService(
    ICurrentUser currentUser,
    ICatalogFollowRepository catalogFollowRepository,
    IMovieRepository movieRepository) : IUpsertMovieFollowService
{
    private const int MaxCreateAttempts = 3;

    public async Task<(MovieFollowMutationResult Mutation, MovieFollowStatusResult Status)> UpsertAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var utcNow = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(utcNow);

        var movie = await movieRepository.GetByIdAsync(movieId, cancellationToken);
        if (movie is null)
        {
            throw new NotFoundException("The requested movie was not found.");
        }

        CatalogFollowValidator.ValidateMovieFollowEligibility(movie.ReleaseDate, today);

        for (var attempt = 0; attempt < MaxCreateAttempts; attempt++)
        {
            var existing = await catalogFollowRepository.GetForUserAndContentForUpdateAsync(
                userId,
                CatalogContentType.Movie,
                movieId,
                cancellationToken);

            if (existing is not null)
            {
                CatalogFollowValidator.ValidateMovieFollow(existing);
                return (MovieFollowMutationResult.Updated, new MovieFollowStatusResult(true));
            }

            var follow = CatalogFollow.CreateMovieFollow(userId, movieId, utcNow);
            CatalogFollowValidator.ValidateMovieFollow(follow);

            var added = await catalogFollowRepository.TryAddAsync(follow, cancellationToken);
            if (added)
            {
                return (MovieFollowMutationResult.Created, new MovieFollowStatusResult(true));
            }
        }

        var concurrentFollow = await catalogFollowRepository.GetForUserAndContentForUpdateAsync(
            userId,
            CatalogContentType.Movie,
            movieId,
            cancellationToken);

        if (concurrentFollow is null)
        {
            throw new ConflictException("Unable to create movie follow due to a concurrent update.");
        }

        return (MovieFollowMutationResult.Updated, new MovieFollowStatusResult(true));
    }
}

using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Ratings;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Ratings;

public sealed class RatingService(
    ICurrentUser currentUser,
    IRatingRepository ratingRepository,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    IUserAnalyticsCacheInvalidator analyticsCacheInvalidator) : IRatingService
{
    public async Task<RatingUpsertResult> UpsertMovieRatingAsync(
        Guid movieId,
        int score,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidateScore(score);
        await EnsureMovieExistsAsync(movieId, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var existingRating = await ratingRepository.GetByUserAndMovieAsync(userId, movieId, cancellationToken);
        if (existingRating is not null)
        {
            existingRating.UpdateScore(score, utcNow);
            await ratingRepository.UpdateAsync(existingRating, cancellationToken);
            await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
            return new RatingUpsertResult(RatingMapper.ToResult(existingRating), Created: false);
        }

        var rating = Rating.CreateForMovie(userId, movieId, score, utcNow);
        await ratingRepository.AddAsync(rating, cancellationToken);
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
        return new RatingUpsertResult(RatingMapper.ToResult(rating), Created: true);
    }

    public async Task<RatingUpsertResult> UpsertTvShowRatingAsync(
        Guid tvShowId,
        int score,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidateScore(score);
        await EnsureTvShowExistsAsync(tvShowId, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var existingRating = await ratingRepository.GetByUserAndTvShowAsync(userId, tvShowId, cancellationToken);
        if (existingRating is not null)
        {
            existingRating.UpdateScore(score, utcNow);
            await ratingRepository.UpdateAsync(existingRating, cancellationToken);
            await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
            return new RatingUpsertResult(RatingMapper.ToResult(existingRating), Created: false);
        }

        var rating = Rating.CreateForTvShow(userId, tvShowId, score, utcNow);
        await ratingRepository.AddAsync(rating, cancellationToken);
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
        return new RatingUpsertResult(RatingMapper.ToResult(rating), Created: true);
    }

    public async Task DeleteMovieRatingAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var deleted = await ratingRepository.DeleteForMovieAsync(userId, movieId, cancellationToken);
        if (!deleted)
        {
            throw new NotFoundException("The requested rating was not found.");
        }

        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
    }

    public async Task DeleteTvShowRatingAsync(Guid tvShowId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var deleted = await ratingRepository.DeleteForTvShowAsync(userId, tvShowId, cancellationToken);
        if (!deleted)
        {
            throw new NotFoundException("The requested rating was not found.");
        }

        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
    }

    public async Task<RatingResult> GetCurrentUserMovieRatingAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var rating = await ratingRepository.GetByUserAndMovieAsync(userId, movieId, cancellationToken);
        if (rating is null)
        {
            throw new NotFoundException("The requested rating was not found.");
        }

        return RatingMapper.ToResult(rating);
    }

    public async Task<RatingResult> GetCurrentUserTvShowRatingAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var rating = await ratingRepository.GetByUserAndTvShowAsync(userId, tvShowId, cancellationToken);
        if (rating is null)
        {
            throw new NotFoundException("The requested rating was not found.");
        }

        return RatingMapper.ToResult(rating);
    }

    public async Task<RatingSummaryResult> GetMovieRatingSummaryAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        if (await movieRepository.GetByIdAsync(movieId, cancellationToken) is null)
        {
            throw new NotFoundException("The requested movie was not found.");
        }

        return await ratingRepository.GetSummaryForMovieAsync(movieId, cancellationToken);
    }

    public async Task<RatingSummaryResult> GetTvShowRatingSummaryAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        if (await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken) is null)
        {
            throw new NotFoundException("The requested TV show was not found.");
        }

        return await ratingRepository.GetSummaryForTvShowAsync(tvShowId, cancellationToken);
    }

    private static void ValidateScore(int score)
    {
        var validationResult = RatingScoreValidator.Validate(score);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }
    }

    private async Task EnsureMovieExistsAsync(Guid movieId, CancellationToken cancellationToken)
    {
        if (await movieRepository.GetByIdAsync(movieId, cancellationToken) is null)
        {
            throw new NotFoundException("The requested movie was not found.");
        }
    }

    private async Task EnsureTvShowExistsAsync(Guid tvShowId, CancellationToken cancellationToken)
    {
        if (await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken) is null)
        {
            throw new NotFoundException("The requested TV show was not found.");
        }
    }
}

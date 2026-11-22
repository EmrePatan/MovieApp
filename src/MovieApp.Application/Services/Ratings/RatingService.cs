using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Ratings;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Ratings;

public sealed class RatingService(
    ICurrentUser currentUser,
    IRatingRepository ratingRepository,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    IEpisodeRepository episodeRepository,
    IUserAnalyticsCacheInvalidator analyticsCacheInvalidator,
    ISocialActivityRecorder? socialActivityRecorder = null,
    IApplicationDbContext? applicationDbContext = null) : IRatingService
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
            if (applicationDbContext is not null && socialActivityRecorder is not null)
            {
                await applicationDbContext.ExecuteInTransactionAsync(async ct =>
                {
                    await ratingRepository.UpdateAsync(existingRating, ct);
                    await socialActivityRecorder.RecordAsync(
                        userId, SocialActivityType.RatedMovie, existingRating.Id, movieId, "Movie", utcNow, ct);
                }, cancellationToken);
            }
            else
            {
                await ratingRepository.UpdateAsync(existingRating, cancellationToken);
                if (socialActivityRecorder is not null)
                    await socialActivityRecorder.RecordAsync(
                        userId, SocialActivityType.RatedMovie, existingRating.Id, movieId, "Movie", utcNow, cancellationToken);
            }
            await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
            return new RatingUpsertResult(RatingMapper.ToResult(existingRating), Created: false);
        }

        var rating = Rating.CreateForMovie(userId, movieId, score, utcNow);
        Rating savedRating = rating;
        var created = false;
        if (applicationDbContext is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                (savedRating, created) = await ratingRepository.AddAsync(rating, ct);
                if (created && socialActivityRecorder is not null)
                    await socialActivityRecorder.RecordAsync(userId, SocialActivityType.RatedMovie, savedRating.Id, movieId, "Movie", savedRating.CreatedAt, ct);
            }, cancellationToken);
        }
        else
        {
            (savedRating, created) = await ratingRepository.AddAsync(rating, cancellationToken);
            if (created && socialActivityRecorder is not null)
                await socialActivityRecorder.RecordAsync(userId, SocialActivityType.RatedMovie, savedRating.Id, movieId, "Movie", savedRating.CreatedAt, cancellationToken);
        }
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
        return new RatingUpsertResult(RatingMapper.ToResult(savedRating), created);
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
            if (applicationDbContext is not null && socialActivityRecorder is not null)
            {
                await applicationDbContext.ExecuteInTransactionAsync(async ct =>
                {
                    await ratingRepository.UpdateAsync(existingRating, ct);
                    await socialActivityRecorder.RecordAsync(
                        userId, SocialActivityType.RatedTvShow, existingRating.Id, tvShowId, "TvShow", utcNow, ct);
                }, cancellationToken);
            }
            else
            {
                await ratingRepository.UpdateAsync(existingRating, cancellationToken);
                if (socialActivityRecorder is not null)
                    await socialActivityRecorder.RecordAsync(
                        userId, SocialActivityType.RatedTvShow, existingRating.Id, tvShowId, "TvShow", utcNow, cancellationToken);
            }
            await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
            return new RatingUpsertResult(RatingMapper.ToResult(existingRating), Created: false);
        }

        var rating = Rating.CreateForTvShow(userId, tvShowId, score, utcNow);
        Rating savedRating = rating;
        var created = false;
        if (applicationDbContext is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                (savedRating, created) = await ratingRepository.AddAsync(rating, ct);
                if (created && socialActivityRecorder is not null)
                    await socialActivityRecorder.RecordAsync(userId, SocialActivityType.RatedTvShow, savedRating.Id, tvShowId, "TvShow", savedRating.CreatedAt, ct);
            }, cancellationToken);
        }
        else
        {
            (savedRating, created) = await ratingRepository.AddAsync(rating, cancellationToken);
            if (created && socialActivityRecorder is not null)
                await socialActivityRecorder.RecordAsync(userId, SocialActivityType.RatedTvShow, savedRating.Id, tvShowId, "TvShow", savedRating.CreatedAt, cancellationToken);
        }
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
        return new RatingUpsertResult(RatingMapper.ToResult(savedRating), created);
    }

    public async Task<RatingUpsertResult> UpsertEpisodeRatingAsync(
        Guid episodeId,
        int score,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidateScore(score);
        await EnsureEpisodeExistsAsync(episodeId, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var existingRating = await ratingRepository.GetByUserAndEpisodeAsync(userId, episodeId, cancellationToken);
        if (existingRating is not null)
        {
            existingRating.UpdateScore(score, utcNow);
            if (applicationDbContext is not null && socialActivityRecorder is not null)
            {
                await applicationDbContext.ExecuteInTransactionAsync(async ct =>
                {
                    await ratingRepository.UpdateAsync(existingRating, ct);
                    await socialActivityRecorder.RecordAsync(
                        userId, SocialActivityType.RatedEpisode, existingRating.Id, episodeId, "Episode", utcNow, ct);
                }, cancellationToken);
            }
            else
            {
                await ratingRepository.UpdateAsync(existingRating, cancellationToken);
                if (socialActivityRecorder is not null)
                    await socialActivityRecorder.RecordAsync(
                        userId, SocialActivityType.RatedEpisode, existingRating.Id, episodeId, "Episode", utcNow, cancellationToken);
            }
            await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
            return new RatingUpsertResult(RatingMapper.ToResult(existingRating), Created: false);
        }

        var rating = Rating.CreateForEpisode(userId, episodeId, score, utcNow);
        Rating savedRating = rating;
        var created = false;
        if (applicationDbContext is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                (savedRating, created) = await ratingRepository.AddAsync(rating, ct);
                if (created && socialActivityRecorder is not null)
                    await socialActivityRecorder.RecordAsync(userId, SocialActivityType.RatedEpisode, savedRating.Id, episodeId, "Episode", savedRating.CreatedAt, ct);
            }, cancellationToken);
        }
        else
        {
            (savedRating, created) = await ratingRepository.AddAsync(rating, cancellationToken);
            if (created && socialActivityRecorder is not null)
                await socialActivityRecorder.RecordAsync(userId, SocialActivityType.RatedEpisode, savedRating.Id, episodeId, "Episode", savedRating.CreatedAt, cancellationToken);
        }
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
        return new RatingUpsertResult(RatingMapper.ToResult(savedRating), created);
    }

    public async Task DeleteMovieRatingAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var deleted = false;
        if (applicationDbContext is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                deleted = await ratingRepository.DeleteForMovieAsync(userId, movieId, ct);
                if (deleted && socialActivityRecorder is not null)
                    await socialActivityRecorder.RetractAsync(userId, SocialActivityType.RatedMovie, movieId, ct);
            }, cancellationToken);
        }
        else
        {
            deleted = await ratingRepository.DeleteForMovieAsync(userId, movieId, cancellationToken);
            if (deleted && socialActivityRecorder is not null)
                await socialActivityRecorder.RetractAsync(userId, SocialActivityType.RatedMovie, movieId, cancellationToken);
        }

        if (!deleted)
        {
            throw new NotFoundException("The requested rating was not found.");
        }
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
    }

    public async Task DeleteTvShowRatingAsync(Guid tvShowId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var deleted = false;
        if (applicationDbContext is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                deleted = await ratingRepository.DeleteForTvShowAsync(userId, tvShowId, ct);
                if (deleted && socialActivityRecorder is not null)
                    await socialActivityRecorder.RetractAsync(userId, SocialActivityType.RatedTvShow, tvShowId, ct);
            }, cancellationToken);
        }
        else
        {
            deleted = await ratingRepository.DeleteForTvShowAsync(userId, tvShowId, cancellationToken);
            if (deleted && socialActivityRecorder is not null)
                await socialActivityRecorder.RetractAsync(userId, SocialActivityType.RatedTvShow, tvShowId, cancellationToken);
        }

        if (!deleted)
        {
            throw new NotFoundException("The requested rating was not found.");
        }
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
    }

    public async Task DeleteEpisodeRatingAsync(Guid episodeId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var deleted = false;
        if (applicationDbContext is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                deleted = await ratingRepository.DeleteForEpisodeAsync(userId, episodeId, ct);
                if (deleted && socialActivityRecorder is not null)
                    await socialActivityRecorder.RetractAsync(userId, SocialActivityType.RatedEpisode, episodeId, ct);
            }, cancellationToken);
        }
        else
        {
            deleted = await ratingRepository.DeleteForEpisodeAsync(userId, episodeId, cancellationToken);
            if (deleted && socialActivityRecorder is not null)
                await socialActivityRecorder.RetractAsync(userId, SocialActivityType.RatedEpisode, episodeId, cancellationToken);
        }

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

    public async Task<RatingResult> GetCurrentUserEpisodeRatingAsync(
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var rating = await ratingRepository.GetByUserAndEpisodeAsync(userId, episodeId, cancellationToken);
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

    public async Task<RatingSummaryResult> GetEpisodeRatingSummaryAsync(
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        await EnsureEpisodeExistsAsync(episodeId, cancellationToken);
        return await ratingRepository.GetSummaryForEpisodeAsync(episodeId, cancellationToken);
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

    private async Task EnsureEpisodeExistsAsync(Guid episodeId, CancellationToken cancellationToken)
    {
        if (!await episodeRepository.ExistsAsync(episodeId, cancellationToken))
        {
            throw new NotFoundException("The requested episode was not found.");
        }
    }
}

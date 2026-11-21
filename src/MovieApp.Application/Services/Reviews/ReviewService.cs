using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Reviews;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Identity;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Reviews;

public sealed class ReviewService(
    ICurrentUser currentUser,
    IReviewRepository reviewRepository,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    IEpisodeRepository episodeRepository,
    IUserAnalyticsCacheInvalidator analyticsCacheInvalidator,
    IUserAvatarPresentationService avatarPresentationService,
    ISocialProfileService socialProfileService,
    ISocialActivityRecorder? socialActivityRecorder = null,
    IApplicationDbContext? applicationDbContext = null,
    ISocialReviewEngagementLookup? socialReviewEngagementLookup = null) : IReviewService
{
    public async Task<ReviewResult> CreateMovieReviewAsync(
        Guid movieId,
        string content,
        string authoringLocale,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidateContent(content);
        await EnsureMovieExistsAsync(movieId, cancellationToken);

        if (await reviewRepository.ExistsForMovieAsync(userId, movieId, cancellationToken))
        {
            throw new ConflictException("A review for this movie already exists.");
        }

        var review = Review.CreateForMovie(userId, movieId, content, authoringLocale, DateTime.UtcNow);
        if (applicationDbContext is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                await reviewRepository.AddAsync(review, ct);
                if (socialActivityRecorder is not null)
                    await socialActivityRecorder.RecordAsync(userId, SocialActivityType.ReviewedMovie, review.Id, movieId, "Movie", review.CreatedAt, ct);
            }, cancellationToken);
        }
        else
        {
            await reviewRepository.AddAsync(review, cancellationToken);
            if (socialActivityRecorder is not null)
                await socialActivityRecorder.RecordAsync(userId, SocialActivityType.ReviewedMovie, review.Id, movieId, "Movie", review.CreatedAt, cancellationToken);
        }
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);

        var createdReview = await reviewRepository.GetByUserAndMovieAsync(userId, movieId, cancellationToken);
        return await MapReviewResultAsync(createdReview!, cancellationToken);
    }

    public async Task<ReviewResult> CreateTvShowReviewAsync(
        Guid tvShowId,
        string content,
        string authoringLocale,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidateContent(content);
        await EnsureTvShowExistsAsync(tvShowId, cancellationToken);

        if (await reviewRepository.ExistsForTvShowAsync(userId, tvShowId, cancellationToken))
        {
            throw new ConflictException("A review for this TV show already exists.");
        }

        var review = Review.CreateForTvShow(userId, tvShowId, content, authoringLocale, DateTime.UtcNow);
        if (applicationDbContext is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                await reviewRepository.AddAsync(review, ct);
                if (socialActivityRecorder is not null)
                    await socialActivityRecorder.RecordAsync(userId, SocialActivityType.ReviewedTvShow, review.Id, tvShowId, "TvShow", review.CreatedAt, ct);
            }, cancellationToken);
        }
        else
        {
            await reviewRepository.AddAsync(review, cancellationToken);
            if (socialActivityRecorder is not null)
                await socialActivityRecorder.RecordAsync(userId, SocialActivityType.ReviewedTvShow, review.Id, tvShowId, "TvShow", review.CreatedAt, cancellationToken);
        }
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);

        var createdReview = await reviewRepository.GetByUserAndTvShowAsync(userId, tvShowId, cancellationToken);
        return await MapReviewResultAsync(createdReview!, cancellationToken);
    }

    public async Task<ReviewResult> CreateEpisodeReviewAsync(
        Guid episodeId,
        string content,
        string authoringLocale,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidateContent(content);
        await EnsureEpisodeExistsAsync(episodeId, cancellationToken);

        if (await reviewRepository.ExistsForEpisodeAsync(userId, episodeId, cancellationToken))
        {
            throw new ConflictException("A review for this episode already exists.");
        }

        var review = Review.CreateForEpisode(userId, episodeId, content, authoringLocale, DateTime.UtcNow);
        if (applicationDbContext is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                await reviewRepository.AddAsync(review, ct);
                if (socialActivityRecorder is not null)
                    await socialActivityRecorder.RecordAsync(userId, SocialActivityType.ReviewedEpisode, review.Id, episodeId, "Episode", review.CreatedAt, ct);
            }, cancellationToken);
        }
        else
        {
            await reviewRepository.AddAsync(review, cancellationToken);
            if (socialActivityRecorder is not null)
                await socialActivityRecorder.RecordAsync(userId, SocialActivityType.ReviewedEpisode, review.Id, episodeId, "Episode", review.CreatedAt, cancellationToken);
        }
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);

        var createdReview = await reviewRepository.GetByUserAndEpisodeAsync(userId, episodeId, cancellationToken);
        return await MapReviewResultAsync(createdReview!, cancellationToken);
    }

    public async Task<ReviewResult> UpdateMovieReviewAsync(
        Guid movieId,
        string content,
        string authoringLocale,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidateContent(content);

        var review = await reviewRepository.GetTrackedByUserAndMovieAsync(userId, movieId, cancellationToken);
        if (review is null)
        {
            throw new NotFoundException("The requested review was not found.");
        }

        var utcNow = DateTime.UtcNow;
        review.UpdateContent(content, authoringLocale, utcNow);
        if (applicationDbContext is not null && socialActivityRecorder is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                await reviewRepository.UpdateAsync(review, ct);
                await socialActivityRecorder.RecordAsync(
                    userId, SocialActivityType.ReviewedMovie, review.Id, movieId, "Movie", utcNow, ct);
            }, cancellationToken);
        }
        else
        {
            await reviewRepository.UpdateAsync(review, cancellationToken);
            if (socialActivityRecorder is not null)
                await socialActivityRecorder.RecordAsync(
                    userId, SocialActivityType.ReviewedMovie, review.Id, movieId, "Movie", utcNow, cancellationToken);
        }
        return await MapReviewResultAsync(review, cancellationToken);
    }

    public async Task<ReviewResult> UpdateTvShowReviewAsync(
        Guid tvShowId,
        string content,
        string authoringLocale,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidateContent(content);

        var review = await reviewRepository.GetTrackedByUserAndTvShowAsync(userId, tvShowId, cancellationToken);
        if (review is null)
        {
            throw new NotFoundException("The requested review was not found.");
        }

        var utcNow = DateTime.UtcNow;
        review.UpdateContent(content, authoringLocale, utcNow);
        if (applicationDbContext is not null && socialActivityRecorder is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                await reviewRepository.UpdateAsync(review, ct);
                await socialActivityRecorder.RecordAsync(
                    userId, SocialActivityType.ReviewedTvShow, review.Id, tvShowId, "TvShow", utcNow, ct);
            }, cancellationToken);
        }
        else
        {
            await reviewRepository.UpdateAsync(review, cancellationToken);
            if (socialActivityRecorder is not null)
                await socialActivityRecorder.RecordAsync(
                    userId, SocialActivityType.ReviewedTvShow, review.Id, tvShowId, "TvShow", utcNow, cancellationToken);
        }
        return await MapReviewResultAsync(review, cancellationToken);
    }

    public async Task<ReviewResult> UpdateEpisodeReviewAsync(
        Guid episodeId,
        string content,
        string authoringLocale,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidateContent(content);

        var review = await reviewRepository.GetTrackedByUserAndEpisodeAsync(userId, episodeId, cancellationToken);
        if (review is null)
        {
            throw new NotFoundException("The requested review was not found.");
        }

        var utcNow = DateTime.UtcNow;
        review.UpdateContent(content, authoringLocale, utcNow);
        if (applicationDbContext is not null && socialActivityRecorder is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                await reviewRepository.UpdateAsync(review, ct);
                await socialActivityRecorder.RecordAsync(
                    userId, SocialActivityType.ReviewedEpisode, review.Id, episodeId, "Episode", utcNow, ct);
            }, cancellationToken);
        }
        else
        {
            await reviewRepository.UpdateAsync(review, cancellationToken);
            if (socialActivityRecorder is not null)
                await socialActivityRecorder.RecordAsync(
                    userId, SocialActivityType.ReviewedEpisode, review.Id, episodeId, "Episode", utcNow, cancellationToken);
        }
        return await MapReviewResultAsync(review, cancellationToken);
    }

    public async Task DeleteMovieReviewAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var deleted = false;
        if (applicationDbContext is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                deleted = await reviewRepository.DeleteForMovieAsync(userId, movieId, ct);
                if (deleted && socialActivityRecorder is not null)
                    await socialActivityRecorder.RetractAsync(userId, SocialActivityType.ReviewedMovie, movieId, ct);
            }, cancellationToken);
        }
        else
        {
            deleted = await reviewRepository.DeleteForMovieAsync(userId, movieId, cancellationToken);
            if (deleted && socialActivityRecorder is not null)
                await socialActivityRecorder.RetractAsync(userId, SocialActivityType.ReviewedMovie, movieId, cancellationToken);
        }

        if (!deleted)
        {
            throw new NotFoundException("The requested review was not found.");
        }
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
    }

    public async Task DeleteTvShowReviewAsync(Guid tvShowId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var deleted = false;
        if (applicationDbContext is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                deleted = await reviewRepository.DeleteForTvShowAsync(userId, tvShowId, ct);
                if (deleted && socialActivityRecorder is not null)
                    await socialActivityRecorder.RetractAsync(userId, SocialActivityType.ReviewedTvShow, tvShowId, ct);
            }, cancellationToken);
        }
        else
        {
            deleted = await reviewRepository.DeleteForTvShowAsync(userId, tvShowId, cancellationToken);
            if (deleted && socialActivityRecorder is not null)
                await socialActivityRecorder.RetractAsync(userId, SocialActivityType.ReviewedTvShow, tvShowId, cancellationToken);
        }

        if (!deleted)
        {
            throw new NotFoundException("The requested review was not found.");
        }
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
    }

    public async Task DeleteEpisodeReviewAsync(Guid episodeId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var deleted = false;
        if (applicationDbContext is not null)
        {
            await applicationDbContext.ExecuteInTransactionAsync(async ct =>
            {
                deleted = await reviewRepository.DeleteForEpisodeAsync(userId, episodeId, ct);
                if (deleted && socialActivityRecorder is not null)
                    await socialActivityRecorder.RetractAsync(userId, SocialActivityType.ReviewedEpisode, episodeId, ct);
            }, cancellationToken);
        }
        else
        {
            deleted = await reviewRepository.DeleteForEpisodeAsync(userId, episodeId, cancellationToken);
            if (deleted && socialActivityRecorder is not null)
                await socialActivityRecorder.RetractAsync(userId, SocialActivityType.ReviewedEpisode, episodeId, cancellationToken);
        }

        if (!deleted)
        {
            throw new NotFoundException("The requested review was not found.");
        }
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
    }

    public async Task<ReviewResult> GetCurrentUserMovieReviewAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var review = await reviewRepository.GetByUserAndMovieAsync(userId, movieId, cancellationToken);
        if (review is null)
        {
            throw new NotFoundException("The requested review was not found.");
        }

        return await MapReviewResultAsync(review, cancellationToken);
    }

    public async Task<ReviewResult> GetCurrentUserTvShowReviewAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var review = await reviewRepository.GetByUserAndTvShowAsync(userId, tvShowId, cancellationToken);
        if (review is null)
        {
            throw new NotFoundException("The requested review was not found.");
        }

        return await MapReviewResultAsync(review, cancellationToken);
    }

    public async Task<ReviewResult> GetCurrentUserEpisodeReviewAsync(
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var review = await reviewRepository.GetByUserAndEpisodeAsync(userId, episodeId, cancellationToken);
        if (review is null)
        {
            throw new NotFoundException("The requested review was not found.");
        }

        return await MapReviewResultAsync(review, cancellationToken);
    }

    public async Task<ReviewListPageResult> GetMovieReviewsAsync(
        Guid movieId,
        int page,
        int pageSize,
        ReviewListSort sort,
        int? ratingStars = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(page, pageSize);
        ValidateRatingStars(ratingStars);
        await EnsureMovieExistsAsync(movieId, cancellationToken);

        var (reviews, totalCount) = await reviewRepository.GetPublicReviewsForMovieAsync(
            movieId,
            page,
            pageSize,
            sort,
            ratingStars,
            cancellationToken);
        var reviewScoreDistribution = await reviewRepository.GetReviewScoreDistributionForMovieAsync(
            movieId,
            cancellationToken);

        return new ReviewListPageResult(
            await ToPaginatedResultAsync(reviews, page, pageSize, totalCount, cancellationToken),
            reviewScoreDistribution);
    }

    public async Task<UserReviewListPageResult> GetCurrentUserReviewsAsync(
        int page,
        int pageSize,
        SearchContentType contentType,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        return await GetUserReviewsCoreAsync(userId, page, pageSize, contentType, cancellationToken);
    }

    public async Task<UserReviewListPageResult> GetUserReviewsAsync(
        Guid targetUserId,
        int page,
        int pageSize,
        SearchContentType contentType,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId == Guid.Empty)
        {
            throw new NotFoundException("User not found.");
        }

        if (!await socialProfileService.CanViewReviewsAsync(targetUserId, cancellationToken))
        {
            throw new NotFoundException("User not found.");
        }

        return await GetUserReviewsCoreAsync(targetUserId, page, pageSize, contentType, cancellationToken);
    }

    private async Task<UserReviewListPageResult> GetUserReviewsCoreAsync(
        Guid userId,
        int page,
        int pageSize,
        SearchContentType contentType,
        CancellationToken cancellationToken)
    {
        ValidatePagination(page, pageSize);

        if (contentType is SearchContentType.Person)
        {
            throw new ValidationException("Invalid content type filter.");
        }

        var (reviews, totalCount) = await reviewRepository.GetUserReviewsAsync(
            userId,
            page,
            pageSize,
            contentType,
            cancellationToken);

        return new UserReviewListPageResult(
            ToUserReviewPaginatedResult(reviews, page, pageSize, totalCount));
    }

    public async Task<ReviewListPageResult> GetTvShowReviewsAsync(
        Guid tvShowId,
        int page,
        int pageSize,
        ReviewListSort sort,
        int? ratingStars = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(page, pageSize);
        ValidateRatingStars(ratingStars);
        await EnsureTvShowExistsAsync(tvShowId, cancellationToken);

        var (reviews, totalCount) = await reviewRepository.GetPublicReviewsForTvShowAsync(
            tvShowId,
            page,
            pageSize,
            sort,
            ratingStars,
            cancellationToken);
        var reviewScoreDistribution = await reviewRepository.GetReviewScoreDistributionForTvShowAsync(
            tvShowId,
            cancellationToken);

        return new ReviewListPageResult(
            await ToPaginatedResultAsync(reviews, page, pageSize, totalCount, cancellationToken),
            reviewScoreDistribution);
    }

    public async Task<ReviewListPageResult> GetEpisodeReviewsAsync(
        Guid episodeId,
        int page,
        int pageSize,
        ReviewListSort sort,
        int? ratingStars = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(page, pageSize);
        ValidateRatingStars(ratingStars);
        await EnsureEpisodeExistsAsync(episodeId, cancellationToken);

        var (reviews, totalCount) = await reviewRepository.GetPublicReviewsForEpisodeAsync(
            episodeId,
            page,
            pageSize,
            sort,
            ratingStars,
            cancellationToken);
        var reviewScoreDistribution = await reviewRepository.GetReviewScoreDistributionForEpisodeAsync(
            episodeId,
            cancellationToken);

        return new ReviewListPageResult(
            await ToPaginatedResultAsync(reviews, page, pageSize, totalCount, cancellationToken),
            reviewScoreDistribution);
    }

    private async Task<ReviewResult> MapReviewResultAsync(Review review, CancellationToken cancellationToken)
    {
        var avatar = await avatarPresentationService.GetForUserAsync(review.UserId, cancellationToken);
        var engagement = await LoadEngagementAsync([review.Id], cancellationToken);
        engagement.TryGetValue(review.Id, out var stats);
        return ReviewMapper.ToResult(
            review,
            userRating: null,
            avatar.EffectiveAvatarUrl,
            stats?.ActivityId,
            stats?.LikesCount ?? 0,
            stats?.CommentsCount ?? 0,
            stats?.IsLikedByMe ?? false);
    }

    private async Task<IReadOnlyDictionary<Guid, SocialReviewEngagement>> LoadEngagementAsync(
        IReadOnlyList<Guid> reviewIds,
        CancellationToken cancellationToken)
    {
        if (socialReviewEngagementLookup is null || reviewIds.Count == 0)
            return new Dictionary<Guid, SocialReviewEngagement>();

        Guid? viewerId = currentUser.IsAuthenticated && currentUser.UserId is Guid id && id != Guid.Empty
            ? id
            : null;
        return await socialReviewEngagementLookup.GetForReviewIdsAsync(reviewIds, viewerId, cancellationToken);
    }

    private static void ValidateRatingStars(int? ratingStars)
    {
        var validationResult = ReviewListValidator.ValidateRatingStars(ratingStars);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }
    }

    private static void ValidateContent(string content)
    {
        var validationResult = ReviewContentValidator.Validate(content);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }
    }

    private static void ValidatePagination(int page, int pageSize)
    {
        var validationResult = SearchPaginationValidator.Validate(page, pageSize);
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

    private async Task<PaginatedResult<ReviewResult>> ToPaginatedResultAsync(
        IReadOnlyList<PublicReviewListItem> reviews,
        int page,
        int pageSize,
        int totalCount,
        CancellationToken cancellationToken)
    {
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var userIds = reviews
            .Select(item => item.Review.UserId)
            .Distinct()
            .ToList();
        var avatars = await avatarPresentationService.GetForUsersAsync(userIds, cancellationToken);
        var reviewIds = reviews.Select(item => item.Review.Id).ToList();
        var engagement = await LoadEngagementAsync(reviewIds, cancellationToken);
        var items = reviews
            .Select(item =>
            {
                var effectiveAvatarUrl = avatars.TryGetValue(item.Review.UserId, out var avatar)
                    ? avatar.EffectiveAvatarUrl
                    : null;
                engagement.TryGetValue(item.Review.Id, out var stats);
                return ReviewMapper.ToResult(
                    item.Review,
                    item.UserRating,
                    effectiveAvatarUrl,
                    stats?.ActivityId,
                    stats?.LikesCount ?? 0,
                    stats?.CommentsCount ?? 0,
                    stats?.IsLikedByMe ?? false);
            })
            .ToList();
        return new PaginatedResult<ReviewResult>(items, page, pageSize, totalCount, totalPages);
    }

    private static PaginatedResult<UserReviewListItemResult> ToUserReviewPaginatedResult(
        IReadOnlyList<UserReviewCatalogListItem> reviews,
        int page,
        int pageSize,
        int totalCount)
    {
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var items = reviews
            .Select(ReviewMapper.ToUserReviewListItemResult)
            .ToList();

        return new PaginatedResult<UserReviewListItemResult>(items, page, pageSize, totalCount, totalPages);
    }
}

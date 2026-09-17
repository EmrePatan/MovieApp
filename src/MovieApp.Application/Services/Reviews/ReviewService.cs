using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Reviews;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Reviews;

public sealed class ReviewService(
    ICurrentUser currentUser,
    IReviewRepository reviewRepository,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    IUserAnalyticsCacheInvalidator analyticsCacheInvalidator) : IReviewService
{
    public async Task<ReviewResult> CreateMovieReviewAsync(
        Guid movieId,
        string content,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidateContent(content);
        await EnsureMovieExistsAsync(movieId, cancellationToken);

        if (await reviewRepository.ExistsForMovieAsync(userId, movieId, cancellationToken))
        {
            throw new ConflictException("A review for this movie already exists.");
        }

        var review = Review.CreateForMovie(userId, movieId, content, DateTime.UtcNow);
        await reviewRepository.AddAsync(review, cancellationToken);
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);

        var createdReview = await reviewRepository.GetByUserAndMovieAsync(userId, movieId, cancellationToken);
        return ReviewMapper.ToResult(createdReview!);
    }

    public async Task<ReviewResult> CreateTvShowReviewAsync(
        Guid tvShowId,
        string content,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidateContent(content);
        await EnsureTvShowExistsAsync(tvShowId, cancellationToken);

        if (await reviewRepository.ExistsForTvShowAsync(userId, tvShowId, cancellationToken))
        {
            throw new ConflictException("A review for this TV show already exists.");
        }

        var review = Review.CreateForTvShow(userId, tvShowId, content, DateTime.UtcNow);
        await reviewRepository.AddAsync(review, cancellationToken);
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);

        var createdReview = await reviewRepository.GetByUserAndTvShowAsync(userId, tvShowId, cancellationToken);
        return ReviewMapper.ToResult(createdReview!);
    }

    public async Task<ReviewResult> UpdateMovieReviewAsync(
        Guid movieId,
        string content,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidateContent(content);

        var review = await reviewRepository.GetTrackedByUserAndMovieAsync(userId, movieId, cancellationToken);
        if (review is null)
        {
            throw new NotFoundException("The requested review was not found.");
        }

        review.UpdateContent(content, DateTime.UtcNow);
        await reviewRepository.UpdateAsync(review, cancellationToken);
        return ReviewMapper.ToResult(review);
    }

    public async Task<ReviewResult> UpdateTvShowReviewAsync(
        Guid tvShowId,
        string content,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidateContent(content);

        var review = await reviewRepository.GetTrackedByUserAndTvShowAsync(userId, tvShowId, cancellationToken);
        if (review is null)
        {
            throw new NotFoundException("The requested review was not found.");
        }

        review.UpdateContent(content, DateTime.UtcNow);
        await reviewRepository.UpdateAsync(review, cancellationToken);
        return ReviewMapper.ToResult(review);
    }

    public async Task DeleteMovieReviewAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var deleted = await reviewRepository.DeleteForMovieAsync(userId, movieId, cancellationToken);
        if (!deleted)
        {
            throw new NotFoundException("The requested review was not found.");
        }

        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
    }

    public async Task DeleteTvShowReviewAsync(Guid tvShowId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var deleted = await reviewRepository.DeleteForTvShowAsync(userId, tvShowId, cancellationToken);
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

        return ReviewMapper.ToResult(review);
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

        return ReviewMapper.ToResult(review);
    }

    public async Task<PaginatedResult<ReviewResult>> GetMovieReviewsAsync(
        Guid movieId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(page, pageSize);
        await EnsureMovieExistsAsync(movieId, cancellationToken);

        var (reviews, totalCount) = await reviewRepository.GetPublicReviewsForMovieAsync(
            movieId,
            page,
            pageSize,
            cancellationToken);

        return ToPaginatedResult(reviews, page, pageSize, totalCount);
    }

    public async Task<PaginatedResult<ReviewResult>> GetTvShowReviewsAsync(
        Guid tvShowId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(page, pageSize);
        await EnsureTvShowExistsAsync(tvShowId, cancellationToken);

        var (reviews, totalCount) = await reviewRepository.GetPublicReviewsForTvShowAsync(
            tvShowId,
            page,
            pageSize,
            cancellationToken);

        return ToPaginatedResult(reviews, page, pageSize, totalCount);
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

    private static PaginatedResult<ReviewResult> ToPaginatedResult(
        IReadOnlyList<Review> reviews,
        int page,
        int pageSize,
        int totalCount)
    {
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var items = reviews.Select(ReviewMapper.ToResult).ToList();
        return new PaginatedResult<ReviewResult>(items, page, pageSize, totalCount, totalPages);
    }
}

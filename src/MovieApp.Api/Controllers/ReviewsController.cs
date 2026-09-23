using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MovieApp.Api.Localization;
using MovieApp.Api.Mapping;
using MovieApp.Api.RateLimiting;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Reviews;
using MovieApp.Application.Services.Reviews;
using MovieApp.Application.Validation;
using MovieApp.Contracts.Reviews;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/reviews")]
public sealed class ReviewsController(
    IReviewService reviewService,
    IReviewTranslationService reviewTranslationService) : ControllerBase
{
    [Authorize]
    [HttpPost("movies/{movieId:guid}")]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewResponse>> CreateMovieReview(
        Guid movieId,
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var authoringLocale = ResolveAuthoringLocale(request.AuthoringLocale);
            var result = await reviewService.CreateMovieReviewAsync(
                movieId,
                request.Content,
                authoringLocale,
                cancellationToken);
            return Created(string.Empty, ReviewContractMapper.ToResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid review request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Movie not found.",
                exception.Message));
        }
        catch (ConflictException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Review conflict.",
                exception.Message));
        }
    }

    [Authorize]
    [HttpPost("tvshows/{tvShowId:guid}")]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewResponse>> CreateTvShowReview(
        Guid tvShowId,
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var authoringLocale = ResolveAuthoringLocale(request.AuthoringLocale);
            var result = await reviewService.CreateTvShowReviewAsync(
                tvShowId,
                request.Content,
                authoringLocale,
                cancellationToken);
            return Created(string.Empty, ReviewContractMapper.ToResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid review request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "TV show not found.",
                exception.Message));
        }
        catch (ConflictException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Review conflict.",
                exception.Message));
        }
    }

    [Authorize]
    [HttpPut("movies/{movieId:guid}")]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewResponse>> UpdateMovieReview(
        Guid movieId,
        [FromBody] UpdateReviewRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var authoringLocale = ResolveAuthoringLocale(request.AuthoringLocale);
            var result = await reviewService.UpdateMovieReviewAsync(
                movieId,
                request.Content,
                authoringLocale,
                cancellationToken);
            return Ok(ReviewContractMapper.ToResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid review request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Review not found.",
                exception.Message));
        }
    }

    [Authorize]
    [HttpPut("tvshows/{tvShowId:guid}")]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewResponse>> UpdateTvShowReview(
        Guid tvShowId,
        [FromBody] UpdateReviewRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var authoringLocale = ResolveAuthoringLocale(request.AuthoringLocale);
            var result = await reviewService.UpdateTvShowReviewAsync(
                tvShowId,
                request.Content,
                authoringLocale,
                cancellationToken);
            return Ok(ReviewContractMapper.ToResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid review request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Review not found.",
                exception.Message));
        }
    }

    [Authorize]
    [HttpDelete("movies/{movieId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMovieReview(Guid movieId, CancellationToken cancellationToken)
    {
        try
        {
            await reviewService.DeleteMovieReviewAsync(movieId, cancellationToken);
            return NoContent();
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Review not found.",
                exception.Message));
        }
    }

    [Authorize]
    [HttpDelete("tvshows/{tvShowId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTvShowReview(Guid tvShowId, CancellationToken cancellationToken)
    {
        try
        {
            await reviewService.DeleteTvShowReviewAsync(tvShowId, cancellationToken);
            return NoContent();
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Review not found.",
                exception.Message));
        }
    }

    [Authorize]
    [HttpGet("movies/{movieId:guid}/me")]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewResponse>> GetCurrentUserMovieReview(
        Guid movieId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await reviewService.GetCurrentUserMovieReviewAsync(movieId, cancellationToken);
            return Ok(ReviewContractMapper.ToResponse(result));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Review not found.",
                exception.Message));
        }
    }

    [Authorize]
    [HttpGet("tvshows/{tvShowId:guid}/me")]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewResponse>> GetCurrentUserTvShowReview(
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await reviewService.GetCurrentUserTvShowReviewAsync(tvShowId, cancellationToken);
            return Ok(ReviewContractMapper.ToResponse(result));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Review not found.",
                exception.Message));
        }
    }

    [AllowAnonymous]
    [HttpGet("movies/{movieId:guid}")]
    [ProducesResponseType(typeof(ReviewListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewListResponse>> GetMovieReviews(
        Guid movieId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? sort,
        [FromQuery] int? ratingStars,
        CancellationToken cancellationToken)
    {
        try
        {
            var sortValidation = ReviewListValidator.ValidateSort(sort);
            if (!sortValidation.IsValid)
            {
                throw new ValidationException(sortValidation.ErrorMessage!);
            }

            var ratingStarsValidation = ReviewListValidator.ValidateRatingStars(ratingStars);
            if (!ratingStarsValidation.IsValid)
            {
                throw new ValidationException(ratingStarsValidation.ErrorMessage!);
            }

            _ = ReviewListValidator.TryParseSort(sort, out var parsedSort);

            var result = await reviewService.GetMovieReviewsAsync(
                movieId,
                page ?? SearchPaginationDefaults.DefaultPage,
                pageSize ?? SearchPaginationDefaults.DefaultPageSize,
                parsedSort,
                ratingStars,
                cancellationToken);

            return Ok(ReviewContractMapper.ToListResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid review list request.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Movie not found.",
                exception.Message));
        }
    }

    [AllowAnonymous]
    [HttpGet("tvshows/{tvShowId:guid}")]
    [ProducesResponseType(typeof(ReviewListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewListResponse>> GetTvShowReviews(
        Guid tvShowId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? sort,
        [FromQuery] int? ratingStars,
        CancellationToken cancellationToken)
    {
        try
        {
            var sortValidation = ReviewListValidator.ValidateSort(sort);
            if (!sortValidation.IsValid)
            {
                throw new ValidationException(sortValidation.ErrorMessage!);
            }

            var ratingStarsValidation = ReviewListValidator.ValidateRatingStars(ratingStars);
            if (!ratingStarsValidation.IsValid)
            {
                throw new ValidationException(ratingStarsValidation.ErrorMessage!);
            }

            _ = ReviewListValidator.TryParseSort(sort, out var parsedSort);

            var result = await reviewService.GetTvShowReviewsAsync(
                tvShowId,
                page ?? SearchPaginationDefaults.DefaultPage,
                pageSize ?? SearchPaginationDefaults.DefaultPageSize,
                parsedSort,
                ratingStars,
                cancellationToken);

            return Ok(ReviewContractMapper.ToListResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid review list request.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "TV show not found.",
                exception.Message));
        }
    }

    [AllowAnonymous]
    [EnableRateLimiting(ReviewTranslationRateLimitPolicies.Translation)]
    [HttpPost("{reviewId:guid}/translation")]
    [ProducesResponseType(typeof(ReviewTranslationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ReviewTranslationResponse>> TranslateReview(
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        try
        {
            var targetLocale = Request.ResolveContentLocale();
            var result = await reviewTranslationService.TranslateAsync(
                reviewId,
                targetLocale,
                cancellationToken);

            return Ok(ReviewContractMapper.ToTranslationResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid review translation request.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Review not found.",
                exception.Message));
        }
        catch (ReviewTranslationUnavailableException exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateProblemDetails(
                    StatusCodes.Status503ServiceUnavailable,
                    "Review translation unavailable.",
                    exception.Message));
        }
    }

    private string ResolveAuthoringLocale(string? requestedAuthoringLocale)
    {
        var resolved = string.IsNullOrWhiteSpace(requestedAuthoringLocale)
            ? Request.ResolveContentLocale()
            : ContentLocaleResolver.Normalize(requestedAuthoringLocale);

        return SupportedContentLocales.Normalize(resolved);
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail) =>
        new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Ratings;
using MovieApp.Contracts.Ratings;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/ratings")]
public sealed class RatingsController(IRatingService ratingService) : ControllerBase
{
    [Authorize]
    [HttpPost("movies/{movieId:guid}")]
    [ProducesResponseType(typeof(RatingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RatingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RatingResponse>> UpsertMovieRating(
        Guid movieId,
        [FromBody] CreateRatingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ratingService.UpsertMovieRatingAsync(movieId, request.Score, cancellationToken);
            var response = RatingContractMapper.ToResponse(result.Rating);

            return result.Created
                ? Created(string.Empty, response)
                : Ok(response);
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid rating request.",
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
    }

    [Authorize]
    [HttpPost("tvshows/{tvShowId:guid}")]
    [ProducesResponseType(typeof(RatingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RatingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RatingResponse>> UpsertTvShowRating(
        Guid tvShowId,
        [FromBody] CreateRatingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ratingService.UpsertTvShowRatingAsync(tvShowId, request.Score, cancellationToken);
            var response = RatingContractMapper.ToResponse(result.Rating);

            return result.Created
                ? Created(string.Empty, response)
                : Ok(response);
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid rating request.",
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
    }

    [Authorize]
    [HttpDelete("movies/{movieId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMovieRating(Guid movieId, CancellationToken cancellationToken)
    {
        try
        {
            await ratingService.DeleteMovieRatingAsync(movieId, cancellationToken);
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
                "Rating not found.",
                exception.Message));
        }
    }

    [Authorize]
    [HttpDelete("tvshows/{tvShowId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTvShowRating(Guid tvShowId, CancellationToken cancellationToken)
    {
        try
        {
            await ratingService.DeleteTvShowRatingAsync(tvShowId, cancellationToken);
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
                "Rating not found.",
                exception.Message));
        }
    }

    [Authorize]
    [HttpGet("movies/{movieId:guid}/me")]
    [ProducesResponseType(typeof(RatingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RatingResponse>> GetCurrentUserMovieRating(
        Guid movieId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ratingService.GetCurrentUserMovieRatingAsync(movieId, cancellationToken);
            return Ok(RatingContractMapper.ToResponse(result));
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
                "Rating not found.",
                exception.Message));
        }
    }

    [Authorize]
    [HttpGet("tvshows/{tvShowId:guid}/me")]
    [ProducesResponseType(typeof(RatingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RatingResponse>> GetCurrentUserTvShowRating(
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ratingService.GetCurrentUserTvShowRatingAsync(tvShowId, cancellationToken);
            return Ok(RatingContractMapper.ToResponse(result));
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
                "Rating not found.",
                exception.Message));
        }
    }

    [AllowAnonymous]
    [HttpGet("movies/{movieId:guid}")]
    [ProducesResponseType(typeof(RatingSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RatingSummaryResponse>> GetMovieRatingSummary(
        Guid movieId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ratingService.GetMovieRatingSummaryAsync(movieId, cancellationToken);
            return Ok(RatingContractMapper.ToSummaryResponse(result));
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
    [ProducesResponseType(typeof(RatingSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RatingSummaryResponse>> GetTvShowRatingSummary(
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ratingService.GetTvShowRatingSummaryAsync(tvShowId, cancellationToken);
            return Ok(RatingContractMapper.ToSummaryResponse(result));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "TV show not found.",
                exception.Message));
        }
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail) =>
        new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
}

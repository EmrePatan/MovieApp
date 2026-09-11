using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Application.Validation;
using MovieApp.Contracts.Recommendations;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
public sealed class RecommendationsController(IRecommendationService recommendationService) : ControllerBase
{
    [HttpGet("movies/{movieId:guid}/similar")]
    [ProducesResponseType(typeof(RecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecommendationResponse>> GetSimilarMovies(
        Guid movieId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = BuildSimilarCriteria(page, pageSize);
            var result = await recommendationService.GetSimilarMoviesAsync(movieId, criteria, cancellationToken);
            return Ok(RecommendationContractMapper.ToRecommendationResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid recommendation request.",
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

    [HttpGet("tvshows/{tvShowId:guid}/similar")]
    [ProducesResponseType(typeof(RecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecommendationResponse>> GetSimilarTvShows(
        Guid tvShowId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = BuildSimilarCriteria(page, pageSize);
            var result = await recommendationService.GetSimilarTvShowsAsync(tvShowId, criteria, cancellationToken);
            return Ok(RecommendationContractMapper.ToRecommendationResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid recommendation request.",
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
    [HttpGet]
    [ProducesResponseType(typeof(RecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RecommendationResponse>> GetRecommendations(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? type,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = BuildRecommendationCriteria(page, pageSize, type);
            var result = await recommendationService.GetRecommendationsForCurrentUserAsync(criteria, cancellationToken);
            return Ok(RecommendationContractMapper.ToRecommendationResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid recommendation request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
    }

    [Authorize]
    [HttpGet("home")]
    [ProducesResponseType(typeof(RecommendationHomeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RecommendationHomeResponse>> GetHomeRecommendations(
        CancellationToken cancellationToken)
    {
        try
        {
            var sections = await recommendationService.GetHomeRecommendationsForCurrentUserAsync(cancellationToken);
            return Ok(RecommendationContractMapper.ToRecommendationHomeResponse(sections));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
    }

    private static SimilarContentCriteria BuildSimilarCriteria(int? page, int? pageSize) =>
        new(
            page ?? SearchPaginationDefaults.DefaultPage,
            pageSize ?? SearchPaginationDefaults.DefaultPageSize);

    private static RecommendationCriteria BuildRecommendationCriteria(int? page, int? pageSize, string? type)
    {
        var typeValidation = RecommendationValidator.ValidateType(type);
        if (!typeValidation.IsValid)
        {
            throw new ValidationException(typeValidation.ErrorMessage!);
        }

        _ = RecommendationValidator.TryParseType(type, out var contentType);

        return new RecommendationCriteria(
            contentType,
            page ?? SearchPaginationDefaults.DefaultPage,
            pageSize ?? SearchPaginationDefaults.DefaultPageSize);
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail) =>
        new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
}

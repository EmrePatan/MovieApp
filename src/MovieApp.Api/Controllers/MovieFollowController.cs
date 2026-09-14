using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MovieApp.Api.Mapping;
using MovieApp.Api.RateLimiting;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.MovieFollows;
using MovieApp.Application.Services.MovieFollows;
using MovieApp.Contracts.MovieFollows;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/movies/{movieId:guid}/follow")]
public sealed class MovieFollowController(
    IGetMovieFollowStatusService getMovieFollowStatusService,
    IUpsertMovieFollowService upsertMovieFollowService,
    IRemoveMovieFollowService removeMovieFollowService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(MovieFollowStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MovieFollowStatusResponse>> GetFollowStatus(
        Guid movieId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await getMovieFollowStatusService.GetAsync(movieId, cancellationToken);
            return Ok(MovieFollowContractMapper.ToStatusResponse(result));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
    }

    [HttpPut]
    [EnableRateLimiting(TvShowFollowRateLimitPolicies.Mutation)]
    [ProducesResponseType(typeof(MovieFollowStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MovieFollowStatusResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MovieFollowStatusResponse>> UpsertFollow(
        Guid movieId,
        [FromBody] UpsertMovieFollowRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var (mutation, status) = await upsertMovieFollowService.UpsertAsync(movieId, cancellationToken);
            var response = MovieFollowContractMapper.ToStatusResponse(status);

            return mutation == MovieFollowMutationResult.Created
                ? StatusCode(StatusCodes.Status201Created, response)
                : Ok(response);
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid follow request.",
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
                "Follow conflict.",
                exception.Message));
        }
    }

    [HttpDelete]
    [EnableRateLimiting(TvShowFollowRateLimitPolicies.Mutation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RemoveFollow(Guid movieId, CancellationToken cancellationToken)
    {
        try
        {
            await removeMovieFollowService.RemoveAsync(movieId, cancellationToken);
            return NoContent();
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
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

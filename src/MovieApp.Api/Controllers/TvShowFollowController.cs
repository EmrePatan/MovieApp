using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MovieApp.Api.Mapping;
using MovieApp.Api.RateLimiting;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.TvShowFollows;
using MovieApp.Application.Services.TvShowFollows;
using MovieApp.Contracts.TvShowFollows;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/tvshows/{tvShowId:guid}/follow")]
public sealed class TvShowFollowController(
    IGetTvShowFollowStatusService getTvShowFollowStatusService,
    IUpsertTvShowFollowService upsertTvShowFollowService,
    IRemoveTvShowFollowService removeTvShowFollowService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(TvShowFollowStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TvShowFollowStatusResponse>> GetFollowStatus(
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await getTvShowFollowStatusService.GetAsync(tvShowId, cancellationToken);
            return Ok(TvShowFollowContractMapper.ToStatusResponse(result));
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
    [ProducesResponseType(typeof(TvShowFollowStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TvShowFollowStatusResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TvShowFollowStatusResponse>> UpsertFollow(
        Guid tvShowId,
        [FromBody] UpsertTvShowFollowRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var preferences = TvShowFollowContractMapper.ToPreferencesUpdate(
                request ?? new UpsertTvShowFollowRequest(null, null));

            var (mutation, status) = await upsertTvShowFollowService.UpsertAsync(
                tvShowId,
                preferences,
                cancellationToken);

            var response = TvShowFollowContractMapper.ToStatusResponse(status);

            return mutation == TvShowFollowMutationResult.Created
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
                "TV show not found.",
                exception.Message));
        }
        catch (ConflictException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Follow conflict.",
                exception.Message));
        }
        catch (TvShowFollowBaselineException exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateProblemDetails(
                    StatusCodes.Status503ServiceUnavailable,
                    "Follow baseline unavailable.",
                    exception.Message));
        }
    }

    [HttpDelete]
    [EnableRateLimiting(TvShowFollowRateLimitPolicies.Mutation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RemoveFollow(Guid tvShowId, CancellationToken cancellationToken)
    {
        try
        {
            await removeTvShowFollowService.RemoveAsync(tvShowId, cancellationToken);
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

[Authorize]
[ApiController]
[Route("api/follows/tvshows")]
public sealed class TvShowFollowsController(IGetTvShowFollowsService getTvShowFollowsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(TvShowFollowsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TvShowFollowsResponse>> GetFollowedTvShows(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await getTvShowFollowsService.GetAsync(
                page ?? SearchPaginationDefaults.DefaultPage,
                pageSize ?? SearchPaginationDefaults.DefaultPageSize,
                cancellationToken);

            return Ok(TvShowFollowContractMapper.ToListResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid pagination request.",
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

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail) =>
        new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
}

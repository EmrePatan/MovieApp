using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Watchlists;
using MovieApp.Application.Services.Watchlists;
using ContractsCreateWatchlistRequest = MovieApp.Contracts.Watchlists.CreateWatchlistRequest;
using ContractsUpdateWatchlistRequest = MovieApp.Contracts.Watchlists.UpdateWatchlistRequest;
using MovieApp.Contracts.Watchlists;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/watchlists")]
public sealed class WatchlistsController(
    ICreateWatchlistService createWatchlistService,
    IUpdateWatchlistService updateWatchlistService,
    IDeleteWatchlistService deleteWatchlistService,
    IGetWatchlistsService getWatchlistsService,
    IGetWatchlistService getWatchlistService,
    IAddMovieToWatchlistService addMovieToWatchlistService,
    IRemoveMovieFromWatchlistService removeMovieFromWatchlistService,
    IAddTvShowToWatchlistService addTvShowToWatchlistService,
    IRemoveTvShowFromWatchlistService removeTvShowFromWatchlistService,
    IGetWatchlistItemsService getWatchlistItemsService,
    IGetWatchlistMembershipService getWatchlistMembershipService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(WatchlistSummaryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WatchlistSummaryResponse>> CreateWatchlist(
        [FromBody] ContractsCreateWatchlistRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await createWatchlistService.CreateAsync(
                WatchlistContractMapper.ToCreateWatchlistRequest(request),
                cancellationToken);

            return Created(string.Empty, WatchlistContractMapper.ToSummaryResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid watchlist request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (ConflictException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Watchlist conflict.",
                exception.Message));
        }
    }

    [HttpGet("membership")]
    [ProducesResponseType(typeof(WatchlistMembershipResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WatchlistMembershipResponse>> GetMembership(
        [FromQuery] string mediaType,
        [FromQuery] Guid contentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = string.Equals(mediaType, "movie", StringComparison.OrdinalIgnoreCase)
                ? await getWatchlistMembershipService.GetMovieMembershipAsync(contentId, cancellationToken)
                : string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase)
                    ? await getWatchlistMembershipService.GetTvShowMembershipAsync(contentId, cancellationToken)
                    : throw new ValidationException("Media type must be 'movie' or 'tv'.");

            return Ok(WatchlistContractMapper.ToMembershipResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid membership request.",
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

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WatchlistSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<WatchlistSummaryResponse>>> GetWatchlists(
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await getWatchlistsService.GetAsync(cancellationToken);
            return Ok(results.Select(WatchlistContractMapper.ToSummaryResponse).ToList());
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
    }

    [HttpGet("{watchlistId:guid}")]
    [ProducesResponseType(typeof(WatchlistDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WatchlistDetailResponse>> GetWatchlist(
        Guid watchlistId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await getWatchlistService.GetAsync(watchlistId, cancellationToken);
            return Ok(WatchlistContractMapper.ToDetailResponse(result));
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
                "Watchlist not found.",
                exception.Message));
        }
    }

    [HttpPatch("{watchlistId:guid}")]
    [ProducesResponseType(typeof(WatchlistSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WatchlistSummaryResponse>> UpdateWatchlist(
        Guid watchlistId,
        [FromBody] ContractsUpdateWatchlistRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await updateWatchlistService.UpdateAsync(
                watchlistId,
                WatchlistContractMapper.ToUpdateWatchlistRequest(request),
                cancellationToken);

            return Ok(WatchlistContractMapper.ToSummaryResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid watchlist request.",
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
                "Watchlist not found.",
                exception.Message));
        }
        catch (ConflictException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Watchlist conflict.",
                exception.Message));
        }
    }

    [HttpDelete("{watchlistId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWatchlist(
        Guid watchlistId,
        CancellationToken cancellationToken)
    {
        try
        {
            await deleteWatchlistService.DeleteAsync(watchlistId, cancellationToken);
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
                "Watchlist not found.",
                exception.Message));
        }
    }

    [HttpPost("{watchlistId:guid}/movies/{movieId:guid}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMovie(
        Guid watchlistId,
        Guid movieId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await addMovieToWatchlistService.AddAsync(watchlistId, movieId, cancellationToken);
            return result == WatchlistItemMutationResult.Created
                ? StatusCode(StatusCodes.Status201Created)
                : Ok();
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
                "Resource not found.",
                exception.Message));
        }
    }

    [HttpDelete("{watchlistId:guid}/movies/{movieId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMovie(
        Guid watchlistId,
        Guid movieId,
        CancellationToken cancellationToken)
    {
        try
        {
            await removeMovieFromWatchlistService.RemoveAsync(watchlistId, movieId, cancellationToken);
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
                "Watchlist not found.",
                exception.Message));
        }
    }

    [HttpPost("{watchlistId:guid}/tvshows/{tvShowId:guid}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddTvShow(
        Guid watchlistId,
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await addTvShowToWatchlistService.AddAsync(watchlistId, tvShowId, cancellationToken);
            return result == WatchlistItemMutationResult.Created
                ? StatusCode(StatusCodes.Status201Created)
                : Ok();
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
                "Resource not found.",
                exception.Message));
        }
    }

    [HttpDelete("{watchlistId:guid}/tvshows/{tvShowId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTvShow(
        Guid watchlistId,
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        try
        {
            await removeTvShowFromWatchlistService.RemoveAsync(watchlistId, tvShowId, cancellationToken);
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
                "Watchlist not found.",
                exception.Message));
        }
    }

    [HttpGet("{watchlistId:guid}/items")]
    [ProducesResponseType(typeof(WatchlistItemsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WatchlistItemsResponse>> GetWatchlistItems(
        Guid watchlistId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await getWatchlistItemsService.GetAsync(
                watchlistId,
                page ?? SearchPaginationDefaults.DefaultPage,
                pageSize ?? SearchPaginationDefaults.DefaultPageSize,
                cancellationToken);

            return Ok(WatchlistContractMapper.ToItemsResponse(result));
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
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Watchlist not found.",
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

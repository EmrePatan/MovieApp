using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Favorites;
using MovieApp.Application.Services.Favorites;
using MovieApp.Contracts.Favorites;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/favorites")]
public sealed class FavoritesController(
    IAddMovieFavoriteService addMovieFavoriteService,
    IRemoveMovieFavoriteService removeMovieFavoriteService,
    IAddTvShowFavoriteService addTvShowFavoriteService,
    IRemoveTvShowFavoriteService removeTvShowFavoriteService,
    IGetFavoritesService getFavoritesService,
    IGetFavoriteStatusService getFavoriteStatusService) : ControllerBase
{
    [HttpPost("movies/{movieId:guid}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMovieFavorite(
        Guid movieId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await addMovieFavoriteService.AddAsync(movieId, cancellationToken);
            return result == FavoriteMutationResult.Created
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
                "Movie not found.",
                exception.Message));
        }
    }

    [HttpDelete("movies/{movieId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveMovieFavorite(
        Guid movieId,
        CancellationToken cancellationToken)
    {
        try
        {
            await removeMovieFavoriteService.RemoveAsync(movieId, cancellationToken);
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

    [HttpPost("tvshows/{tvShowId:guid}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddTvShowFavorite(
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await addTvShowFavoriteService.AddAsync(tvShowId, cancellationToken);
            return result == FavoriteMutationResult.Created
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
                "TV show not found.",
                exception.Message));
        }
    }

    [HttpDelete("tvshows/{tvShowId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveTvShowFavorite(
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        try
        {
            await removeTvShowFavoriteService.RemoveAsync(tvShowId, cancellationToken);
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

    [HttpGet("movies/{movieId:guid}/status")]
    [ProducesResponseType(typeof(FavoriteStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FavoriteStatusResponse>> GetMovieFavoriteStatus(
        Guid movieId,
        CancellationToken cancellationToken)
    {
        try
        {
            var isFavorited = await getFavoriteStatusService.GetMovieStatusAsync(movieId, cancellationToken);
            return Ok(new FavoriteStatusResponse(isFavorited));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
    }

    [HttpGet("tvshows/{tvShowId:guid}/status")]
    [ProducesResponseType(typeof(FavoriteStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FavoriteStatusResponse>> GetTvShowFavoriteStatus(
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        try
        {
            var isFavorited = await getFavoriteStatusService.GetTvShowStatusAsync(tvShowId, cancellationToken);
            return Ok(new FavoriteStatusResponse(isFavorited));
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
    [ProducesResponseType(typeof(FavoritesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FavoritesResponse>> GetFavorites(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await getFavoritesService.GetAsync(
                page ?? SearchPaginationDefaults.DefaultPage,
                pageSize ?? SearchPaginationDefaults.DefaultPageSize,
                cancellationToken);

            return Ok(FavoriteContractMapper.ToFavoritesResponse(result));
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

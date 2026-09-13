using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Services.WatchHistory;
using MovieApp.Contracts.WatchHistory;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/watch-history")]
public sealed class WatchHistoryController(IWatchHistoryService watchHistoryService) : ControllerBase
{
    [HttpPost("movies/{movieId:guid}")]
    [ProducesResponseType(typeof(WatchMovieResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(WatchMovieResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkMovieWatched(Guid movieId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchHistoryService.MarkMovieWatchedAsync(movieId, cancellationToken);
            var response = WatchHistoryContractMapper.ToWatchMovieResponse(movieId, result);

            return result.Created
                ? StatusCode(StatusCodes.Status201Created, response)
                : Ok(response);
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
    public async Task<IActionResult> UnmarkMovieWatched(Guid movieId, CancellationToken cancellationToken)
    {
        try
        {
            await watchHistoryService.UnmarkMovieWatchedAsync(movieId, cancellationToken);
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

    [HttpGet("movies/{movieId:guid}/me")]
    [ProducesResponseType(typeof(MovieWatchStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MovieWatchStatusResponse>> GetMovieWatchStatus(
        Guid movieId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchHistoryService.GetMovieWatchStatusAsync(movieId, cancellationToken);
            return Ok(WatchHistoryContractMapper.ToMovieWatchStatusResponse(result));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
    }

    [HttpGet("movies")]
    [ProducesResponseType(typeof(WatchedMoviesListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WatchedMoviesListResponse>> GetWatchedMovies(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchHistoryService.GetWatchedMoviesAsync(
                page ?? SearchPaginationDefaults.DefaultPage,
                pageSize ?? SearchPaginationDefaults.DefaultPageSize,
                cancellationToken);

            return Ok(WatchHistoryContractMapper.ToWatchedMoviesListResponse(result));
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

    [HttpPost("episodes/{episodeId:guid}")]
    [ProducesResponseType(typeof(WatchEpisodeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(WatchEpisodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkEpisodeWatched(Guid episodeId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchHistoryService.MarkEpisodeWatchedAsync(episodeId, cancellationToken);
            var response = WatchHistoryContractMapper.ToWatchEpisodeResponse(episodeId, result);

            return result.Created
                ? StatusCode(StatusCodes.Status201Created, response)
                : Ok(response);
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
                "Episode not found.",
                exception.Message));
        }
    }

    [HttpDelete("episodes/{episodeId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnmarkEpisodeWatched(Guid episodeId, CancellationToken cancellationToken)
    {
        try
        {
            await watchHistoryService.UnmarkEpisodeWatchedAsync(episodeId, cancellationToken);
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

    [HttpGet("episodes/{episodeId:guid}/me")]
    [ProducesResponseType(typeof(EpisodeWatchStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EpisodeWatchStatusResponse>> GetEpisodeWatchStatus(
        Guid episodeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchHistoryService.GetEpisodeWatchStatusAsync(episodeId, cancellationToken);
            return Ok(WatchHistoryContractMapper.ToEpisodeWatchStatusResponse(result));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
    }

    [HttpGet("episodes")]
    [ProducesResponseType(typeof(WatchedEpisodesListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WatchedEpisodesListResponse>> GetWatchedEpisodes(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchHistoryService.GetWatchedEpisodesAsync(
                page ?? SearchPaginationDefaults.DefaultPage,
                pageSize ?? SearchPaginationDefaults.DefaultPageSize,
                cancellationToken);

            return Ok(WatchHistoryContractMapper.ToWatchedEpisodesListResponse(result));
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

    [HttpGet("recent")]
    [ProducesResponseType(typeof(RecentWatchHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RecentWatchHistoryResponse>> GetRecentWatchHistory(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchHistoryService.GetRecentWatchHistoryAsync(
                page ?? SearchPaginationDefaults.DefaultPage,
                pageSize ?? SearchPaginationDefaults.DefaultPageSize,
                cancellationToken);

            return Ok(WatchHistoryContractMapper.ToRecentWatchHistoryResponse(result));
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

    [HttpGet("tvshows/{tvShowId:guid}")]
    [ProducesResponseType(typeof(TvShowWatchProgressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TvShowWatchProgressResponse>> GetTvShowWatchProgress(
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchHistoryService.GetTvShowWatchProgressAsync(tvShowId, cancellationToken);
            return Ok(WatchHistoryContractMapper.ToTvShowWatchProgressResponse(result));
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

    [HttpGet("tvshows/{tvShowId:guid}/seasons/{seasonNumber:int}/episodes")]
    [ProducesResponseType(typeof(SeasonWatchedEpisodesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonWatchedEpisodesResponse>> GetSeasonWatchedEpisodes(
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchHistoryService.GetSeasonWatchedEpisodesAsync(
                tvShowId,
                seasonNumber,
                cancellationToken);

            return Ok(WatchHistoryContractMapper.ToSeasonWatchedEpisodesResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid season request.",
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
                "Season not found.",
                exception.Message));
        }
    }

    [HttpPost("tvshows/{tvShowId:guid}/watch-state")]
    [ProducesResponseType(typeof(BulkUpdateEpisodeWatchStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BulkUpdateEpisodeWatchStateResponse>> BulkUpdateTvShowWatchState(
        Guid tvShowId,
        [FromBody] SetWatchStateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchHistoryService.BulkUpdateTvShowWatchStateAsync(
                tvShowId,
                request.Watched,
                cancellationToken);

            return Ok(WatchHistoryContractMapper.ToBulkUpdateEpisodeWatchStateResponse(result));
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

    [HttpPost("tvshows/{tvShowId:guid}/seasons/{seasonNumber:int}/watch-state")]
    [ProducesResponseType(typeof(BulkUpdateEpisodeWatchStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BulkUpdateEpisodeWatchStateResponse>> BulkUpdateSeasonWatchState(
        Guid tvShowId,
        int seasonNumber,
        [FromBody] SetWatchStateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchHistoryService.BulkUpdateSeasonWatchStateAsync(
                tvShowId,
                seasonNumber,
                request.Watched,
                cancellationToken);

            return Ok(WatchHistoryContractMapper.ToBulkUpdateEpisodeWatchStateResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid season request.",
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
                "Season not found.",
                exception.Message));
        }
    }

    [HttpPost("tvshows/{tvShowId:guid}/episodes/bulk")]
    [ProducesResponseType(typeof(BulkUpdateEpisodeWatchStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BulkUpdateEpisodeWatchStateResponse>> BulkUpdateEpisodeWatchState(
        Guid tvShowId,
        [FromBody] BulkUpdateEpisodeWatchStateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchHistoryService.BulkUpdateEpisodeWatchStateAsync(
                tvShowId,
                request.EpisodeIds,
                request.Watched,
                cancellationToken);

            return Ok(WatchHistoryContractMapper.ToBulkUpdateEpisodeWatchStateResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid bulk watch history request.",
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

    [HttpPost("tvshows/{tvShowId:guid}/episodes/{episodeId:guid}/mark-through")]
    [ProducesResponseType(typeof(MarkThroughEpisodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MarkThroughEpisodeResponse>> MarkThroughEpisode(
        Guid tvShowId,
        Guid episodeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchHistoryService.MarkThroughEpisodeAsync(
                tvShowId,
                episodeId,
                cancellationToken);

            return Ok(WatchHistoryContractMapper.ToMarkThroughEpisodeResponse(result));
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
                "Episode not found.",
                exception.Message));
        }
    }

    [HttpGet("tvshows/{tvShowId:guid}/seasons/{seasonNumber:int}")]
    [ProducesResponseType(typeof(SeasonWatchProgressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonWatchProgressResponse>> GetSeasonWatchProgress(
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchHistoryService.GetSeasonWatchProgressAsync(
                tvShowId,
                seasonNumber,
                cancellationToken);

            return Ok(WatchHistoryContractMapper.ToSeasonWatchProgressResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid season request.",
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
                "Season not found.",
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

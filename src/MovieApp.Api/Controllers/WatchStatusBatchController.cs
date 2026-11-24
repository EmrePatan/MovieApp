using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.WatchHistory;
using MovieApp.Contracts.WatchHistory;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/watch-history/status")]
public sealed class WatchStatusBatchController(IWatchStatusBatchService watchStatusBatchService) : ControllerBase
{
    /// <summary>
    /// Watch state of the current user for many movies / TV shows in one round trip.
    /// Replaces one GET per poster on list screens.
    /// </summary>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(WatchStatusBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WatchStatusBatchResponse>> GetWatchStatusBatch(
        [FromBody] WatchStatusBatchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchStatusBatchService.GetAsync(
                request.MovieIds,
                request.TvShowIds,
                cancellationToken);

            return Ok(new WatchStatusBatchResponse(
                result.Movies
                    .Select(movie => new MovieWatchStatusResponse(movie.MovieId, movie.IsWatched, movie.WatchedAt))
                    .ToList(),
                result.TvShows
                    .Select(show => new TvShowWatchStatusBatchItemResponse(
                        show.TvShowId,
                        show.IsFullyWatched,
                        show.RegularTotalEpisodes,
                        show.RegularWatchedEpisodes))
                    .ToList()));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid watch status batch request.",
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
            Detail = detail,
        };
}

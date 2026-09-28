using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.WatchlistShare;
using MovieApp.Contracts.WatchlistShare;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/watchlists/share")]
public sealed class WatchlistShareController(IWatchlistShareService watchlistShareService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(WatchlistShareStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<WatchlistShareStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchlistShareService.GetStatusAsync(cancellationToken);
            return Ok(new WatchlistShareStatusResponse(result.IsSharingEnabled));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(Problem(StatusCodes.Status401Unauthorized, exception.Message));
        }
    }

    [HttpPost("enable")]
    [ProducesResponseType(typeof(WatchlistShareEnableResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<WatchlistShareEnableResponse>> Enable(CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchlistShareService.EnableAsync(cancellationToken);
            return Ok(new WatchlistShareEnableResponse(
                string.IsNullOrEmpty(result.ShareUrl) ? null : result.ShareUrl,
                result.CreatedNewLink));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(Problem(StatusCodes.Status401Unauthorized, exception.Message));
        }
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Disable(CancellationToken cancellationToken)
    {
        try
        {
            await watchlistShareService.DisableAsync(cancellationToken);
            return NoContent();
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(Problem(StatusCodes.Status401Unauthorized, exception.Message));
        }
    }

    [HttpPost("rotate")]
    [ProducesResponseType(typeof(WatchlistShareRotateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WatchlistShareRotateResponse>> Rotate(CancellationToken cancellationToken)
    {
        try
        {
            var result = await watchlistShareService.RotateAsync(cancellationToken);
            return Ok(new WatchlistShareRotateResponse(result.ShareUrl));
        }
        catch (NotFoundException exception)
        {
            return NotFound(Problem(StatusCodes.Status404NotFound, exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(Problem(StatusCodes.Status401Unauthorized, exception.Message));
        }
    }

    private static ProblemDetails Problem(int statusCode, string detail) =>
        new() { Status = statusCode, Title = "Watchlist share request failed.", Detail = detail };
}

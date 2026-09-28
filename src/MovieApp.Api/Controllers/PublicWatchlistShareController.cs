using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Application.Services.WatchlistShare;
using MovieApp.Contracts.WatchlistShare;

namespace MovieApp.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/public/watchlists")]
public sealed class PublicWatchlistShareController(IWatchlistShareService watchlistShareService) : ControllerBase
{
    [HttpGet("{token}")]
    [ProducesResponseType(typeof(PublicWatchlistShareResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicWatchlistShareResponse>> GetByToken(
        string token,
        CancellationToken cancellationToken)
    {
        var result = await watchlistShareService.TryGetPublicByTokenAsync(token, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(new PublicWatchlistShareResponse(
            result.OwnerDisplayName,
            result.Items.Select(item => new PublicWatchlistShareItemResponse(
                item.ContentType,
                item.ContentId,
                item.Title,
                item.Year,
                item.PosterPath,
                item.VoteAverage)).ToList()));
    }
}

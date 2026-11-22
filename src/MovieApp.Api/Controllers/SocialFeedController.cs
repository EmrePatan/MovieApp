using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Localization;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/social/feed")]
public sealed class SocialFeedController(ISocialFeedService socialFeedService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SocialFeedPageResult>> Get(
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? mode = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await socialFeedService.GetFeedAsync(
                cursor,
                pageSize,
                ParseMode(mode),
                Request.ResolveContentLocale(),
                cancellationToken));
        }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
        catch (ValidationException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
    }

    private static SocialFeedMode ParseMode(string? mode) =>
        mode?.Trim().ToLowerInvariant() switch
        {
            "following" => SocialFeedMode.Following,
            "discover" => SocialFeedMode.Discover,
            _ => SocialFeedMode.Mixed,
        };
}

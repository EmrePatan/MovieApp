using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        [FromQuery] string? cursor, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await socialFeedService.GetFeedAsync(cursor, pageSize, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
        catch (ValidationException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
    }
}

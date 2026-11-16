using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/social")]
public sealed class SocialFollowsController(ISocialFollowService socialFollowService) : ControllerBase
{
    [HttpPost("follows/{userId:guid}")]
    public async Task<ActionResult<SocialFollowResult>> Follow(Guid userId, CancellationToken cancellationToken)
    {
        try { return Ok(await socialFollowService.FollowAsync(userId, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
        catch (ConflictException exception) { return Conflict(Problem(exception.Message)); }
    }

    [HttpDelete("follows/{userId:guid}")]
    public async Task<IActionResult> Unfollow(Guid userId, CancellationToken cancellationToken)
    {
        try { await socialFollowService.UnfollowAsync(userId, cancellationToken); return NoContent(); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
    }

    [HttpPost("follow-requests/{followerUserId:guid}/accept")]
    public async Task<ActionResult<SocialFollowResult>> Accept(Guid followerUserId, CancellationToken cancellationToken)
    {
        try { return Ok(await socialFollowService.AcceptRequestAsync(followerUserId, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
    }

    [HttpDelete("follow-requests/{followerUserId:guid}")]
    public async Task<IActionResult> Reject(Guid followerUserId, CancellationToken cancellationToken)
    {
        try { await socialFollowService.RejectRequestAsync(followerUserId, cancellationToken); return NoContent(); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
    }

    private static ProblemDetails Problem(string detail) => new() { Detail = detail };
}

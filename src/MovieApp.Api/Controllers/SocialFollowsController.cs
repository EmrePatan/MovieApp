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
        catch (AuthenticationException exception) { return Unauthorized(CreateProblem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(CreateProblem(exception.Message)); }
        catch (ConflictException exception) { return Conflict(CreateProblem(exception.Message)); }
    }

    [HttpDelete("follows/{userId:guid}")]
    public async Task<IActionResult> Unfollow(Guid userId, CancellationToken cancellationToken)
    {
        try { await socialFollowService.UnfollowAsync(userId, cancellationToken); return NoContent(); }
        catch (AuthenticationException exception) { return Unauthorized(CreateProblem(exception.Message)); }
    }

    [HttpGet("follow-requests")]
    public async Task<ActionResult<IReadOnlyList<SocialFollowRequestResult>>> GetPendingRequests(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try { return Ok(await socialFollowService.GetPendingRequestsAsync(page, pageSize, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(CreateProblem(exception.Message)); }
    }

    [HttpGet("follow-requests")]
    public async Task<ActionResult<IReadOnlyList<SocialFollowRequestResult>>> PendingRequests(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await socialFollowService.GetPendingRequestsAsync(page, pageSize, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(CreateProblem(exception.Message)); }
    }

    [HttpPost("follow-requests/{followerUserId:guid}/accept")]
    public async Task<ActionResult<SocialFollowResult>> Accept(Guid followerUserId, CancellationToken cancellationToken)
    {
        try { return Ok(await socialFollowService.AcceptRequestAsync(followerUserId, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(CreateProblem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(CreateProblem(exception.Message)); }
    }

    [HttpDelete("follow-requests/{followerUserId:guid}")]
    public async Task<IActionResult> Reject(Guid followerUserId, CancellationToken cancellationToken)
    {
        try { await socialFollowService.RejectRequestAsync(followerUserId, cancellationToken); return NoContent(); }
        catch (AuthenticationException exception) { return Unauthorized(CreateProblem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(CreateProblem(exception.Message)); }
    }


    [HttpPost("blocks/{userId:guid}")]
    public async Task<IActionResult> Block(Guid userId, CancellationToken cancellationToken)
    {
        try { await socialFollowService.BlockAsync(userId, cancellationToken); return NoContent(); }
        catch (AuthenticationException exception) { return Unauthorized(CreateProblem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(CreateProblem(exception.Message)); }
        catch (ConflictException exception) { return Conflict(CreateProblem(exception.Message)); }
    }

    [HttpDelete("blocks/{userId:guid}")]
    public async Task<IActionResult> Unblock(Guid userId, CancellationToken cancellationToken)
    {
        try { await socialFollowService.UnblockAsync(userId, cancellationToken); return NoContent(); }
        catch (AuthenticationException exception) { return Unauthorized(CreateProblem(exception.Message)); }
    }

    private static ProblemDetails CreateProblem(string detail) => new() { Detail = detail };
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/social")]
public sealed class SocialLikesController(ISocialLikeService likes) : ControllerBase
{
    [HttpPost("activities/{activityId:guid}/likes")]
    public Task<ActionResult<SocialLikeResult>> LikeActivity(Guid activityId, CancellationToken cancellationToken) =>
        Execute(() => likes.LikeActivityAsync(activityId, cancellationToken));

    [HttpDelete("activities/{activityId:guid}/likes")]
    public Task<ActionResult<SocialLikeResult>> UnlikeActivity(Guid activityId, CancellationToken cancellationToken) =>
        Execute(() => likes.UnlikeActivityAsync(activityId, cancellationToken));

    [HttpPost("comments/{commentId:guid}/likes")]
    public Task<ActionResult<SocialLikeResult>> LikeComment(Guid commentId, CancellationToken cancellationToken) =>
        Execute(() => likes.LikeCommentAsync(commentId, cancellationToken));

    [HttpDelete("comments/{commentId:guid}/likes")]
    public Task<ActionResult<SocialLikeResult>> UnlikeComment(Guid commentId, CancellationToken cancellationToken) =>
        Execute(() => likes.UnlikeCommentAsync(commentId, cancellationToken));

    private async Task<ActionResult<SocialLikeResult>> Execute(Func<Task<SocialLikeResult>> action)
    {
        try { return Ok(await action()); }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
        catch (NotFoundException exception) { return NotFound(new ProblemDetails { Detail = exception.Message }); }
    }
}

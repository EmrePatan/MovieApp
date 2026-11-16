using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/social/activities/{activityId:guid}/comments")]
public sealed class SocialCommentsController(ISocialCommentService comments) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SocialCommentResult>>> Get(Guid activityId,
        [FromQuery] Guid? parentCommentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await comments.GetAsync(activityId, parentCommentId, page, pageSize, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
        catch (NotFoundException exception) { return NotFound(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<SocialCommentResult>> Create(Guid activityId, [FromBody] CreateSocialCommentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await comments.CreateAsync(activityId, request.ParentCommentId, request.Content, cancellationToken);
            return CreatedAtAction(nameof(Get), new { activityId, parentCommentId = result.ParentCommentId }, result);
        }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
        catch (NotFoundException exception) { return NotFound(new ProblemDetails { Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpPut("~/api/social/comments/{commentId:guid}")]
    public async Task<ActionResult<SocialCommentResult>> Edit(Guid commentId, [FromBody] UpdateSocialCommentRequest request,
        CancellationToken cancellationToken)
    {
        try { return Ok(await comments.EditAsync(commentId, request.Content, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
        catch (NotFoundException exception) { return NotFound(new ProblemDetails { Detail = exception.Message }); }
        catch (InvalidOperationException exception) { return Forbid(exception.Message); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpDelete("~/api/social/comments/{commentId:guid}")]
    public async Task<IActionResult> Delete(Guid commentId, CancellationToken cancellationToken)
    {
        try { await comments.DeleteAsync(commentId, cancellationToken); return NoContent(); }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
        catch (NotFoundException exception) { return NotFound(new ProblemDetails { Detail = exception.Message }); }
        catch (InvalidOperationException exception) { return Forbid(exception.Message); }
    }
}

public sealed record CreateSocialCommentRequest(Guid? ParentCommentId, string Content);
public sealed record UpdateSocialCommentRequest(string Content);
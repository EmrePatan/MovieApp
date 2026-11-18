using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;

namespace MovieApp.Api.Controllers;

public sealed record CreateSocialCommentRequest(string Content, Guid? ParentCommentId);
public sealed record CreateSocialReportRequest(string TargetType, Guid TargetId, string Reason, string? Details);

[Authorize]
[ApiController]
[Route("api/social")]
public sealed class SocialInteractionsController(ISocialInteractionService interactionService) : ControllerBase
{
    [HttpPost("activities/{activityId:guid}/like")]
    public async Task<IActionResult> LikeActivity(Guid activityId, CancellationToken cancellationToken)
    {
        try { await interactionService.SetActivityLikeAsync(activityId, true, cancellationToken); return NoContent(); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
        catch (ValidationException exception) { return BadRequest(Problem(exception.Message)); }
    }

    [HttpDelete("activities/{activityId:guid}/like")]
    public async Task<IActionResult> UnlikeActivity(Guid activityId, CancellationToken cancellationToken)
    {
        try { await interactionService.SetActivityLikeAsync(activityId, false, cancellationToken); return NoContent(); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
    }

    [HttpGet("activities/{activityId:guid}/comments")]
    public async Task<ActionResult<SocialCommentPageResult>> GetComments(
        Guid activityId, [FromQuery] Guid? parentCommentId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try { return Ok(await interactionService.GetCommentsAsync(activityId, parentCommentId, page, pageSize, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
    }

    [HttpPost("activities/{activityId:guid}/comments")]
    public async Task<ActionResult<SocialCommentResult>> CreateComment(
        Guid activityId, [FromBody] CreateSocialCommentRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await interactionService.CreateCommentAsync(activityId, request.ParentCommentId, request.Content, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
        catch (ValidationException exception) { return BadRequest(Problem(exception.Message)); }
        catch (ArgumentException exception) { return BadRequest(Problem(exception.Message)); }
    }

    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid commentId, CancellationToken cancellationToken)
    {
        try { await interactionService.DeleteCommentAsync(commentId, cancellationToken); return NoContent(); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
    }

    [HttpPost("comments/{commentId:guid}/like")]
    public async Task<IActionResult> LikeComment(Guid commentId, CancellationToken cancellationToken)
    {
        try { await interactionService.SetCommentLikeAsync(commentId, true, cancellationToken); return NoContent(); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
        catch (ValidationException exception) { return BadRequest(Problem(exception.Message)); }
    }

    [HttpDelete("comments/{commentId:guid}/like")]
    public async Task<IActionResult> UnlikeComment(Guid commentId, CancellationToken cancellationToken)
    {
        try { await interactionService.SetCommentLikeAsync(commentId, false, cancellationToken); return NoContent(); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
    }

    [HttpPost("reports")]
    public async Task<IActionResult> Report([FromBody] CreateSocialReportRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await interactionService.ReportAsync(request.TargetType, request.TargetId, request.Reason, request.Details, cancellationToken);
            return NoContent();
        }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
        catch (ValidationException exception) { return BadRequest(Problem(exception.Message)); }
        catch (ArgumentException exception) { return BadRequest(Problem(exception.Message)); }
    }

    private static ProblemDetails Problem(string detail) => new() { Detail = detail };
}

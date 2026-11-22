using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MovieApp.Api.Localization;
using MovieApp.Api.RateLimiting;
using MovieApp.Api.Mapping;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Reviews;
using MovieApp.Contracts.Reviews;

namespace MovieApp.Api.Controllers;

public sealed record CreateSocialCommentRequest(string Content, Guid? ParentCommentId, string? AuthoringLocale);
public sealed record CreateSocialReportRequest(string TargetType, Guid TargetId, string Reason, string? Details);

[Authorize]
[ApiController]
[Route("api/social")]
public sealed class SocialInteractionsController(
    ISocialInteractionService interactionService,
    ISocialFeedService feedService,
    ISocialCommentTranslationService commentTranslationService) : ControllerBase
{
    [HttpGet("activities/{activityId:guid}")]
    public async Task<ActionResult<SocialFeedItemResult>> GetActivity(Guid activityId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await feedService.GetActivityAsync(
                activityId,
                Request.ResolveContentLocale(),
                cancellationToken));
        }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
    }

    [EnableRateLimiting(AccountRateLimitPolicies.SocialWrite)]
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

    [EnableRateLimiting(AccountRateLimitPolicies.SocialWrite)]
    [HttpPost("activities/{activityId:guid}/comments")]
    public async Task<ActionResult<SocialCommentResult>> CreateComment(
        Guid activityId, [FromBody] CreateSocialCommentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var authoringLocale = ResolveAuthoringLocale(request.AuthoringLocale);
            return Ok(await interactionService.CreateCommentAsync(
                activityId, request.ParentCommentId, request.Content, authoringLocale, cancellationToken));
        }
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

    [EnableRateLimiting(AccountRateLimitPolicies.SocialWrite)]
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

    [EnableRateLimiting(ReviewTranslationRateLimitPolicies.Translation)]
    [HttpPost("comments/{commentId:guid}/translation")]
    [ProducesResponseType(typeof(ReviewTranslationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReviewTranslationResponse>> TranslateComment(
        Guid commentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var targetLocale = Request.ResolveContentLocale();
            var result = await commentTranslationService.TranslateAsync(commentId, targetLocale, cancellationToken);
            return Ok(ReviewContractMapper.ToTranslationResponse(result));
        }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (ValidationException exception) { return BadRequest(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
        catch (ReviewTranslationUnavailableException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, Problem(exception.Message));
        }
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

    private string ResolveAuthoringLocale(string? requestedAuthoringLocale)
    {
        var resolved = string.IsNullOrWhiteSpace(requestedAuthoringLocale)
            ? Request.ResolveContentLocale()
            : ContentLocaleResolver.Normalize(requestedAuthoringLocale);

        return SupportedContentLocales.Normalize(resolved);
    }

    private static ProblemDetails Problem(string detail) => new() { Detail = detail };
}

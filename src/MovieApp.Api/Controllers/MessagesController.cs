using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MovieApp.Api.RateLimiting;
using MovieApp.Application.Abstractions.Messages;
using MovieApp.Application.Exceptions;

namespace MovieApp.Api.Controllers;

public sealed record CreateDirectConversationRequest(Guid UserId);

public sealed record SendDirectMessageRequest(string? Body);

[Authorize]
[ApiController]
[Route("api/messages")]
public sealed class MessagesController(IDirectMessageService messages) : ControllerBase
{
    [HttpGet("conversations")]
    public async Task<ActionResult<DirectConversationPage>> ListConversations(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try { return Ok(await messages.ListConversationsAsync(page, pageSize, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
    }

    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<ActionResult<DirectConversationDetail>> GetConversation(
        Guid conversationId, CancellationToken cancellationToken)
    {
        try { return Ok(await messages.GetConversationAsync(conversationId, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
    }

    [EnableRateLimiting(AccountRateLimitPolicies.SocialWrite)]
    [HttpPost("conversations")]
    public async Task<ActionResult<DirectConversationDetail>> GetOrCreateConversation(
        [FromBody] CreateDirectConversationRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await messages.GetOrCreateConversationAsync(request.UserId, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
        catch (ConflictException exception) { return Conflict(Problem(exception.Message)); }
    }

    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<DirectMessagePage>> GetMessages(
        Guid conversationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await messages.GetMessagesAsync(conversationId, page, pageSize, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
    }

    [EnableRateLimiting(AccountRateLimitPolicies.SocialWrite)]
    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<DirectMessageItem>> SendMessage(
        Guid conversationId, [FromBody] SendDirectMessageRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await messages.SendAsync(conversationId, request.Body, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
        catch (ValidationException exception) { return BadRequest(Problem(exception.Message)); }
    }

    [HttpDelete("conversations/{conversationId:guid}")]
    public async Task<IActionResult> HideConversation(Guid conversationId, CancellationToken cancellationToken)
    {
        try
        {
            await messages.HideConversationAsync(conversationId, cancellationToken);
            return NoContent();
        }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
        catch (NotFoundException exception) { return NotFound(Problem(exception.Message)); }
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken cancellationToken)
    {
        try { return Ok(await messages.GetUnreadCountAsync(cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(Problem(exception.Message)); }
    }
}

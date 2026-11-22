using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/social/notifications")]
public sealed class SocialNotificationsController(ISocialNotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SocialNotificationPage>> Get([FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try { return Ok(await notifications.GetAsync(page, pageSize, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken cancellationToken)
    {
        try { return Ok(await notifications.GetUnreadCountAsync(cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpPatch("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        try { await notifications.MarkReadAsync(notificationId, cancellationToken); return NoContent(); }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
        catch (NotFoundException exception) { return NotFound(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpPatch("read-all")]
    public async Task<ActionResult<int>> MarkAllRead(CancellationToken cancellationToken)
    {
        try { return Ok(await notifications.MarkAllReadAsync(cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
    }
}
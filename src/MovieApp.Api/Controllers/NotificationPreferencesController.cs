using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Application.Abstractions.Notifications;
using MovieApp.Application.Exceptions;

namespace MovieApp.Api.Controllers;

public sealed record UpdateNotificationPreferencesRequest(bool MessagePushEnabled, bool ReleasePushEnabled);

[Authorize]
[ApiController]
[Route("api/notification-preferences")]
public sealed class NotificationPreferencesController(INotificationPreferencesService preferences) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationPreferencesResult>> Get(CancellationToken cancellationToken)
    {
        try { return Ok(await preferences.GetAsync(cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Title = exception.Message }); }
    }

    [HttpPut]
    public async Task<ActionResult<NotificationPreferencesResult>> Update(
        [FromBody] UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await preferences.UpdateAsync(
                request.MessagePushEnabled, request.ReleasePushEnabled, cancellationToken));
        }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Title = exception.Message }); }
    }
}

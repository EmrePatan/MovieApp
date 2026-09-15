using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Services.Notifications;
using MovieApp.Contracts.Notifications;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(
    IGetNotificationsService getNotificationsService,
    IGetUnreadNotificationCountService getUnreadNotificationCountService,
    IMarkNotificationReadService markNotificationReadService,
    IMarkAllNotificationsReadService markAllNotificationsReadService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(NotificationsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationsResponse>> GetNotifications(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await getNotificationsService.GetAsync(
                page ?? SearchPaginationDefaults.DefaultPage,
                pageSize ?? SearchPaginationDefaults.DefaultPageSize,
                cancellationToken);

            return Ok(NotificationContractMapper.ToNotificationsResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid pagination request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadNotificationCountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UnreadNotificationCountResponse>> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        try
        {
            var unreadCount = await getUnreadNotificationCountService.GetAsync(cancellationToken);
            return Ok(NotificationContractMapper.ToUnreadCountResponse(unreadCount));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
    }

    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(typeof(MarkNotificationReadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MarkNotificationReadResponse>> MarkRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await markNotificationReadService.MarkAsync(id, cancellationToken);
            return Ok(NotificationContractMapper.ToMarkReadResponse(result));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Notification not found.",
                exception.Message));
        }
    }

    [HttpPatch("read-all")]
    [ProducesResponseType(typeof(MarkAllNotificationsReadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MarkAllNotificationsReadResponse>> MarkAllRead(
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await markAllNotificationsReadService.MarkAllAsync(cancellationToken);
            return Ok(NotificationContractMapper.ToMarkAllReadResponse(result));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail) =>
        new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
}

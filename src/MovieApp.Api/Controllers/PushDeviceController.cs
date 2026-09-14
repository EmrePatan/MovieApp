using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.PushDevices;
using MovieApp.Contracts.PushDevices;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/push-devices")]
public sealed class PushDeviceController(
    IRegisterPushDeviceService registerPushDeviceService,
    IUnregisterPushDeviceService unregisterPushDeviceService) : ControllerBase
{
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterPushDeviceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await registerPushDeviceService.RegisterAsync(
                request.ExpoPushToken,
                request.Platform,
                cancellationToken);

            return NoContent();
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid push device request.",
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

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Unregister(
        [FromBody] UnregisterPushDeviceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await unregisterPushDeviceService.UnregisterAsync(
                request.ExpoPushToken,
                cancellationToken);

            return NoContent();
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid push device request.",
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

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail) =>
        new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
}

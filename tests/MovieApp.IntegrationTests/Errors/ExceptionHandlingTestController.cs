using Microsoft.AspNetCore.Mvc;
using MovieApp.Application.Exceptions;

namespace MovieApp.IntegrationTests.Errors;

#pragma warning disable CA1822 // Test controller action must remain an instance member for MVC discovery.

[ApiController]
[Route("__test/errors")]
public sealed class ExceptionHandlingTestController : ControllerBase
{
    [HttpGet("unhandled")]
    public IActionResult Unhandled() =>
        throw new InvalidOperationException("SensitiveConnectionString=super-secret-value");

    [HttpGet("conflict")]
    public IActionResult ThrowConflict() =>
        throw new ConflictException("Test conflict.");
}

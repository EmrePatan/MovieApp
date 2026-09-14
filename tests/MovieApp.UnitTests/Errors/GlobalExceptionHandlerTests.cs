using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Api.Errors;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.UnitTests.Errors;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task RequestAbortedCancellationDoesNotReturn500()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var httpContext = CreateHttpContext(cancellation.Token);

        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var handled = await handler.TryHandleAsync(
            httpContext,
            new OperationCanceledException(cancellation.Token),
            CancellationToken.None);

        Assert.True(handled);
        Assert.False(httpContext.Response.HasStarted);
        Assert.NotEqual(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task NonRequestAbortedCancellationStillReturns500()
    {
        var httpContext = CreateHttpContext();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            httpContext,
            new OperationCanceledException(),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task UnhandledExceptionStillReturns500()
    {
        var httpContext = CreateHttpContext();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("boom"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
    }

    private static DefaultHttpContext CreateHttpContext(CancellationToken requestAborted = default)
    {
        var services = new ServiceCollection()
            .AddSingleton(Options.Create(new AppOptions { PublicBaseUrl = "https://api.test.local" }))
            .BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestAborted = requestAborted,
            RequestServices = services,
        };
    }
}

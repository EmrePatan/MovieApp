using Microsoft.AspNetCore.Http;
using MovieApp.Api.Observability;

namespace MovieApp.UnitTests.Observability;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationIdWhenMissing()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var correlationId = CorrelationIdAccessor.Get(context);
        Assert.NotNull(correlationId);
        Assert.Equal(32, correlationId!.Length);
    }

    [Fact]
    public async Task InvokeAsync_EchoesValidSuppliedCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdConstants.HeaderName] = "integration-correlation-001";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal("integration-correlation-001", CorrelationIdAccessor.Get(context));
    }

    [Fact]
    public async Task InvokeAsync_AcceptsAlternateRequestIdHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdConstants.AlternateHeaderName] = "request-id-001";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal("request-id-001", CorrelationIdAccessor.Get(context));
    }

    [Fact]
    public async Task InvokeAsync_ReplacesMalformedCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdConstants.HeaderName] = new string('x', 200);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var correlationId = CorrelationIdAccessor.Get(context);
        Assert.NotNull(correlationId);
        Assert.Equal(32, correlationId!.Length);
        Assert.NotEqual(new string('x', 200), correlationId);
    }
}

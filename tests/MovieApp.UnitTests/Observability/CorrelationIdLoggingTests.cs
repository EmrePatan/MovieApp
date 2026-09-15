using Microsoft.AspNetCore.Http;
using MovieApp.Api.Observability;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MovieApp.UnitTests.Observability;

public sealed class CorrelationIdLoggingTests
{
    [Fact]
    public async Task InvokeAsync_PushesCorrelationIdIntoLogContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdConstants.HeaderName] = "log-context-test";
        var sink = new CollectSink();
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Sink(sink)
            .Enrich.FromLogContext()
            .CreateLogger();

        var middleware = new CorrelationIdMiddleware(async _ =>
        {
            Log.Information("correlation logging probe");
            await Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        var logEvent = Assert.Single(sink.Events);
        Assert.True(logEvent.Properties.ContainsKey(CorrelationIdConstants.SerilogPropertyName));
        Assert.Equal("\"log-context-test\"", logEvent.Properties[CorrelationIdConstants.SerilogPropertyName].ToString());
    }

    private sealed class CollectSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}

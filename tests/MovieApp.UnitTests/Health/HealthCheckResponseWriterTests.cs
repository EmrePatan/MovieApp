using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MovieApp.Api.Health;

namespace MovieApp.UnitTests.Health;

public sealed class HealthCheckResponseWriterTests
{
    [Fact]
    public async Task WriteResponse_SerializesHealthyChecks()
    {
        var context = CreateHttpContext();
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["postgresql"] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    "PostgreSQL is healthy.",
                    TimeSpan.FromMilliseconds(12),
                    exception: null,
                    data: null)
            },
            HealthStatus.Healthy,
            TimeSpan.FromMilliseconds(15));

        await HealthCheckResponseWriter.WriteResponse(context, report);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("Testing", document.RootElement.GetProperty("environment").GetString());
        Assert.Equal("Healthy", document.RootElement.GetProperty("checks").GetProperty("postgresql").GetProperty("status").GetString());
        Assert.Equal("PostgreSQL is healthy.", document.RootElement.GetProperty("checks").GetProperty("postgresql").GetProperty("description").GetString());
    }

    [Fact]
    public async Task WriteResponse_RedactsUnhealthyDescriptions()
    {
        var context = CreateHttpContext();
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["redis"] = new HealthReportEntry(
                    HealthStatus.Unhealthy,
                    "Connection failed: redis://secret-host:6399",
                    TimeSpan.FromMilliseconds(50),
                    exception: null,
                    data: null)
            },
            HealthStatus.Unhealthy,
            TimeSpan.FromMilliseconds(55));

        await HealthCheckResponseWriter.WriteResponse(context, report);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("Check failed.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("6399", body, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-host", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteResponse_LogsUnhealthyReadinessWithoutSensitiveDetails()
    {
        var logger = new TestLogger();
        var context = CreateHttpContext(logger);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["redis"] = new HealthReportEntry(
                    HealthStatus.Unhealthy,
                    "Connection failed: redis://secret-host:6399",
                    TimeSpan.FromMilliseconds(50),
                    exception: null,
                    data: null)
            },
            HealthStatus.Unhealthy,
            TimeSpan.FromMilliseconds(55));

        await HealthCheckResponseWriter.WriteResponse(context, report);

        var failureLog = Assert.Single(
            logger.Messages,
            message => message.Contains("Readiness check reported unhealthy status", StringComparison.Ordinal));
        Assert.Contains("redis", failureLog, StringComparison.Ordinal);
        Assert.DoesNotContain("6399", failureLog, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-host", failureLog, StringComparison.Ordinal);
    }

    private static DefaultHttpContext CreateHttpContext(TestLogger? logger = null)
    {
        var services = new ServiceCollection()
            .AddSingleton<IHostEnvironment>(new FakeHostEnvironment("Testing"))
            .AddSingleton<ILoggerFactory>(new TestLoggerFactory(logger ?? new TestLogger()))
            .BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices = services,
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MovieApp.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class TestLoggerFactory(TestLogger logger) : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => logger;

        public void Dispose()
        {
        }
    }
}

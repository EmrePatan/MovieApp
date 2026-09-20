using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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

    private static DefaultHttpContext CreateHttpContext()
    {
        var services = new ServiceCollection()
            .AddSingleton<IHostEnvironment>(new FakeHostEnvironment("Testing"))
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
}

using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MovieApp.Application;

namespace MovieApp.Api.Health;

internal static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = report.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();

        if (report.Status != HealthStatus.Healthy)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("MovieApp.Api.Health.Readiness");
            var failedChecks = string.Join(
                ", ",
                report.Entries
                    .Where(entry => entry.Value.Status != HealthStatus.Healthy)
                    .Select(entry => entry.Key));

            HealthCheckLogMessages.LogReadinessUnhealthy(logger, report.Status, failedChecks);
        }

        var payload = new
        {
            status = report.Status.ToString(),
            timestamp = DateTimeOffset.UtcNow,
            environment = environment.EnvironmentName,
            sourceVersion = ApplicationSourceVersion.Resolve(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = GetSafeDescription(entry.Value),
                    duration = entry.Value.Duration.TotalMilliseconds
                })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
    }

    private static string GetSafeDescription(HealthReportEntry entry) =>
        entry.Status == HealthStatus.Healthy
            ? entry.Description ?? string.Empty
            : "Check failed.";
}

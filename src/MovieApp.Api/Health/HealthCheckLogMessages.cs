using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace MovieApp.Api.Health;

internal static partial class HealthCheckLogMessages
{
    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Warning,
        Message = "Readiness check reported unhealthy status. OverallStatus={OverallStatus} FailedChecks={FailedChecks}")]
    internal static partial void LogReadinessUnhealthy(
        ILogger logger,
        HealthStatus overallStatus,
        string failedChecks);
}

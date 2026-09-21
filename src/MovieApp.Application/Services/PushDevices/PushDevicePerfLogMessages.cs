using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.PushDevices;

internal static partial class PushDevicePerfLogMessages
{
    [LoggerMessage(
        EventId = 8401,
        Level = LogLevel.Debug,
        Message = "PushDevicePerf Register TotalMs={TotalMs} PersistenceMs={PersistenceMs} Platform={Platform}")]
    public static partial void LogRegister(
        ILogger logger,
        long totalMs,
        long persistenceMs,
        string platform);
}

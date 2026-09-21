using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.PushNotifications;

internal static partial class ExpoPushLogMessages
{
    [LoggerMessage(
        EventId = 7301,
        Level = LogLevel.Warning,
        Message = "Expo push send HTTP request failed. StatusCode={StatusCode} BatchSize={BatchSize}")]
    internal static partial void LogSendHttpFailure(ILogger logger, int statusCode, int batchSize);

    [LoggerMessage(
        EventId = 7302,
        Level = LogLevel.Warning,
        Message = "Expo push receipt HTTP request failed. StatusCode={StatusCode} TicketCount={TicketCount}")]
    internal static partial void LogReceiptHttpFailure(ILogger logger, int statusCode, int ticketCount);
}

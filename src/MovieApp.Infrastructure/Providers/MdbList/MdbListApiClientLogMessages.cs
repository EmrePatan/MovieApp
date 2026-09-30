using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Providers.MdbList;

internal static partial class MdbListApiClientLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "MDBList request failed for {MediaSegment} tmdb:{TmdbId}")]
    public static partial void LogTransportFailure(
        ILogger logger,
        string mediaSegment,
        int tmdbId,
        Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "MDBList non-success for {MediaSegment} tmdb:{TmdbId} status:{StatusCode}")]
    public static partial void LogNonSuccess(
        ILogger logger,
        string mediaSegment,
        int tmdbId,
        int statusCode);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "MDBList batch request failed for {MediaSegment} count:{Count}")]
    public static partial void LogBatchTransportFailure(
        ILogger logger,
        string mediaSegment,
        int count,
        Exception exception);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "MDBList batch non-success for {MediaSegment} count:{Count} status:{StatusCode}")]
    public static partial void LogBatchNonSuccess(
        ILogger logger,
        string mediaSegment,
        int count,
        int statusCode);
}

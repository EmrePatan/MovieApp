using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Caching;

public sealed class RedisCacheFailureLogger
{
    private static readonly TimeSpan SuppressionInterval = TimeSpan.FromSeconds(60);

    private readonly object _sync = new();
    private DateTimeOffset _suppressedUntil = DateTimeOffset.MinValue;
    private bool _unavailabilityLogged;

    internal void LogGetFailure(ILogger logger, Exception exception)
    {
        if (!TryEnterLoggingWindow(logger))
        {
            return;
        }

        RedisCacheLogMessages.LogCacheGetFailed(logger, exception.GetType().Name);
    }

    internal void LogSetFailure(ILogger logger, Exception exception)
    {
        if (!TryEnterLoggingWindow(logger))
        {
            return;
        }

        RedisCacheLogMessages.LogCacheSetFailed(logger, exception.GetType().Name);
    }

    internal void LogRemoveFailure(ILogger logger, Exception exception)
    {
        if (!TryEnterLoggingWindow(logger))
        {
            return;
        }

        RedisCacheLogMessages.LogCacheRemoveFailed(logger, exception.GetType().Name);
    }

    private bool TryEnterLoggingWindow(ILogger logger)
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;

            if (now < _suppressedUntil)
            {
                return false;
            }

            if (!_unavailabilityLogged)
            {
                RedisCacheLogMessages.LogCacheUnavailable(
                    logger,
                    (int)SuppressionInterval.TotalSeconds);
                _unavailabilityLogged = true;
            }

            _suppressedUntil = now.Add(SuppressionInterval);
            return true;
        }
    }
}

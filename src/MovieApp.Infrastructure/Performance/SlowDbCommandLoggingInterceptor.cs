using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Performance;

/// <summary>
/// Logs a warning for any database command slower than <see cref="ThresholdMs"/>.
/// Fast commands write nothing. Parameter values are never logged, only the command text.
/// </summary>
internal sealed partial class SlowDbCommandLoggingInterceptor : DbCommandInterceptor
{
    internal const int ThresholdMs = 300;
    private const int MaxCommandTextLength = 400;

    private readonly ILogger<SlowDbCommandLoggingInterceptor> _logger;

    public SlowDbCommandLoggingInterceptor(ILogger<SlowDbCommandLoggingInterceptor> logger)
    {
        _logger = logger;
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        LogIfSlow(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        LogIfSlow(command, eventData);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override async ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return await base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        LogIfSlow(command, eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void LogIfSlow(DbCommand command, CommandExecutedEventData eventData)
    {
        var elapsedMs = (long)eventData.Duration.TotalMilliseconds;
        if (elapsedMs < ThresholdMs)
        {
            return;
        }

        LogSlowCommand(_logger, elapsedMs, Truncate(command.CommandText));
    }

    internal static string Truncate(string commandText)
    {
        var collapsed = string.Join(' ', commandText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= MaxCommandTextLength
            ? collapsed
            : string.Concat(collapsed.AsSpan(0, MaxCommandTextLength), "...");
    }

    [LoggerMessage(
        EventId = 7421,
        Level = LogLevel.Warning,
        Message = "Slow DB command {ElapsedMs} ms: {CommandText}")]
    private static partial void LogSlowCommand(ILogger logger, long elapsedMs, string commandText);
}

using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MovieApp.Infrastructure.Performance;

internal sealed class HomeColdPerfDbTelemetryInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        HomeColdPerfScope.Current?.EnterCommand();
        return base.ReaderExecuting(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        HomeColdPerfScope.Current?.EnterCommand();
        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        RecordCommand(eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        RecordCommand(eventData);
        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        HomeColdPerfScope.Current?.EnterCommand();
        return base.ScalarExecuting(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        HomeColdPerfScope.Current?.EnterCommand();
        return await base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        RecordCommand(eventData);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override async ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        RecordCommand(eventData);
        return await base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        HomeColdPerfScope.Current?.EnterCommand();
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        HomeColdPerfScope.Current?.EnterCommand();
        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        RecordCommand(eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        RecordCommand(eventData);
        return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    private static void RecordCommand(CommandExecutedEventData eventData)
    {
        var metrics = HomeColdPerfScope.Current;
        if (metrics is null)
        {
            return;
        }

        metrics.ExitCommand();
        var elapsedMs = eventData.Duration.TotalMilliseconds < 0
            ? 0
            : (long)eventData.Duration.TotalMilliseconds;
        metrics.RecordCommandCompleted(elapsedMs);
    }
}

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MovieApp.Infrastructure.Performance;

internal sealed class HomeColdPerfConnectionInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        RecordConnection(eventData);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        RecordConnection(eventData);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void RecordConnection(ConnectionEndEventData eventData)
    {
        var metrics = HomeColdPerfScope.Current;
        if (metrics is null)
        {
            return;
        }

        var elapsedMs = eventData.Duration.TotalMilliseconds < 0
            ? 0
            : (long)eventData.Duration.TotalMilliseconds;
        metrics.RecordConnectionOpen(elapsedMs);
    }
}

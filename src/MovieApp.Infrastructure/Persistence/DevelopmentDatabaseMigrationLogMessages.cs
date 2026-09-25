using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Persistence;

internal static partial class DevelopmentDatabaseMigrationLogMessages
{
    [LoggerMessage(
        EventId = 5210,
        Level = LogLevel.Information,
        Message = "Development database schema is current. No EF Core migrations to apply.")]
    internal static partial void LogSchemaCurrent(ILogger logger);

    [LoggerMessage(
        EventId = 5211,
        Level = LogLevel.Information,
        Message = "Applying pending EF Core migration in Development: {MigrationId}")]
    internal static partial void LogApplyingPendingMigration(ILogger logger, string migrationId);

    [LoggerMessage(
        EventId = 5212,
        Level = LogLevel.Error,
        Message = "Development database migration failed. Login cannot issue refresh-token sessions until pending EF Core migrations are applied.")]
    internal static partial void LogMigrationFailed(ILogger logger, Exception exception);
}

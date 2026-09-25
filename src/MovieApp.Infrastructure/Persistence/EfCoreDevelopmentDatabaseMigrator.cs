using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Persistence;

public sealed class EfCoreDevelopmentDatabaseMigrator(
    IServiceScopeFactory scopeFactory,
    ILogger<EfCoreDevelopmentDatabaseMigrator> logger) : IDevelopmentDatabaseMigrator
{
    public async Task ApplyPendingMigrationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

            if (pendingMigrations.Length == 0)
            {
                DevelopmentDatabaseMigrationLogMessages.LogSchemaCurrent(logger);
                return;
            }

            foreach (var migrationId in pendingMigrations)
            {
                DevelopmentDatabaseMigrationLogMessages.LogApplyingPendingMigration(logger, migrationId);
            }

            await dbContext.Database.MigrateAsync(cancellationToken);
            DevelopmentDatabaseMigrationLogMessages.LogMigrationsApplied(logger);
        }
        catch (Exception exception)
        {
            DevelopmentDatabaseMigrationLogMessages.LogMigrationFailed(logger, exception);
            throw;
        }
    }
}

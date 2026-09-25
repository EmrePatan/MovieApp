namespace MovieApp.Infrastructure.Persistence;

public interface IDevelopmentDatabaseMigrator
{
    Task ApplyPendingMigrationsAsync(CancellationToken cancellationToken);
}

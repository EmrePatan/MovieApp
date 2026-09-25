using Microsoft.Extensions.Hosting;

namespace MovieApp.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations when the host environment is Development.
/// Production, Staging, and Testing keep schema updates outside process startup.
/// </summary>
public sealed class DevelopmentDatabaseMigrationHostedService(
    IHostEnvironment hostEnvironment,
    IDevelopmentDatabaseMigrator migrator) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            return Task.CompletedTask;
        }

        return migrator.ApplyPendingMigrationsAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

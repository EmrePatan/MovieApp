using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Health;

public sealed class PendingDatabaseMigrationsHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pendingMigrations = await DatabaseSchemaMigrationStatus.GetPendingMigrationsAsync(
                dbContext,
                cancellationToken);

            if (pendingMigrations.Count > 0)
            {
                return HealthCheckResult.Unhealthy(
                    DatabaseSchemaMigrationStatus.FormatPendingMigrationsMessage(pendingMigrations));
            }

            return HealthCheckResult.Healthy("Database schema is current.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database schema verification failed.", exception);
        }
    }
}

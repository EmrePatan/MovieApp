using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.Health;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Api.Health;

internal static class DatabaseSchemaStartupValidator
{
    internal static async Task EnsureCurrentAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
        {
            return;
        }

        var postgreSqlOptions = app.Configuration
            .GetSection(PostgreSqlOptions.SectionName)
            .Get<PostgreSqlOptions>() ?? new PostgreSqlOptions();

        if (!postgreSqlOptions.IsConfigured())
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pendingMigrations = await DatabaseSchemaMigrationStatus.GetPendingMigrationsAsync(
            dbContext,
            cancellationToken);

        if (pendingMigrations.Count > 0)
        {
            throw new InvalidOperationException(
                DatabaseSchemaMigrationStatus.FormatPendingMigrationsMessage(pendingMigrations));
        }
    }
}

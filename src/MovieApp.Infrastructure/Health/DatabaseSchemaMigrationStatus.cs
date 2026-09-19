using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Health;

public static class DatabaseSchemaMigrationStatus
{
    public static async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        return pending.ToList();
    }

    public static string FormatPendingMigrationsMessage(IReadOnlyList<string> pendingMigrations) =>
        pendingMigrations.Count == 0
            ? string.Empty
            : $"Apply pending EF Core migrations before starting the API: {string.Join(", ", pendingMigrations)}";
}

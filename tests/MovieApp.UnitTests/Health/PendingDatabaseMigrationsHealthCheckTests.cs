using Microsoft.Extensions.Diagnostics.HealthChecks;
using MovieApp.Infrastructure.Health;

namespace MovieApp.UnitTests.Health;

public sealed class PendingDatabaseMigrationsHealthCheckTests
{
    [Fact]
    public void FormatPendingMigrationsMessageIncludesMigrationIds()
    {
        var message = DatabaseSchemaMigrationStatus.FormatPendingMigrationsMessage(
            ["20260919141753_AddDataProtectionKeys"]);

        Assert.Contains("20260919141753_AddDataProtectionKeys", message, StringComparison.Ordinal);
        Assert.Contains("Apply pending EF Core migrations", message, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatPendingMigrationsMessageIsEmptyWhenNoPendingMigrations()
    {
        var message = DatabaseSchemaMigrationStatus.FormatPendingMigrationsMessage([]);

        Assert.Equal(string.Empty, message);
    }
}

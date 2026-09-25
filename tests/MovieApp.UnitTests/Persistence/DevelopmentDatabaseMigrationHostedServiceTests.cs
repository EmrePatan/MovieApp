using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.UnitTests.Persistence;

public sealed class DevelopmentDatabaseMigrationHostedServiceTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    [InlineData("Testing", false)]
    public async Task StartAppliesPendingMigrationsOnlyInDevelopment(string environmentName, bool shouldApply)
    {
        var migrator = new RecordingDevelopmentDatabaseMigrator();
        var service = new DevelopmentDatabaseMigrationHostedService(
            new FakeHostEnvironment(environmentName),
            migrator);

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(shouldApply ? 1 : 0, migrator.ApplyCount);
    }

    private sealed class RecordingDevelopmentDatabaseMigrator : IDevelopmentDatabaseMigrator
    {
        public int ApplyCount { get; private set; }

        public Task ApplyPendingMigrationsAsync(CancellationToken cancellationToken)
        {
            ApplyCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MovieApp.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Persistence;

public sealed class CatalogPersistenceFixture : IAsyncLifetime
{
    public static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    private static string GetConnectionString() => IntegrationTestDatabase.GetConnectionString();
}

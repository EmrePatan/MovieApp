using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.ProductMetrics;

public sealed class ProductMetricsFixture : IAsyncLifetime
{
    public ProductMetricsWebApplicationFactory Factory { get; } = new();

    public static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ProductMetricsIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        Factory.Dispose();
    }

    public static async Task ResetAsync()
    {
        await using var context = CreateContext();
        await context.ProductMetricDaily.ExecuteDeleteAsync();
    }
}

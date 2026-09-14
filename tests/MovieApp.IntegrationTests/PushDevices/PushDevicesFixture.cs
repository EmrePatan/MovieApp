using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.PushDevices;

public sealed class PushDevicesFixture : IAsyncLifetime
{
    public PushDevicesWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        await using var context = CreateContext();
        context.PushDevices.RemoveRange(context.PushDevices);
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        Factory.Dispose();
    }

    internal static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(PushDevicesIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

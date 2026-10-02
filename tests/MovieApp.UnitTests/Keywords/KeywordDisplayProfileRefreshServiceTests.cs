using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Keywords;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Recommendations;

namespace MovieApp.UnitTests.Keywords;

public sealed class KeywordDisplayProfileRefreshServiceTests
{
    [Fact]
    public async Task RefreshAsyncReturnsDisabledResultWithoutOpeningTransaction()
    {
        var factory = CreateInMemoryFactory();
        var loader = new KeywordDisplayProfileLoader(
            factory,
            Options.Create(new KeywordCatalogStatisticsOptions()));
        var service = new KeywordDisplayProfileRefreshService(
            factory,
            loader,
            Options.Create(new KeywordDisplayProfileOptions { Enabled = false }),
            NullLogger<KeywordDisplayProfileRefreshService>.Instance);

        var result = await service.RefreshAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("disabled", result.FailureReason);
        Assert.Equal(0, result.ProfilesWritten);
    }

    private static IDbContextFactory<ApplicationDbContext> CreateInMemoryFactory()
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        var provider = services.BuildServiceProvider();
        using (var context = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext())
        {
            context.Database.EnsureCreated();
        }

        return provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    }
}

using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Persistence;

public sealed class SearchProviderRefreshRepositoryTests
{
    [Fact]
    public async Task SetLastRefreshedAtUtcAsyncUpdatesExistingRowAfterConcurrentInsertRace()
    {
        await using var dbContext = CreateDbContext();
        var repository = new SearchProviderRefreshRepository(dbContext);

        await repository.SetLastRefreshedAtUtcAsync("batman", SearchContentType.All, 1, DateTime.UtcNow.AddHours(-1));
        dbContext.ChangeTracker.Clear();

        await repository.SetLastRefreshedAtUtcAsync("batman", SearchContentType.All, 1, DateTime.UtcNow);

        var stored = await dbContext.SearchProviderRefreshes.SingleAsync();
        Assert.True(stored.LastRefreshedAtUtc <= DateTime.UtcNow);
        Assert.True(stored.LastRefreshedAtUtc > DateTime.UtcNow.AddMinutes(-1));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"search-provider-refresh-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }
}

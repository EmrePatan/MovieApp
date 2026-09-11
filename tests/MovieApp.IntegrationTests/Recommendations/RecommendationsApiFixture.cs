using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Recommendations;

public sealed class RecommendationsApiFixture : IAsyncLifetime
{
    public RecommendationsWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        _ = Factory;

        await using var context = CreateContext();
        context.SearchHistories.RemoveRange(context.SearchHistories);
        context.WatchedEpisodes.RemoveRange(context.WatchedEpisodes);
        context.WatchedMovies.RemoveRange(context.WatchedMovies);
        context.Reviews.RemoveRange(context.Reviews);
        context.Ratings.RemoveRange(context.Ratings);
        context.WatchlistItems.RemoveRange(context.WatchlistItems);
        context.Watchlists.RemoveRange(context.Watchlists);
        context.Favorites.RemoveRange(context.Favorites);
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        Factory.Dispose();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(RecommendationsIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.People;

public sealed class PeopleApiFixture : IAsyncLifetime
{
    public PeopleWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public static async Task ResetAsync()
    {
        await using var context = CreateContext();
        context.MovieGenres.RemoveRange(context.MovieGenres);
        context.MoviePeople.RemoveRange(context.MoviePeople);
        context.TvShowPeople.RemoveRange(context.TvShowPeople);
        context.Movies.RemoveRange(context.Movies);
        context.TvShows.RemoveRange(context.TvShows);
        context.People.RemoveRange(context.People);
        context.Genres.RemoveRange(context.Genres);
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
            .UseNpgsql(PeopleIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Persistence;

public sealed class MovieRepositoryTests
{
    [Fact]
    public async Task UpsertFromProviderAsyncPreservesInternalIdForDuplicateExternalId()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"movie-repository-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var repository = new MovieRepository(context);

        var details = new MovieProviderDetails(
            ExternalId: "fake-tmdb-900001",
            TmdbId: 900001,
            TvdbId: 900002,
            ImdbId: "tt9000001",
            Title: "Interstellar",
            OriginalTitle: "Interstellar",
            Overview: "Overview",
            ReleaseDate: new DateOnly(2014, 11, 7),
            RuntimeMinutes: 169,
            PosterPath: "/poster.jpg",
            BackdropPath: "/backdrop.jpg",
            OriginalLanguage: "en",
            VoteAverage: 8.7m,
            VoteCount: 100,
            Genres: ["Adventure", "Drama"]);

        var created = await repository.UpsertFromProviderAsync(details);
        var updated = await repository.UpsertFromProviderAsync(
            details with { VoteCount = 200, Overview = "Updated overview" });

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(200, updated.VoteCount);
        Assert.Equal(1, await context.Movies.CountAsync());
        Assert.Equal(2, await context.Genres.CountAsync());
        Assert.Equal(2, await context.MovieGenres.CountAsync());
    }
}

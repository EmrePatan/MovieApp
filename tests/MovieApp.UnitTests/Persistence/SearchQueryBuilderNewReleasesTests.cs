using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Search;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Search;

namespace MovieApp.UnitTests.Persistence;

public sealed class SearchQueryBuilderNewReleasesTests
{
    [Fact]
    public async Task BuildNewReleasesQuery_ExcludesPosterlessItemsBeforePagination()
    {
        await using var dbContext = CreateContext();
        var today = new DateOnly(2026, 9, 30);

        dbContext.Movies.AddRange(
            CreateMovie("With Poster", "/poster.jpg", today.AddDays(-3)),
            CreateMovie("No Poster", null, today.AddDays(-2)),
            CreateMovie("Empty Poster", "", today.AddDays(-1)));

        await dbContext.SaveChangesAsync();

        var query = SearchQueryBuilder.BuildNewReleasesQuery(
            dbContext,
            new DiscoveryCriteria(SearchContentType.Movie, 1, 20),
            today,
            30);

        var titles = await query.Select(item => item.Title).ToListAsync();

        Assert.Contains("With Poster", titles);
        Assert.DoesNotContain("No Poster", titles);
        Assert.DoesNotContain("Empty Poster", titles);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static MovieApp.Domain.Entities.Movie CreateMovie(string title, string? posterPath, DateOnly releaseDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            OriginalTitle = title,
            Overview = "Overview",
            PosterPath = posterPath,
            ReleaseDate = releaseDate,
            VoteAverage = 7,
            VoteCount = 100,
            TmdbId = Random.Shared.Next(1, 1_000_000),
        };
}

using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Enums;
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

    [Fact]
    public async Task BuildNewReleasesQuery_ExcludesTitlesBelowTheVoteFloorAndKeepsTheWindow()
    {
        await using var dbContext = CreateContext();
        var today = new DateOnly(2026, 9, 30);
        var movieFloor = NewReleasesOptions.DefaultMinVoteCountMovie;
        var tvFloor = NewReleasesOptions.DefaultMinVoteCountTv;

        dbContext.Movies.AddRange(
            CreateMovie("One Vote Ten", "/poster.jpg", today.AddDays(-1), voteCount: 1, voteAverage: 10m),
            CreateMovie("Just Under Floor", "/poster.jpg", today.AddDays(-2), voteCount: movieFloor - 1),
            CreateMovie("At Floor", "/poster.jpg", today.AddDays(-4), voteCount: movieFloor),
            CreateMovie("Outside Window", "/poster.jpg", today.AddDays(-31), voteCount: 500));
        dbContext.TvShows.AddRange(
            CreateTvShow("Tv Under Floor", today.AddDays(-1), voteCount: tvFloor - 1),
            CreateTvShow("Tv At Floor", today.AddDays(-3), voteCount: tvFloor));
        await dbContext.SaveChangesAsync();

        var query = SearchQueryBuilder.BuildNewReleasesQuery(
            dbContext,
            new DiscoveryCriteria(SearchContentType.All, 1, 20),
            today,
            30);

        var titles = await query.Select(item => item.Title).ToListAsync();

        Assert.Equal(["At Floor", "Tv At Floor"], titles.OrderBy(title => title).ToArray());
    }

    [Fact]
    public async Task BuildNewReleasesQuery_ZeroVoteFloorKeepsUnvotedTitles()
    {
        await using var dbContext = CreateContext();
        var today = new DateOnly(2026, 9, 30);

        dbContext.Movies.Add(CreateMovie("Unvoted", "/poster.jpg", today.AddDays(-1), voteCount: 0, voteAverage: 10m));
        await dbContext.SaveChangesAsync();

        var query = SearchQueryBuilder.BuildNewReleasesQuery(
            dbContext,
            new DiscoveryCriteria(SearchContentType.Movie, 1, 20),
            today,
            30,
            minVoteCountMovie: 0);

        var titles = await query.Select(item => item.Title).ToListAsync();

        Assert.Equal(["Unvoted"], titles);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static MovieApp.Domain.Entities.Movie CreateMovie(
        string title,
        string? posterPath,
        DateOnly releaseDate,
        int voteCount = 100,
        decimal voteAverage = 7m) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            OriginalTitle = title,
            Overview = "Overview",
            PosterPath = posterPath,
            ReleaseDate = releaseDate,
            VoteAverage = voteAverage,
            VoteCount = voteCount,
            TmdbId = Random.Shared.Next(1, 1_000_000),
        };

    private static MovieApp.Domain.Entities.TvShow CreateTvShow(string title, DateOnly firstAirDate, int voteCount) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            OriginalTitle = title,
            Overview = "Overview",
            PosterPath = "/poster.jpg",
            FirstAirDate = firstAirDate,
            VoteAverage = 7m,
            VoteCount = voteCount,
            Status = TvShowStatus.ReturningSeries,
            TmdbId = Random.Shared.Next(1, 1_000_000),
        };
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Search;

public sealed class NewReleasesWindowTests
{
    [Fact]
    public async Task GetNewReleasesAsyncKeepsTitlesInsideTheDefaultNinetyDayWindow()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await using var context = CreateContext();
        var currentMovieId = Guid.NewGuid();
        var boundaryMovieId = Guid.NewGuid();
        var staleMovieId = Guid.NewGuid();
        var futureMovieId = Guid.NewGuid();
        var undatedMovieId = Guid.NewGuid();
        var currentTvId = Guid.NewGuid();
        var staleTvId = Guid.NewGuid();

        context.Movies.AddRange(
            CreateMovie(currentMovieId, "Current Movie", today.AddDays(-7)),
            CreateMovie(boundaryMovieId, "Boundary Movie", today.AddDays(-90)),
            CreateMovie(staleMovieId, "Stale Movie", today.AddDays(-91)),
            CreateMovie(futureMovieId, "Future Movie", today.AddDays(1)),
            CreateMovie(undatedMovieId, "Undated Movie", null));
        context.TvShows.AddRange(
            CreateTvShow(currentTvId, "Current Show", today),
            CreateTvShow(staleTvId, "Stale Show", today.AddDays(-91)));
        await context.SaveChangesAsync();

        var repository = CreateRepository(context, maxAgeDays: NewReleasesOptions.DefaultMaxAgeDays);
        var result = await repository.GetNewReleasesAsync(new DiscoveryCriteria(SearchContentType.All, 1, 20));

        Assert.Equal(90, new NewReleasesOptions().MaxAgeDays);
        Assert.Equal(
            [currentTvId, currentMovieId, boundaryMovieId],
            result.Items.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task GetNewReleasesAsyncWithZeroMaxAgeKeepsEveryReleasedTitle()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await using var context = CreateContext();
        var classicId = Guid.NewGuid();
        var futureId = Guid.NewGuid();
        context.Movies.AddRange(
            CreateMovie(classicId, "Classic", today.AddDays(-400)),
            CreateMovie(futureId, "Future", today.AddDays(2)));
        await context.SaveChangesAsync();

        var repository = CreateRepository(context, maxAgeDays: 0);
        var result = await repository.GetNewReleasesAsync(new DiscoveryCriteria(SearchContentType.Movie, 1, 20));

        Assert.Equal([classicId], result.Items.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task GetNewReleasesAsyncExcludesLowVoteTitlesAndRanksQualifiedTitlesNewestFirst()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await using var context = CreateContext();
        var oneVoteId = Guid.NewGuid();
        var underFloorMovieId = Guid.NewGuid();
        var newestQualifiedId = Guid.NewGuid();
        var olderQualifiedId = Guid.NewGuid();
        var posterlessId = Guid.NewGuid();
        var outsideWindowId = Guid.NewGuid();
        var underFloorTvId = Guid.NewGuid();
        var qualifiedTvId = Guid.NewGuid();

        context.Movies.AddRange(
            CreateMovie(oneVoteId, "One Vote Ten", today.AddDays(-1), voteCount: 1, voteAverage: 10m),
            CreateMovie(underFloorMovieId, "Under Floor Movie", today.AddDays(-2), voteCount: NewReleasesOptions.DefaultMinVoteCountMovie - 1),
            CreateMovie(newestQualifiedId, "Newest Qualified", today.AddDays(-3), voteCount: NewReleasesOptions.DefaultMinVoteCountMovie),
            CreateMovie(olderQualifiedId, "Older Qualified", today.AddDays(-12), voteCount: NewReleasesOptions.DefaultMinVoteCountMovie + 10),
            CreateMovie(posterlessId, "Posterless Qualified", today.AddDays(-1), voteCount: 400, posterPath: null),
            CreateMovie(outsideWindowId, "Outside Window", today.AddDays(-91), voteCount: 900));
        context.TvShows.AddRange(
            CreateTvShow(underFloorTvId, "Under Floor Show", today, voteCount: NewReleasesOptions.DefaultMinVoteCountTv - 1),
            CreateTvShow(qualifiedTvId, "Qualified Show", today.AddDays(-6), voteCount: NewReleasesOptions.DefaultMinVoteCountTv));
        await context.SaveChangesAsync();

        var repository = CreateRepository(context, maxAgeDays: NewReleasesOptions.DefaultMaxAgeDays);
        var result = await repository.GetNewReleasesAsync(new DiscoveryCriteria(SearchContentType.All, 1, 20));

        Assert.Equal(
            [newestQualifiedId, qualifiedTvId, olderQualifiedId],
            result.Items.Select(item => item.Id).ToArray());
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task GetNewReleasesAsyncWithZeroVoteFloorKeepsUnvotedTitles()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await using var context = CreateContext();
        var unvotedId = Guid.NewGuid();
        context.Movies.Add(CreateMovie(unvotedId, "Unvoted", today.AddDays(-2), voteCount: 0, voteAverage: 10m));
        await context.SaveChangesAsync();

        var repository = CreateRepository(context, maxAgeDays: 90, minVoteCountMovie: 0, minVoteCountTv: 0);
        var result = await repository.GetNewReleasesAsync(new DiscoveryCriteria(SearchContentType.Movie, 1, 20));

        Assert.Equal([unvotedId], result.Items.Select(item => item.Id).ToArray());
    }

    private static SearchRepository CreateRepository(
        ApplicationDbContext context,
        int maxAgeDays,
        int? minVoteCountMovie = null,
        int? minVoteCountTv = null) =>
        new(
            context,
            Options.Create(new TopRatedOptions()),
            NullLogger<SearchRepository>.Instance,
            newReleasesOptions: Options.Create(new NewReleasesOptions
            {
                MaxAgeDays = maxAgeDays,
                MinVoteCountMovie = minVoteCountMovie ?? NewReleasesOptions.DefaultMinVoteCountMovie,
                MinVoteCountTv = minVoteCountTv ?? NewReleasesOptions.DefaultMinVoteCountTv
            }));

    private static Movie CreateMovie(
        Guid id,
        string title,
        DateOnly? releaseDate,
        int? voteCount = null,
        decimal voteAverage = 7m,
        string? posterPath = "/poster.jpg")
    {
        var utcNow = DateTime.UtcNow;
        return new Movie
        {
            Id = id,
            Title = title,
            ReleaseDate = releaseDate,
            PosterPath = posterPath,
            VoteAverage = voteAverage,
            VoteCount = voteCount ?? NewReleasesOptions.DefaultMinVoteCountMovie,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    private static TvShow CreateTvShow(
        Guid id,
        string title,
        DateOnly? firstAirDate,
        int? voteCount = null)
    {
        var utcNow = DateTime.UtcNow;
        return new TvShow
        {
            Id = id,
            Title = title,
            FirstAirDate = firstAirDate,
            PosterPath = "/poster.jpg",
            Status = TvShowStatus.ReturningSeries,
            VoteAverage = 8m,
            VoteCount = voteCount ?? NewReleasesOptions.DefaultMinVoteCountTv,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"new-releases-window-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }
}

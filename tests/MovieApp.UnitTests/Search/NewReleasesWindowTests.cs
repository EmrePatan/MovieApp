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

    private static SearchRepository CreateRepository(ApplicationDbContext context, int maxAgeDays) =>
        new(
            context,
            Options.Create(new TopRatedOptions()),
            NullLogger<SearchRepository>.Instance,
            newReleasesOptions: Options.Create(new NewReleasesOptions { MaxAgeDays = maxAgeDays }));

    private static Movie CreateMovie(Guid id, string title, DateOnly? releaseDate)
    {
        var utcNow = DateTime.UtcNow;
        return new Movie
        {
            Id = id,
            Title = title,
            ReleaseDate = releaseDate,
            PosterPath = "/poster.jpg",
            VoteAverage = 7m,
            VoteCount = 20,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    private static TvShow CreateTvShow(Guid id, string title, DateOnly? firstAirDate)
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
            VoteCount = 30,
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

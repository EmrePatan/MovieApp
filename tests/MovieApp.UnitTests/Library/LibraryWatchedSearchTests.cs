using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Common;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Library;

public sealed class LibraryWatchedSearchTests
{
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task GetWatchedAsync_OrdersByLastActivityThenTypeThenId()
    {
        await using var context = await CreateSeededContextAsync();
        var repository = new LibraryRepository(context);
        var request = new LibraryPageRequest(
            1,
            10,
            11,
            null,
            LibraryCountMode.Required,
            SearchTextMatch.Empty);

        var (items, _) = await repository.GetWatchedAsync(
            UserId,
            SearchContentType.All,
            request);

        Assert.Equal(3, items.Count);
        Assert.Equal(["Done Show", "Star Movie", "Earlier Movie"], items.Select(item => item.Title).ToList());
    }

    [Fact]
    public void WatchedUnionRows_WithTitleFilter_IsComposedBeforeOrdering()
    {
        using var context = CreateNpgsqlContext();
        var repository = new LibraryRepository(context);
        var request = new LibraryPageRequest(
            1,
            5,
            6,
            null,
            LibraryCountMode.Required,
            SearchTextMatch.FromQuery("star"),
            "en-US");

        var sql = repository.WatchedUnionRows(UserId, request)
            .OrderByDescending(row => row.LastActivityAt)
            .ThenBy(row => row.Type)
            .ThenBy(row => row.Id)
            .Take(5)
            .ToQueryString();

        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("watched_movies", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationDbContext CreateNpgsqlContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=movieapp;Username=postgres;Password=postgres")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<ApplicationDbContext> CreateSeededContextAsync()
    {
        var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"library-watched-search-{Guid.NewGuid()}")
                .Options);

        var utcNow = DateTime.UtcNow;
        var earlierMovieId = Guid.NewGuid();
        var starMovieId = Guid.NewGuid();
        context.Movies.AddRange(
            new Movie
            {
                Id = earlierMovieId,
                Title = "Earlier Movie",
                VoteAverage = 7,
                VoteCount = 10,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            },
            new Movie
            {
                Id = starMovieId,
                Title = "Star Movie",
                VoteAverage = 8,
                VoteCount = 20,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });

        context.Users.Add(new User
        {
            Id = UserId,
            Email = "watched-search@test.local",
            NormalizedEmail = "WATCHED-SEARCH@TEST.LOCAL",
            UserName = "watched-search",
            DisplayName = "watched-search",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.WatchedMovies.AddRange(
            WatchedMovie.Create(UserId, earlierMovieId, utcNow.AddDays(-2)),
            WatchedMovie.Create(UserId, starMovieId, utcNow.AddDays(-1)));

        var tvShowId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            Title = "Done Show",
            Status = TvShowStatus.Ended,
            VoteAverage = 9,
            VoteCount = 100,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = tvShowId,
            SeasonNumber = 1,
            EpisodeCount = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.Episodes.Add(new Episode
        {
            Id = episodeId,
            SeasonId = seasonId,
            EpisodeNumber = 1,
            Name = "Pilot",
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.WatchedEpisodes.Add(new WatchedEpisode
        {
            UserId = UserId,
            EpisodeId = episodeId,
            WatchedAt = utcNow,
        });

        await context.SaveChangesAsync();
        return context;
    }
}

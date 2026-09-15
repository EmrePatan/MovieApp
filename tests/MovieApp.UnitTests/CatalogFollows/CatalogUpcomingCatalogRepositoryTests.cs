using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.CatalogFollows;

public sealed class CatalogUpcomingCatalogRepositoryTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly Today = new(2026, 9, 15);

    [Fact]
    public async Task GetUpcomingCatalogAsync_WhenAuthenticated_IncludesFollowedFutureEpisode()
    {
        await using var context = CreateContext();
        var tvShowId = await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            episodes:
            [
                (seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(3), name: "Next"),
                (seasonNumber: 1, episodeNumber: 2, airDate: Today.AddDays(10), name: "Later")
            ]);
        var repository = new CatalogFollowCatalogRepository(context);

        var (items, totalCount) = await repository.GetUpcomingCatalogAsync(
            UserId,
            1,
            10,
            Today,
            "TR");

        Assert.Equal(1, totalCount);
        var episode = Assert.Single(items);
        Assert.Equal(CatalogUpcomingKind.TvEpisode, episode.UpcomingKind);
        Assert.Equal(tvShowId, episode.ContentId);
        Assert.Equal(Today.AddDays(3), episode.ReleaseDate);
        Assert.Equal(1, episode.SeasonNumber);
        Assert.Equal(1, episode.EpisodeNumber);
        Assert.Equal("Next", episode.EpisodeName);
        Assert.True(episode.IsFollowed);
    }

    [Fact]
    public async Task GetUpcomingCatalogAsync_SelectsEarliestFutureEpisodePerShow()
    {
        await using var context = CreateContext();
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            episodes:
            [
                (seasonNumber: 2, episodeNumber: 1, airDate: Today.AddDays(7), name: "S2E1"),
                (seasonNumber: 1, episodeNumber: 5, airDate: Today.AddDays(5), name: "S1E5"),
                (seasonNumber: 1, episodeNumber: 6, airDate: Today.AddDays(4), name: "S1E6")
            ]);
        var repository = new CatalogFollowCatalogRepository(context);

        var (items, _) = await repository.GetUpcomingCatalogAsync(UserId, 1, 10, Today, "TR");

        var episode = Assert.Single(items);
        Assert.Equal(1, episode.SeasonNumber);
        Assert.Equal(6, episode.EpisodeNumber);
        Assert.Equal(Today.AddDays(4), episode.ReleaseDate);
    }

    [Fact]
    public async Task GetUpcomingCatalogAsync_ExcludesEpisodeAiringToday()
    {
        await using var context = CreateContext();
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today, name: "Today")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var (items, totalCount) = await repository.GetUpcomingCatalogAsync(UserId, 1, 10, Today, "TR");

        Assert.Equal(0, totalCount);
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetUpcomingCatalogAsync_ExcludesPastEpisodes()
    {
        await using var context = CreateContext();
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(-1), name: "Past")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var (items, totalCount) = await repository.GetUpcomingCatalogAsync(UserId, 1, 10, Today, "TR");

        Assert.Equal(0, totalCount);
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetUpcomingCatalogAsync_ExcludesNullAirDateEpisodes()
    {
        await using var context = CreateContext();
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: null, name: "Undated")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var (items, totalCount) = await repository.GetUpcomingCatalogAsync(UserId, 1, 10, Today, "TR");

        Assert.Equal(0, totalCount);
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetUpcomingCatalogAsync_ExcludesUnfollowedShows()
    {
        await using var context = CreateContext();
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: null,
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(2), name: "Unfollowed")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var (items, totalCount) = await repository.GetUpcomingCatalogAsync(UserId, 1, 10, Today, "TR");

        Assert.Equal(0, totalCount);
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetUpcomingCatalogAsync_UnfollowDoesNotDeleteEpisodeMetadata()
    {
        await using var context = CreateContext();
        var tvShowId = await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(2), name: "Kept")]);

        context.CatalogFollows.RemoveRange(context.CatalogFollows);
        await context.SaveChangesAsync();

        Assert.Equal(1, await context.Episodes.CountAsync());
        Assert.Equal(tvShowId, await context.Episodes.Select(episode => episode.Season.TvShowId).SingleAsync());
    }

    [Fact]
    public async Task GetUpcomingCatalogAsync_MoviesRemainUnchangedForAnonymousUsers()
    {
        await using var context = CreateContext();
        var movieId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Future Movie",
            ReleaseDate = Today.AddDays(8),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        await context.SaveChangesAsync();
        var repository = new CatalogFollowCatalogRepository(context);

        var (items, totalCount) = await repository.GetUpcomingCatalogAsync(null, 1, 10, Today, "TR");

        Assert.Equal(1, totalCount);
        var movie = Assert.Single(items);
        Assert.Equal(CatalogUpcomingKind.MovieRelease, movie.UpcomingKind);
        Assert.Equal(movieId, movie.ContentId);
        Assert.False(movie.IsFollowed);
    }

    [Fact]
    public async Task GetUpcomingCatalogAsync_UsesDeterministicOrderingForEqualDates()
    {
        await using var context = CreateContext();
        var movieA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var movieB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var utcNow = DateTime.UtcNow;
        var sharedDate = Today.AddDays(30);

        context.Movies.AddRange(
            new Movie { Id = movieB, Title = "Movie B", ReleaseDate = sharedDate, CreatedAt = utcNow, UpdatedAt = utcNow },
            new Movie { Id = movieA, Title = "Movie A", ReleaseDate = sharedDate, CreatedAt = utcNow, UpdatedAt = utcNow });
        await context.SaveChangesAsync();

        var repository = new CatalogFollowCatalogRepository(context);
        var first = await repository.GetUpcomingCatalogAsync(null, 1, 10, Today, "TR");
        var second = await repository.GetUpcomingCatalogAsync(null, 1, 10, Today, "TR");

        Assert.Equal([movieA, movieB], first.Items.Select(item => item.ContentId).ToList());
        Assert.Equal(first.Items.Select(item => item.ContentId).ToList(), second.Items.Select(item => item.ContentId).ToList());
    }

    [Fact]
    public async Task GetUpcomingCatalogAsync_RespectsUtcTodayBoundary()
    {
        await using var context = CreateContext();
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            episodes:
            [
                (seasonNumber: 1, episodeNumber: 1, airDate: Today, name: "Today"),
                (seasonNumber: 1, episodeNumber: 2, airDate: Today.AddDays(1), name: "Tomorrow")
            ]);
        var repository = new CatalogFollowCatalogRepository(context);

        var (items, _) = await repository.GetUpcomingCatalogAsync(UserId, 1, 10, Today, "TR");

        var episode = Assert.Single(items);
        Assert.Equal(Today.AddDays(1), episode.ReleaseDate);
        Assert.Equal("Tomorrow", episode.EpisodeName);
    }

    [Fact]
    public async Task GetFollowedTvUpcomingEpisodesAsync_LimitsResultsToRequestedCount()
    {
        await using var context = CreateContext();
        for (var index = 0; index < 7; index++)
        {
            await SeedTvShowWithEpisodesAsync(
                context,
                followUserId: UserId,
                title: $"Show {index}",
                episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(index + 1), name: $"E{index}")]);
        }

        var repository = new CatalogFollowCatalogRepository(context);
        var items = await repository.GetFollowedTvUpcomingEpisodesAsync(UserId, Today, 5);

        Assert.Equal(5, items.Count);
        Assert.All(items, item => Assert.Equal(CatalogUpcomingKind.TvEpisode, item.UpcomingKind));
    }

    [Fact]
    public async Task GetFollowedTvUpcomingEpisodesAsync_OrdersNearestAirDateFirst()
    {
        await using var context = CreateContext();
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Later",
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(10), name: "Later")]);
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Sooner",
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(2), name: "Sooner")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var items = await repository.GetFollowedTvUpcomingEpisodesAsync(UserId, Today, 5);

        Assert.Equal(["Sooner", "Later"], items.Select(item => item.Title).ToList());
    }

    [Fact]
    public async Task GetUpcomingCatalogAsync_ReturnsNextEpisodeForMultipleFollowedShowsInOneQuery()
    {
        await using var context = CreateContext();
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Show A",
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(2), name: "A1")]);
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Show B",
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(3), name: "B1")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var (items, totalCount) = await repository.GetUpcomingCatalogAsync(UserId, 1, 10, Today, "TR");

        Assert.Equal(2, totalCount);
        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal(CatalogUpcomingKind.TvEpisode, item.UpcomingKind));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"catalog-upcoming-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Guid> SeedTvShowWithEpisodesAsync(
        ApplicationDbContext context,
        Guid? followUserId,
        string title = "Followed Show",
        IReadOnlyList<(int seasonNumber, int episodeNumber, DateOnly? airDate, string name)>? episodes = null)
    {
        episodes ??= [(1, 1, Today.AddDays(2), "Episode")];
        var utcNow = DateTime.UtcNow;
        var tvShowId = Guid.NewGuid();
        var tvShow = new TvShow
        {
            Id = tvShowId,
            Title = title,
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        context.TvShows.Add(tvShow);

        foreach (var episodeSeed in episodes)
        {
            var season = context.Seasons.Local.FirstOrDefault(item =>
                item.TvShowId == tvShowId && item.SeasonNumber == episodeSeed.seasonNumber);

            if (season is null)
            {
                season = new Season
                {
                    Id = Guid.NewGuid(),
                    TvShowId = tvShowId,
                    TvShow = tvShow,
                    SeasonNumber = episodeSeed.seasonNumber,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                };
                context.Seasons.Add(season);
            }

            context.Episodes.Add(new Episode
            {
                Id = Guid.NewGuid(),
                SeasonId = season.Id,
                Season = season,
                EpisodeNumber = episodeSeed.episodeNumber,
                Name = episodeSeed.name,
                AirDate = episodeSeed.airDate,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
        }

        if (followUserId.HasValue)
        {
            context.CatalogFollows.Add(CatalogFollow.CreateTvFollow(
                followUserId.Value,
                tvShowId,
                notifyNewSeasons: true,
                notifyNewEpisodes: true,
                utcNow));
        }

        await context.SaveChangesAsync();
        return tvShowId;
    }
}

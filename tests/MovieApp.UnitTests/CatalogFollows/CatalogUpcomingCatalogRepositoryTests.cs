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
    public async Task GetFollowedUpcomingForHomeAsync_IncludesFollowedFutureMovieRelease()
    {
        await using var context = CreateContext();
        var utcNow = DateTime.UtcNow;
        var movieId = Guid.NewGuid();
        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Future Movie",
            PosterPath = "/movie.jpg",
            ReleaseDate = Today.AddDays(5),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.CatalogFollows.Add(CatalogFollow.CreateMovieFollow(UserId, movieId, utcNow));
        await context.SaveChangesAsync();

        var repository = new CatalogFollowCatalogRepository(context);
        var items = await repository.GetFollowedUpcomingForHomeAsync(UserId, Today, "TR", 5);

        var movie = Assert.Single(items);
        Assert.Equal(CatalogUpcomingKind.MovieRelease, movie.UpcomingKind);
        Assert.Equal("Future Movie", movie.Title);
        Assert.True(movie.IsFollowed);
    }

    [Fact]
    public async Task GetFollowedUpcomingForHomeAsync_LimitsResultsToRequestedCount()
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
        var items = await repository.GetFollowedUpcomingForHomeAsync(UserId, Today, "TR", 5);

        Assert.Equal(5, items.Count);
        Assert.All(items, item => Assert.Equal(CatalogUpcomingKind.TvEpisode, item.UpcomingKind));
    }

    [Fact]
    public async Task GetFollowedUpcomingForHomeAsync_OrdersNearestAirDateFirstAcrossMoviesAndEpisodes()
    {
        await using var context = CreateContext();
        var utcNow = DateTime.UtcNow;
        var movieId = Guid.NewGuid();
        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Soon Movie",
            ReleaseDate = Today.AddDays(1),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.CatalogFollows.Add(CatalogFollow.CreateMovieFollow(UserId, movieId, utcNow));
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Later Show",
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(10), name: "Later")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var items = await repository.GetFollowedUpcomingForHomeAsync(UserId, Today, "TR", 5);

        Assert.Equal(["Soon Movie", "Later Show"], items.Select(item => item.Title).ToList());
    }

    [Fact]
    public async Task GetFollowedUpcomingForHomeAsync_OrdersNearestAirDateFirst()
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

        var items = await repository.GetFollowedUpcomingForHomeAsync(UserId, Today, "TR", 5);

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

    [Fact]
    public void FollowedTvNextEpisodeQueryIsTranslatedByNpgsql()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=movieapp;Username=postgres;Password=postgres")
            .Options;
        using var context = new ApplicationDbContext(options);
        var repository = new CatalogFollowCatalogRepository(context);

        var sql = repository.FollowedTvNextEpisodeQuery(UserId, Today).ToQueryString();

        Assert.Contains("episodes", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("catalog_follows", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROW_NUMBER()", sql, StringComparison.Ordinal);
        Assert.Contains("row <= 1", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFollowedUpcomingCatalogAsync_OrdersMoviesAndEpisodesGlobally()
    {
        await using var context = CreateContext();
        var utcNow = DateTime.UtcNow;
        var movieId = Guid.NewGuid();
        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Mid Movie",
            ReleaseDate = Today.AddDays(5),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.CatalogFollows.Add(CatalogFollow.CreateMovieFollow(UserId, movieId, utcNow));
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Soon Show",
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(2), name: "Soon")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var (items, totalCount) = await repository.GetFollowedUpcomingCatalogAsync(UserId, 1, 10, Today, "TR");

        Assert.Equal(2, totalCount);
        Assert.Equal(["Soon Show", "Mid Movie"], items.Select(item => item.Title).ToList());
        Assert.Equal(CatalogUpcomingKind.TvEpisode, items[0].UpcomingKind);
        Assert.Equal(CatalogUpcomingKind.MovieRelease, items[1].UpcomingKind);
    }

    [Fact]
    public async Task GetFollowedUpcomingCatalogAsync_ReturnsRequestedPageWithoutLoadingEarlierPages()
    {
        await using var context = CreateContext();
        for (var index = 0; index < 5; index++)
        {
            await SeedTvShowWithEpisodesAsync(
                context,
                followUserId: UserId,
                title: $"Show {index}",
                episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(index + 1), name: $"E{index}")]);
        }

        var repository = new CatalogFollowCatalogRepository(context);

        var (pageOne, totalCount) = await repository.GetFollowedUpcomingCatalogAsync(UserId, 1, 2, Today, "TR");
        var (pageTwo, _) = await repository.GetFollowedUpcomingCatalogAsync(UserId, 2, 2, Today, "TR");
        var (pagePastEnd, _) = await repository.GetFollowedUpcomingCatalogAsync(UserId, 4, 2, Today, "TR");

        Assert.Equal(5, totalCount);
        Assert.Equal(2, pageOne.Count);
        Assert.Equal(["Show 0", "Show 1"], pageOne.Select(item => item.Title).ToList());
        Assert.Equal(["Show 2", "Show 3"], pageTwo.Select(item => item.Title).ToList());
        Assert.Empty(pagePastEnd);
    }

    [Fact]
    public void FollowedUpcomingPaging_AppliesTakeInSqlPerSource()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=movieapp;Username=postgres;Password=postgres")
            .Options;
        using var context = new ApplicationDbContext(options);
        var repository = new CatalogFollowCatalogRepository(context);

        var movieSql = repository
            .FollowedMovieUpcomingRowsQuery(UserId, Today, "TR")
            .OrderBy(row => row.ReleaseDate)
            .ThenBy(row => row.UpcomingKind)
            .ThenBy(row => row.ContentType)
            .ThenBy(row => row.ContentId)
            .ThenBy(row => row.EpisodeId)
            .Take(3)
            .ToQueryString();

        var episodeSql = repository.FollowedTvNextEpisodeQuery(UserId, Today).ToQueryString();
        var boundedEpisodeSql = repository
            .BoundedFollowedTvNextEpisodeRowsQuery(UserId, Today, 3)
            .ToQueryString();

        Assert.Contains("LIMIT", movieSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROW_NUMBER()", episodeSql, StringComparison.Ordinal);
        Assert.Contains("LIMIT", boundedEpisodeSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROW_NUMBER()", boundedEpisodeSql, StringComparison.Ordinal);
        Assert.DoesNotContain("OFFSET", movieSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetFollowedEpisodeReleasesForHomeAsync_WhenLimitZero_ReturnsEmpty()
    {
        await using var context = CreateContext();
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(2), name: "Next")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var items = await repository.GetFollowedEpisodeReleasesForHomeAsync(UserId, Today, 0);

        Assert.Empty(items);
    }

    [Fact]
    public async Task GetFollowedEpisodeReleasesForHomeAsync_SelectsOneNextEpisodePerShowBeforeGlobalLimit()
    {
        await using var context = CreateContext();
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Many Episodes",
            episodes:
            [
                (seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(1), name: "E1"),
                (seasonNumber: 1, episodeNumber: 2, airDate: Today.AddDays(2), name: "E2"),
                (seasonNumber: 1, episodeNumber: 3, airDate: Today.AddDays(3), name: "E3"),
                (seasonNumber: 1, episodeNumber: 4, airDate: Today.AddDays(4), name: "E4"),
                (seasonNumber: 1, episodeNumber: 5, airDate: Today.AddDays(5), name: "E5"),
                (seasonNumber: 1, episodeNumber: 6, airDate: Today.AddDays(6), name: "E6")
            ]);
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Other A",
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(10), name: "A1")]);
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Other B",
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(11), name: "B1")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var items = await repository.GetFollowedEpisodeReleasesForHomeAsync(UserId, Today, 3);

        Assert.Equal(3, items.Count);
        Assert.Equal(
            ["Many Episodes", "Other A", "Other B"],
            items.Select(item => item.Title).ToList());
        Assert.Equal(Today.AddDays(1), items[0].ReleaseDate);
        Assert.Equal(1, items[0].EpisodeNumber);
    }

    [Fact]
    public async Task GetFollowedEpisodeReleasesForHomeAsync_OrdersByReleaseDateThenContentId()
    {
        await using var context = CreateContext();
        var showA = await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Show A",
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(5), name: "A1")]);
        var showB = await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Show B",
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(5), name: "B1")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var items = await repository.GetFollowedEpisodeReleasesForHomeAsync(UserId, Today, 10);

        Assert.Equal(2, items.Count);
        var expectedContentIds = new[] { showA, showB }
            .OrderBy(_ => Today.AddDays(5))
            .ThenBy(id => id)
            .ToList();
        Assert.Equal(expectedContentIds, items.Select(item => item.ContentId).ToList());
    }

    [Fact]
    public async Task GetFollowedUpcomingForHomeAsync_SuppressesTvPremiereWhenSeasonOneEpisodeOneSharesDate()
    {
        await using var context = CreateContext();
        var sharedDate = Today.AddDays(4);
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Reina",
            firstAirDate: sharedDate,
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: sharedDate, name: "Pilot")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var items = await repository.GetFollowedUpcomingForHomeAsync(UserId, Today, "TR", 5);

        var episode = Assert.Single(items);
        Assert.Equal(CatalogUpcomingKind.TvEpisode, episode.UpcomingKind);
        Assert.Equal("Reina", episode.Title);
        Assert.Equal(1, episode.SeasonNumber);
        Assert.Equal(1, episode.EpisodeNumber);
        Assert.Equal(sharedDate, episode.ReleaseDate);
    }

    [Fact]
    public async Task GetFollowedUpcomingForHomeAsync_KeepsTvPremiereWhenSeasonOneEpisodeOneMissing()
    {
        await using var context = CreateContext();
        var premiereDate = Today.AddDays(4);
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Premiere Only",
            firstAirDate: premiereDate,
            episodes: [(seasonNumber: 1, episodeNumber: 2, airDate: Today.AddDays(10), name: "E2")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var items = await repository.GetFollowedUpcomingForHomeAsync(UserId, Today, "TR", 5);

        Assert.Equal(2, items.Count);
        var premiere = Assert.Single(items, item => item.UpcomingKind == CatalogUpcomingKind.TvShowPremiere);
        Assert.Equal(premiereDate, premiere.ReleaseDate);
    }

    [Fact]
    public async Task GetFollowedUpcomingForHomeAsync_KeepsTvPremiereWhenSeasonOneEpisodeOneHasNoAirDate()
    {
        await using var context = CreateContext();
        var premiereDate = Today.AddDays(4);
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Undated Pilot",
            firstAirDate: premiereDate,
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: null, name: "Pilot")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var items = await repository.GetFollowedUpcomingForHomeAsync(UserId, Today, "TR", 5);

        var premiere = Assert.Single(items);
        Assert.Equal(CatalogUpcomingKind.TvShowPremiere, premiere.UpcomingKind);
    }

    [Fact]
    public async Task GetFollowedUpcomingForHomeAsync_KeepsTvPremiereWhenDatesDifferFromSeasonOneEpisodeOne()
    {
        await using var context = CreateContext();
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Split Dates",
            firstAirDate: Today.AddDays(4),
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: Today.AddDays(5), name: "Pilot")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var items = await repository.GetFollowedUpcomingForHomeAsync(UserId, Today, "TR", 5);

        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.UpcomingKind == CatalogUpcomingKind.TvShowPremiere);
        Assert.Contains(items, item => item.UpcomingKind == CatalogUpcomingKind.TvEpisode);
    }

    [Fact]
    public async Task GetFollowedUpcomingForHomeAsync_DoesNotRemoveDifferentShowsOnSameDate()
    {
        await using var context = CreateContext();
        var sharedDate = Today.AddDays(3);
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Show A",
            firstAirDate: sharedDate,
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: sharedDate, name: "A1")]);
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "Show B",
            firstAirDate: sharedDate,
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: sharedDate, name: "B1")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var items = await repository.GetFollowedUpcomingForHomeAsync(UserId, Today, "TR", 5);

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal(CatalogUpcomingKind.TvEpisode, item.UpcomingKind));
        Assert.Equal(2, items.Select(item => item.Title).Distinct().Count());
    }

    [Fact]
    public async Task GetFollowedUpcomingForHomeAsync_LimitStillAppliedAfterPremiereDedup()
    {
        await using var context = CreateContext();
        var utcNow = DateTime.UtcNow;
        var movieId = Guid.NewGuid();
        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Movie",
            ReleaseDate = Today.AddDays(1),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.CatalogFollows.Add(CatalogFollow.CreateMovieFollow(UserId, movieId, utcNow));

        for (var index = 0; index < 6; index++)
        {
            var sharedDate = Today.AddDays(index + 2);
            await SeedTvShowWithEpisodesAsync(
                context,
                followUserId: UserId,
                title: $"Show {index}",
                firstAirDate: sharedDate,
                episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: sharedDate, name: $"E{index}")]);
        }

        var repository = new CatalogFollowCatalogRepository(context);
        var items = await repository.GetFollowedUpcomingForHomeAsync(UserId, Today, "TR", 5);

        Assert.Equal(5, items.Count);
        Assert.DoesNotContain(items, item => item.UpcomingKind == CatalogUpcomingKind.TvShowPremiere);
    }

    [Fact]
    public async Task GetFollowedUpcomingCatalogAsync_SuppressesTvPremiereWhenSeasonOneEpisodeOneSharesDate()
    {
        await using var context = CreateContext();
        var sharedDate = Today.AddDays(4);
        await SeedTvShowWithEpisodesAsync(
            context,
            followUserId: UserId,
            title: "See All Show",
            firstAirDate: sharedDate,
            episodes: [(seasonNumber: 1, episodeNumber: 1, airDate: sharedDate, name: "Pilot")]);
        var repository = new CatalogFollowCatalogRepository(context);

        var (items, _) = await repository.GetFollowedUpcomingCatalogAsync(UserId, 1, 10, Today, "TR");

        var episode = Assert.Single(items);
        Assert.Equal(CatalogUpcomingKind.TvEpisode, episode.UpcomingKind);
    }

    [Fact]
    public async Task GetUpcomingCatalogAsync_PagePastTheEndDoesNotOverflowOrLoadRows()
    {
        await using var context = CreateContext();
        var repository = new CatalogFollowCatalogRepository(context);

        var (items, totalCount) = await repository.GetUpcomingCatalogAsync(
            null,
            page: int.MaxValue,
            pageSize: 100,
            Today,
            "TR");

        Assert.Empty(items);
        Assert.Equal(0, totalCount);
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
        DateOnly? firstAirDate = null,
        IReadOnlyList<(int seasonNumber, int episodeNumber, DateOnly? airDate, string name)>? episodes = null)
    {
        episodes ??= [(1, 1, Today.AddDays(2), "Episode")];
        var utcNow = DateTime.UtcNow;
        var tvShowId = Guid.NewGuid();
        var tvShow = new TvShow
        {
            Id = tvShowId,
            Title = title,
            FirstAirDate = firstAirDate,
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

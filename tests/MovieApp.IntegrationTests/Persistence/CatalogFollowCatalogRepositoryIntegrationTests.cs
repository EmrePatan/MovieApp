using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;
using Npgsql;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class CatalogFollowCatalogRepositoryIntegrationTests
{
    [Fact]
    public async Task CatalogFollowUserCreatedAtIndexExistsOnPostgreSql()
    {
        await using var connection = new NpgsqlConnection(IntegrationTestDatabase.GetConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT indexname
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND tablename = 'catalog_follows'
              AND indexname = 'IX_catalog_follows_UserId_CreatedAt'
            """;

        var indexName = await command.ExecuteScalarAsync() as string;

        Assert.Equal("IX_catalog_follows_UserId_CreatedAt", indexName);
    }

    [Fact]
    public async Task GetFollowingCatalogAsyncOrdersByCreatedAtDescendingAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearCatalogFollowDataAsync(context);

        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var olderMovieId = Guid.NewGuid();
        var newerTvShowId = Guid.NewGuid();

        context.Users.Add(new User
        {
            Id = userId,
            UserName = $"following-order-{Guid.NewGuid():N}",
            Email = $"following-order-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.Movies.Add(new Movie
        {
            Id = olderMovieId,
            Title = "Older Follow Movie",
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.TvShows.Add(new TvShow
        {
            Id = newerTvShowId,
            Title = "Newer Follow Show",
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.CatalogFollows.AddRange(
            CatalogFollow.CreateMovieFollow(userId, olderMovieId, utcNow.AddMinutes(-10)),
            CatalogFollow.CreateTvFollow(userId, newerTvShowId, true, true, utcNow));
        await context.SaveChangesAsync();

        var repository = new CatalogFollowCatalogRepository(context);
        var (items, totalCount) = await repository.GetFollowingCatalogAsync(userId, page: 1, pageSize: 10);

        Assert.Equal(2, totalCount);
        Assert.Equal(newerTvShowId, items[0].ContentId);
        Assert.Equal(CatalogContentType.Tv, items[0].ContentType);
        Assert.Equal(olderMovieId, items[1].ContentId);
        Assert.Equal(CatalogContentType.Movie, items[1].ContentType);
    }

    [Fact]
    public async Task GetUpcomingCatalogAsyncPaginatesMixedMovieAndTvWithoutDuplicatesOrGaps()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearUpcomingCatalogDataAsync(context);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var utcNow = DateTime.UtcNow;
        var movieIds = Enumerable.Range(0, 5)
            .Select(_ => Guid.NewGuid())
            .ToArray();
        var tvIds = Enumerable.Range(0, 5)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        context.Movies.AddRange(movieIds.Select((id, index) => new Movie
        {
            Id = id,
            Title = $"Future Movie {index}",
            ReleaseDate = today.AddDays(10 + index),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }));
        context.TvShows.AddRange(tvIds.Select((id, index) => new TvShow
        {
            Id = id,
            Title = $"Future Show {index}",
            FirstAirDate = today.AddDays(5 + index),
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }));
        await context.SaveChangesAsync();

        var repository = new CatalogFollowCatalogRepository(context);
        var page1 = await repository.GetUpcomingCatalogAsync(null, page: 1, pageSize: 4, today, "TR");
        var page2 = await repository.GetUpcomingCatalogAsync(null, page: 2, pageSize: 4, today, "TR");

        Assert.Equal(10, page1.TotalCount);
        Assert.Equal(4, page1.Items.Count);
        Assert.Equal(4, page2.Items.Count);

        var combinedPages = page1.Items
            .Concat(page2.Items)
            .Select(item => (item.ContentId, item.ContentType))
            .ToList();

        Assert.Equal(8, combinedPages.Distinct().Count());

        var expectedGlobalOrder = tvIds
            .Select((id, index) => (ContentId: id, ContentType: CatalogContentType.Tv, ReleaseDate: today.AddDays(5 + index)))
            .Concat(movieIds.Select((id, index) => (ContentId: id, ContentType: CatalogContentType.Movie, ReleaseDate: today.AddDays(10 + index))))
            .OrderBy(item => item.ReleaseDate)
            .ThenBy(item => item.ContentType)
            .ThenBy(item => item.ContentId)
            .ToList();

        Assert.Equal(
            expectedGlobalOrder.Take(4).Select(item => item.ContentId).ToList(),
            page1.Items.Select(item => item.ContentId).ToList());
        Assert.Equal(
            expectedGlobalOrder.Skip(4).Take(4).Select(item => item.ContentId).ToList(),
            page2.Items.Select(item => item.ContentId).ToList());
    }

    [Fact]
    public async Task GetUpcomingCatalogAsyncUsesDeterministicOrderingForEqualReleaseDates()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearUpcomingCatalogDataAsync(context);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sharedDate = today.AddDays(30);
        var utcNow = DateTime.UtcNow;
        var movieA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var movieB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var tvA = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var tvB = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        context.Movies.AddRange(
            new Movie
            {
                Id = movieB,
                Title = "Movie B",
                ReleaseDate = sharedDate,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            },
            new Movie
            {
                Id = movieA,
                Title = "Movie A",
                ReleaseDate = sharedDate,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
        context.TvShows.AddRange(
            new TvShow
            {
                Id = tvB,
                Title = "Show B",
                FirstAirDate = sharedDate,
                Status = TvShowStatus.ReturningSeries,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            },
            new TvShow
            {
                Id = tvA,
                Title = "Show A",
                FirstAirDate = sharedDate,
                Status = TvShowStatus.ReturningSeries,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
        await context.SaveChangesAsync();

        var repository = new CatalogFollowCatalogRepository(context);
        var firstPage = await repository.GetUpcomingCatalogAsync(null, page: 1, pageSize: 10, today, "TR");
        var secondPage = await repository.GetUpcomingCatalogAsync(null, page: 1, pageSize: 10, today, "TR");

        Assert.Equal(
            [movieA, movieB, tvA, tvB],
            firstPage.Items.Select(item => item.ContentId).ToList());
        Assert.Equal(
            firstPage.Items.Select(item => item.ContentId).ToList(),
            secondPage.Items.Select(item => item.ContentId).ToList());
    }

    [Fact]
    public async Task GetUpcomingCatalogAsyncUsesRegionalFutureDateWhenSynced()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearUpcomingCatalogDataAsync(context);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var utcNow = DateTime.UtcNow;
        var movieId = Guid.NewGuid();

        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Regional Future Movie",
            ReleaseDate = today.AddDays(-5),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.MovieRegionalReleases.Add(new MovieRegionalRelease
        {
            MovieId = movieId,
            Region = "TR",
            EffectiveReleaseDate = today.AddDays(14),
            EffectiveReleaseType = Domain.Enums.TmdbReleaseType.Theatrical,
            IsFallbackGlobal = false,
            SyncedAtUtc = utcNow
        });
        await context.SaveChangesAsync();

        var repository = new CatalogFollowCatalogRepository(context);
        var (items, totalCount) = await repository.GetUpcomingCatalogAsync(null, page: 1, pageSize: 10, today, "TR");

        Assert.Equal(1, totalCount);
        Assert.Equal(movieId, items[0].ContentId);
        Assert.Equal(today.AddDays(14), items[0].ReleaseDate);
    }

    [Fact]
    public async Task GetUpcomingCatalogAsyncExcludesRegionalReleasedMovieEvenWhenGlobalFuture()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearUpcomingCatalogDataAsync(context);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var utcNow = DateTime.UtcNow;
        var movieId = Guid.NewGuid();

        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Regional Released Movie",
            ReleaseDate = today.AddDays(30),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.MovieRegionalReleases.Add(new MovieRegionalRelease
        {
            MovieId = movieId,
            Region = "TR",
            EffectiveReleaseDate = today.AddDays(-1),
            EffectiveReleaseType = Domain.Enums.TmdbReleaseType.Digital,
            IsFallbackGlobal = false,
            SyncedAtUtc = utcNow
        });
        await context.SaveChangesAsync();

        var repository = new CatalogFollowCatalogRepository(context);
        var (items, totalCount) = await repository.GetUpcomingCatalogAsync(null, page: 1, pageSize: 10, today, "TR");

        Assert.Equal(0, totalCount);
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetUpcomingCatalogAsyncDistinguishesMovieAndTvFollowState()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearUpcomingCatalogDataAsync(context);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var utcNow = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var sharedContentId = Guid.NewGuid();
        var movieId = Guid.NewGuid();
        var tvShowId = Guid.NewGuid();

        context.Users.Add(new User
        {
            Id = userId,
            UserName = $"upcoming-follow-{Guid.NewGuid():N}",
            Email = $"upcoming-follow-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Followed Movie",
            ReleaseDate = today.AddDays(7),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            Title = "Unfollowed Show",
            FirstAirDate = today.AddDays(3),
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.CatalogFollows.Add(CatalogFollow.CreateMovieFollow(userId, movieId, utcNow));
        context.CatalogFollows.Add(CatalogFollow.CreateTvFollow(userId, sharedContentId, true, true, utcNow));
        await context.SaveChangesAsync();

        var repository = new CatalogFollowCatalogRepository(context);
        var (items, totalCount) = await repository.GetUpcomingCatalogAsync(userId, page: 1, pageSize: 10, today, "TR");

        Assert.Equal(2, totalCount);
        Assert.Equal(tvShowId, items[0].ContentId);
        Assert.False(items[0].IsFollowed);
        Assert.Equal(movieId, items[1].ContentId);
        Assert.True(items[1].IsFollowed);
    }

    private static async Task ClearCatalogFollowDataAsync(ApplicationDbContext context)
    {
        context.CatalogFollows.RemoveRange(context.CatalogFollows);
        context.Movies.RemoveRange(context.Movies);
        context.TvShows.RemoveRange(context.TvShows);
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();
    }

    private static async Task ClearUpcomingCatalogDataAsync(ApplicationDbContext context)
    {
        context.CatalogFollows.RemoveRange(context.CatalogFollows);
        context.MovieRegionalReleases.RemoveRange(context.MovieRegionalReleases);
        context.Movies.RemoveRange(context.Movies);
        context.TvShows.RemoveRange(context.TvShows);
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();
    }
}

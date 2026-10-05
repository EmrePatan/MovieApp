using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Library;

[Collection("CatalogPersistence")]
public sealed class LibraryActionStatusRepositoryIntegrationTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid MovieId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid TvShowId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid OtherTvShowId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EpisodeId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid OtherEpisodeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime UtcNow = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetMovieAsyncEmptyStateReturnsExpectedDefaults()
    {
        await using var context = Persistence.CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var movieId = Guid.NewGuid();
        await SeedUserAndMovieAsync(context, userId, movieId);

        var snapshot = await new LibraryActionStatusRepository(context).GetMovieAsync(userId, movieId);

        Assert.Equal("movie", snapshot.MediaType);
        Assert.False(snapshot.IsFavorited);
        Assert.False(snapshot.IsInWatchlist);
        Assert.Empty(snapshot.WatchlistIds);
        Assert.False(snapshot.IsFollowing);
        Assert.False(snapshot.IsWatched);
        Assert.Null(snapshot.WatchedAt);
    }

    [Fact]
    public async Task GetMovieAsyncReturnsDistinctWatchlistIdsFromMultipleLists()
    {
        await using var context = Persistence.CatalogPersistenceFixture.CreateContext();
        await SeedUserAndMovieAsync(context);
        var watchlistA = Watchlist.Create(UserId, "A", UtcNow);
        var watchlistB = Watchlist.Create(UserId, "B", UtcNow);
        watchlistA.Items.Add(WatchlistItem.CreateForMovie(watchlistA.Id, MovieId, UtcNow));
        watchlistB.Items.Add(WatchlistItem.CreateForMovie(watchlistB.Id, MovieId, UtcNow));
        context.AddRange(watchlistA, watchlistB);
        await context.SaveChangesAsync();

        var snapshot = await new LibraryActionStatusRepository(context).GetMovieAsync(UserId, MovieId);

        Assert.True(snapshot.IsInWatchlist);
        Assert.Equal(2, snapshot.WatchlistIds.Count);
    }

    [Fact]
    public async Task GetMovieAsyncReturnsFavoriteWatchlistFollowAndWatchedTogether()
    {
        await using var context = Persistence.CatalogPersistenceFixture.CreateContext();
        await SeedUserAndMovieAsync(context);
        var watchlist = Watchlist.Create(UserId, "Later", UtcNow);
        watchlist.Items.Add(WatchlistItem.CreateForMovie(watchlist.Id, MovieId, UtcNow));
        context.Add(watchlist);
        context.Add(Favorite.CreateForMovie(UserId, MovieId, UtcNow));
        context.Add(CatalogFollow.CreateMovieFollow(UserId, MovieId, UtcNow));
        context.Add(WatchedMovie.Create(UserId, MovieId, UtcNow));
        await context.SaveChangesAsync();

        var snapshot = await new LibraryActionStatusRepository(context).GetMovieAsync(UserId, MovieId);

        Assert.True(snapshot.IsFavorited);
        Assert.True(snapshot.IsInWatchlist);
        Assert.True(snapshot.IsFollowing);
        Assert.True(snapshot.IsWatched);
        Assert.Equal(UtcNow, snapshot.WatchedAt);
    }

    [Fact]
    public async Task GetTvShowAsyncWithoutEpisodeLeavesIsWatchedNull()
    {
        await using var context = Persistence.CatalogPersistenceFixture.CreateContext();
        await SeedUserAsync(context);
        await SeedTvShowGraphAsync(context, TvShowId, EpisodeId);

        var snapshot = await new LibraryActionStatusRepository(context).GetTvShowAsync(UserId, TvShowId, null);

        Assert.False(snapshot.IsFollowing);
        Assert.True(snapshot.NotifyNewSeasons);
        Assert.True(snapshot.NotifyNewEpisodes);
        Assert.Null(snapshot.IsWatched);
    }

    [Fact]
    public async Task GetTvShowAsyncWithEpisodeIdButUnwatchedReturnsFalseNotNull()
    {
        await using var context = Persistence.CatalogPersistenceFixture.CreateContext();
        await SeedUserAsync(context);
        await SeedTvShowGraphAsync(context, TvShowId, EpisodeId);

        var snapshot = await new LibraryActionStatusRepository(context).GetTvShowAsync(UserId, TvShowId, EpisodeId);

        Assert.False(snapshot.IsWatched);
        Assert.Null(snapshot.WatchedAt);
    }

    [Fact]
    public async Task GetTvShowAsyncWrongShowEpisodeDoesNotCountAsWatched()
    {
        await using var context = Persistence.CatalogPersistenceFixture.CreateContext();
        await SeedUserAsync(context);
        await SeedTvShowGraphAsync(context, TvShowId, EpisodeId);
        await SeedTvShowGraphAsync(context, OtherTvShowId, OtherEpisodeId);
        context.Add(WatchedEpisode.Create(UserId, OtherEpisodeId, UtcNow));
        await context.SaveChangesAsync();

        var snapshot = await new LibraryActionStatusRepository(context)
            .GetTvShowAsync(UserId, TvShowId, OtherEpisodeId);

        Assert.False(snapshot.IsWatched);
        Assert.Null(snapshot.WatchedAt);
    }

    [Fact]
    public async Task GetMovieAsyncUsesSinglePostgreSqlCommand()
    {
        var counter = new CommandCounter();
        await using var context = CreateContextWithCommandCounter(counter);
        await SeedUserAndMovieAsync(context);

        counter.Reset();
        _ = await new LibraryActionStatusRepository(context).GetMovieAsync(UserId, MovieId);

        Assert.Equal(1, counter.Value);
    }

    [Fact]
    public async Task GetTvShowAsyncWithoutEpisodeUsesSinglePostgreSqlCommand()
    {
        var counter = new CommandCounter();
        await using var context = CreateContextWithCommandCounter(counter);
        await SeedUserAsync(context);
        await SeedTvShowGraphAsync(context, TvShowId, EpisodeId);

        counter.Reset();
        _ = await new LibraryActionStatusRepository(context).GetTvShowAsync(UserId, TvShowId, null);

        Assert.Equal(1, counter.Value);
    }

    [Fact]
    public async Task GetTvShowAsyncWithEpisodeUsesSinglePostgreSqlCommand()
    {
        var counter = new CommandCounter();
        await using var context = CreateContextWithCommandCounter(counter);
        await SeedUserAsync(context);
        await SeedTvShowGraphAsync(context, TvShowId, EpisodeId);

        counter.Reset();
        _ = await new LibraryActionStatusRepository(context).GetTvShowAsync(UserId, TvShowId, EpisodeId);

        Assert.Equal(1, counter.Value);
    }

    private static ApplicationDbContext CreateContextWithCommandCounter(CommandCounter counter)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                IntegrationTestDatabase.GetConnectionString(),
                npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3))
            .LogTo(
                _ => counter.Increment(),
                [RelationalEventId.CommandExecuted])
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class CommandCounter
    {
        private int _value;

        public int Value => _value;

        public void Increment() => Interlocked.Increment(ref _value);

        public void Reset() => _value = 0;
    }

    private static async Task SeedUserAsync(ApplicationDbContext context, Guid? userId = null)
    {
        var resolvedUserId = userId ?? UserId;
        if (await context.Users.AnyAsync(user => user.Id == resolvedUserId))
        {
            return;
        }

        var email = $"library-actions-{resolvedUserId:N}@test.local";
        context.Users.Add(new User
        {
            Id = resolvedUserId,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = $"library-actions-{resolvedUserId:N}",
            DisplayName = "Library Actions Test",
            PasswordHash = "hash",
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedUserAndMovieAsync(
        ApplicationDbContext context,
        Guid? userId = null,
        Guid? movieId = null)
    {
        var resolvedUserId = userId ?? UserId;
        var resolvedMovieId = movieId ?? MovieId;
        await SeedUserAsync(context, resolvedUserId);
        if (await context.Movies.AnyAsync(movie => movie.Id == resolvedMovieId))
        {
            return;
        }

        context.Movies.Add(new Movie
        {
            Id = resolvedMovieId,
            Title = "Library Actions Movie",
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedTvShowGraphAsync(ApplicationDbContext context, Guid tvShowId, Guid episodeId)
    {
        if (await context.TvShows.AnyAsync(show => show.Id == tvShowId))
        {
            return;
        }

        var tvShow = new TvShow
        {
            Id = tvShowId,
            Title = $"Show {tvShowId}",
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
        };
        var season = new Season
        {
            Id = Guid.NewGuid(),
            TvShowId = tvShowId,
            TvShow = tvShow,
            SeasonNumber = 1,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
        };
        var episode = new Episode
        {
            Id = episodeId,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = 1,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
        };
        season.Episodes.Add(episode);
        tvShow.Seasons.Add(season);
        context.Add(tvShow);
        await context.SaveChangesAsync();
    }
}

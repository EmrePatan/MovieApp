using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Library;

public sealed class LibraryActionStatusRepositoryTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid MovieId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid TvShowId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid EpisodeId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly DateTime UtcNow = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetMovieAsyncReturnsFavoriteWatchlistFollowAndWatchedTogether()
    {
        await using var context = CreateContext();
        var watchlist = Watchlist.Create(UserId, "Later", UtcNow);
        var item = WatchlistItem.CreateForMovie(watchlist.Id, MovieId, UtcNow);
        watchlist.Items.Add(item);
        context.Add(watchlist);
        context.Add(Favorite.CreateForMovie(UserId, MovieId, UtcNow));
        context.Add(CatalogFollow.CreateMovieFollow(UserId, MovieId, UtcNow));
        context.Add(WatchedMovie.Create(UserId, MovieId, UtcNow));
        await context.SaveChangesAsync();

        var snapshot = await new LibraryActionStatusRepository(context).GetMovieAsync(UserId, MovieId);

        Assert.Equal("movie", snapshot.MediaType);
        Assert.True(snapshot.IsFavorited);
        Assert.True(snapshot.IsInWatchlist);
        Assert.Equal(watchlist.Id, Assert.Single(snapshot.WatchlistIds));
        Assert.True(snapshot.IsFollowing);
        Assert.True(snapshot.IsWatched);
        Assert.Equal(UtcNow, snapshot.WatchedAt);
    }

    [Fact]
    public async Task GetTvShowAsyncUsesFollowDefaultsAndEpisodeWatchedState()
    {
        await using var context = CreateContext();
        var follow = CatalogFollow.CreateTvFollow(UserId, TvShowId, notifyNewSeasons: false, notifyNewEpisodes: true, UtcNow);
        context.Add(follow);
        context.Add(WatchedEpisode.Create(UserId, EpisodeId, UtcNow));
        await context.SaveChangesAsync();

        var repository = new LibraryActionStatusRepository(context);
        var withEpisode = await repository.GetTvShowAsync(UserId, TvShowId, EpisodeId);
        var withoutEpisode = await repository.GetTvShowAsync(UserId, Guid.NewGuid(), null);

        Assert.True(withEpisode.IsFollowing);
        Assert.False(withEpisode.NotifyNewSeasons);
        Assert.True(withEpisode.NotifyNewEpisodes);
        Assert.False(withEpisode.BaselineEstablished);
        Assert.True(withEpisode.IsWatched);
        Assert.Equal(UtcNow, withEpisode.WatchedAt);

        Assert.False(withoutEpisode.IsFollowing);
        Assert.True(withoutEpisode.NotifyNewSeasons);
        Assert.True(withoutEpisode.NotifyNewEpisodes);
        Assert.Null(withoutEpisode.IsWatched);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"library-actions-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }
}

using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Services.Watchlists;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Watchlists;

public sealed class GetWatchlistMembershipServiceTests
{
    [Fact]
    public async Task GetMovieMembershipAsyncReturnsWatchlistIds()
    {
        var userId = Guid.NewGuid();
        var movieId = Guid.NewGuid();
        var watchlistIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var repository = new FakeWatchlistItemRepository { MovieWatchlistIds = watchlistIds };
        var service = new GetWatchlistMembershipService(new FakeCurrentUser(userId), repository);

        var result = await service.GetMovieMembershipAsync(movieId);

        Assert.True(result.IsInWatchlist);
        Assert.Equal(watchlistIds, result.WatchlistIds);
        Assert.Equal(userId, repository.LastUserId);
        Assert.Equal(movieId, repository.LastMovieId);
    }

    [Fact]
    public async Task GetTvShowMembershipAsyncReturnsEmptyWhenNotMember()
    {
        var userId = Guid.NewGuid();
        var tvShowId = Guid.NewGuid();
        var repository = new FakeWatchlistItemRepository();
        var service = new GetWatchlistMembershipService(new FakeCurrentUser(userId), repository);

        var result = await service.GetTvShowMembershipAsync(tvShowId);

        Assert.False(result.IsInWatchlist);
        Assert.Empty(result.WatchlistIds);
        Assert.Equal(userId, repository.LastUserId);
        Assert.Equal(tvShowId, repository.LastTvShowId);
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class FakeWatchlistItemRepository : IWatchlistItemRepository
    {
        public IReadOnlyList<Guid> MovieWatchlistIds { get; init; } = [];

        public IReadOnlyList<Guid> TvShowWatchlistIds { get; init; } = [];

        public Guid? LastUserId { get; private set; }

        public Guid? LastMovieId { get; private set; }

        public Guid? LastTvShowId { get; private set; }

        public Task<IReadOnlyList<Guid>> GetWatchlistIdsContainingMovieAsync(
            Guid userId,
            Guid movieId,
            CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            LastMovieId = movieId;
            return Task.FromResult(MovieWatchlistIds);
        }

        public Task<IReadOnlyList<Guid>> GetWatchlistIdsContainingTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            LastTvShowId = tvShowId;
            return Task.FromResult(TvShowWatchlistIds);
        }

        public Task<bool> ExistsForMovieAsync(Guid watchlistId, Guid movieId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> ExistsForTvShowAsync(Guid watchlistId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> TryAddAsync(WatchlistItem item, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> RemoveForMovieAsync(Guid watchlistId, Guid movieId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> RemoveForTvShowAsync(Guid watchlistId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<(IReadOnlyList<WatchlistItem> Items, int TotalCount)> GetItemsAsync(
            Guid watchlistId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<WatchlistItem>, int)>(([], 0));

        public Task<IReadOnlyList<WatchlistItem>> GetAllItemsAsync(
            Guid watchlistId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WatchlistItem>>([]);

        public Task<int> GetItemCountAsync(Guid watchlistId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyDictionary<Guid, int>> GetItemCountsByWatchlistIdsAsync(
            IReadOnlyCollection<Guid> watchlistIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());
    }
}

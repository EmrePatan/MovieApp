using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Watchlists;
using MovieApp.Application.Services.Watchlists;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Watchlists;

public sealed class UpdateWatchlistServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid WatchlistId = Guid.NewGuid();

    [Fact]
    public async Task UpdateAsyncRenamesWatchlist()
    {
        var watchlist = Watchlist.Create(UserId, "Weekend Watch", DateTime.UtcNow);
        watchlist.Id = WatchlistId;

        var repository = new FakeWatchlistRepository(watchlist, existsByName: false);
        var service = new UpdateWatchlistService(
            new FakeCurrentUser(UserId),
            repository,
            new FakeWatchlistItemRepository());

        var result = await service.UpdateAsync(
            WatchlistId,
            new UpdateWatchlistRequest("Friday Night"));

        Assert.Equal("Friday Night", result.Name);
        Assert.Equal("Friday Night", watchlist.Name);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task UpdateAsyncThrowsNotFoundWhenWatchlistMissing()
    {
        var service = new UpdateWatchlistService(
            new FakeCurrentUser(UserId),
            new FakeWatchlistRepository(watchlist: null, existsByName: false),
            new FakeWatchlistItemRepository());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateAsync(WatchlistId, new UpdateWatchlistRequest("Friday Night")));
    }

    [Fact]
    public async Task UpdateAsyncThrowsConflictForDuplicateName()
    {
        var watchlist = Watchlist.Create(UserId, "Weekend Watch", DateTime.UtcNow);
        watchlist.Id = WatchlistId;

        var service = new UpdateWatchlistService(
            new FakeCurrentUser(UserId),
            new FakeWatchlistRepository(watchlist, existsByName: true),
            new FakeWatchlistItemRepository());

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateAsync(WatchlistId, new UpdateWatchlistRequest("Friday Night")));
    }

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId is not null;

        public Guid? UserId => userId;
    }

    private sealed class FakeWatchlistRepository(Watchlist? watchlist, bool existsByName) : IWatchlistRepository
    {
        public int SaveChangesCount { get; private set; }

        public Task<Watchlist?> GetByIdForUserAsync(
            Guid userId,
            Guid watchlistId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Watchlist?>(watchlist);

        public Task<Watchlist?> GetTrackedByIdForUserAsync(
            Guid userId,
            Guid watchlistId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Watchlist?>(watchlist);

        public Task<IReadOnlyList<Watchlist>> GetUserWatchlistsAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Watchlist>>([]);

        public Task<bool> ExistsByNormalizedNameAsync(
            Guid userId,
            string normalizedName,
            Guid? excludeWatchlistId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(existsByName);

        public Task<Watchlist> AddAsync(Watchlist watchlist, CancellationToken cancellationToken = default) =>
            Task.FromResult(watchlist);

        public Task<bool> DeleteAsync(Guid userId, Guid watchlistId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }

        public Task TouchAsync(Guid watchlistId, DateTime utcNow, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeWatchlistItemRepository : IWatchlistItemRepository
    {
        public Task<bool> ExistsForMovieAsync(
            Guid watchlistId,
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> ExistsForTvShowAsync(
            Guid watchlistId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> TryAddAsync(WatchlistItem item, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> RemoveForMovieAsync(
            Guid watchlistId,
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> RemoveForTvShowAsync(
            Guid watchlistId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
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
            Task.FromResult(3);

        public Task<IReadOnlyDictionary<Guid, int>> GetItemCountsByWatchlistIdsAsync(
            IReadOnlyCollection<Guid> watchlistIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(
                watchlistIds.ToDictionary(id => id, _ => 3));

        public Task<IReadOnlyList<Guid>> GetWatchlistIdsContainingMovieAsync(
            Guid userId,
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyList<Guid>> GetWatchlistIdsContainingTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}

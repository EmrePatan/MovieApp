using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Watchlists;
using MovieApp.Application.Services.Watchlists;
using MovieApp.Domain.Entities;

using MovieApp.UnitTests.Caching;

namespace MovieApp.UnitTests.Watchlists;

public sealed class AddMovieToWatchlistServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly Guid WatchlistId = Guid.NewGuid();
    private static readonly Guid MovieId = Guid.NewGuid();

    [Fact]
    public async Task AddAsyncCreatesItemForOwnedWatchlist()
    {
        var service = CreateService(
            new FakeCurrentUser(UserId),
            new FakeWatchlistRepository(UserId, WatchlistId),
            new FakeWatchlistItemRepository(exists: false, tryAddReturns: true),
            new FakeMovieRepository(CreateMovie()));

        var result = await service.AddAsync(WatchlistId, MovieId);

        Assert.Equal(WatchlistItemMutationResult.Created, result);
    }

    [Fact]
    public async Task AddAsyncThrowsNotFoundForAnotherUsersWatchlist()
    {
        var service = CreateService(
            new FakeCurrentUser(OtherUserId),
            new FakeWatchlistRepository(UserId, WatchlistId),
            new FakeWatchlistItemRepository(exists: false, tryAddReturns: true),
            new FakeMovieRepository(CreateMovie()));

        await Assert.ThrowsAsync<NotFoundException>(() => service.AddAsync(WatchlistId, MovieId));
    }

    private static AddMovieToWatchlistService CreateService(
        ICurrentUser currentUser,
        IWatchlistRepository watchlistRepository,
        IWatchlistItemRepository watchlistItemRepository,
        IMovieRepository movieRepository) =>
        new(currentUser, watchlistRepository, watchlistItemRepository, movieRepository, new FakeProfileStatisticsCache());

    private static Movie CreateMovie() =>
        new()
        {
            Id = MovieId,
            Title = "Interstellar",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId is not null;

        public Guid? UserId => userId;
    }

    private sealed class FakeWatchlistRepository(Guid ownerId, Guid watchlistId) : IWatchlistRepository
    {
        public Task<Watchlist?> GetByIdForUserAsync(
            Guid userId,
            Guid requestedWatchlistId,
            CancellationToken cancellationToken = default)
        {
            if (userId != ownerId || requestedWatchlistId != watchlistId)
            {
                return Task.FromResult<Watchlist?>(null);
            }

            return Task.FromResult<Watchlist?>(new Watchlist
            {
                Id = watchlistId,
                UserId = ownerId,
                Name = "Weekend",
                NormalizedName = "weekend",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        public Task<IReadOnlyList<Watchlist>> GetUserWatchlistsAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Watchlist>>([]);

        public Task<bool> ExistsByNormalizedNameAsync(
            Guid userId,
            string normalizedName,
            Guid? excludeWatchlistId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<Watchlist> AddAsync(Watchlist watchlist, CancellationToken cancellationToken = default) =>
            Task.FromResult(watchlist);

        public Task<bool> DeleteAsync(Guid userId, Guid requestedWatchlistId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task TouchAsync(Guid requestedWatchlistId, DateTime utcNow, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeWatchlistItemRepository(bool exists, bool tryAddReturns) : IWatchlistItemRepository
    {
        public Task<bool> ExistsForMovieAsync(
            Guid watchlistId,
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(exists);

        public Task<bool> ExistsForTvShowAsync(
            Guid watchlistId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> TryAddAsync(WatchlistItem item, CancellationToken cancellationToken = default) =>
            Task.FromResult(tryAddReturns);

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
            Task.FromResult(0);

        public Task<IReadOnlyDictionary<Guid, int>> GetItemCountsByWatchlistIdsAsync(
            IReadOnlyCollection<Guid> watchlistIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

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

    private sealed class FakeMovieRepository(Movie? movie) : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(movie);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie> UpsertFromProviderAsync(
            Application.Models.Providers.MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

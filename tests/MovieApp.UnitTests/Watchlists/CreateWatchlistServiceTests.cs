using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Watchlists;
using MovieApp.Application.Services.Watchlists;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Watchlists;

namespace MovieApp.UnitTests.Watchlists;

public sealed class CreateWatchlistServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task CreateAsyncCreatesWatchlist()
    {
        var repository = new FakeWatchlistRepository(existsByName: false);
        var service = new CreateWatchlistService(new FakeCurrentUser(UserId), repository);

        var result = await service.CreateAsync(new CreateWatchlistRequest("Weekend Watch"));

        Assert.Equal("Weekend Watch", result.Name);
        Assert.Equal(0, result.ItemCount);
        Assert.Equal(1, repository.AddCount);
    }

    [Fact]
    public async Task CreateAsyncThrowsConflictForDuplicateName()
    {
        var service = new CreateWatchlistService(
            new FakeCurrentUser(UserId),
            new FakeWatchlistRepository(existsByName: true));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(new CreateWatchlistRequest("Weekend Watch")));
    }

    [Fact]
    public async Task CreateAsyncThrowsValidationExceptionForEmptyName()
    {
        var service = new CreateWatchlistService(
            new FakeCurrentUser(UserId),
            new FakeWatchlistRepository(existsByName: false));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(new CreateWatchlistRequest("   ")));
    }

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId is not null;

        public Guid? UserId => userId;
    }

    private sealed class FakeWatchlistRepository(bool existsByName) : IWatchlistRepository
    {
        public int AddCount { get; private set; }

        public Task<Watchlist?> GetByIdForUserAsync(
            Guid userId,
            Guid watchlistId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Watchlist?>(null);

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

        public Task<Watchlist> AddAsync(Watchlist watchlist, CancellationToken cancellationToken = default)
        {
            AddCount++;
            return Task.FromResult(watchlist);
        }

        public Task<bool> DeleteAsync(Guid userId, Guid watchlistId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task TouchAsync(Guid watchlistId, DateTime utcNow, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

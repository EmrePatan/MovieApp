using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Identity;
using MovieApp.Application.Services.WatchlistShare;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.WatchlistShare;

public sealed class WatchlistShareServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task EnableAsync_CreatesActiveShareWithUrl()
    {
        var repo = new FakeShareRepository();
        var items = new FakeWatchlistItemRepository();
        var service = CreateService(repo, items, UserId);

        var result = await service.EnableAsync();

        Assert.True(result.CreatedNewLink);
        Assert.Contains("/watchlist/", result.ShareUrl, StringComparison.Ordinal);
        Assert.True(repo.Share is { IsActive: true });
    }

    [Fact]
    public async Task DisableAsync_DeactivatesShare()
    {
        var repo = new FakeShareRepository();
        var items = new FakeWatchlistItemRepository();
        var service = CreateService(repo, items, UserId);
        var enabled = await service.EnableAsync();
        var token = enabled.ShareUrl.Split("/watchlist/", StringSplitOptions.None)[1];

        await service.DisableAsync();
        var publicResult = await service.TryGetPublicByTokenAsync(token);

        Assert.Null(publicResult);
        Assert.False(repo.Share!.IsActive);
    }

    [Fact]
    public async Task RotateAsync_InvalidatesOldToken()
    {
        var repo = new FakeShareRepository();
        var items = new FakeWatchlistItemRepository();
        var service = CreateService(repo, items, UserId);
        var enabled = await service.EnableAsync();
        var oldToken = enabled.ShareUrl.Split("/watchlist/", StringSplitOptions.None)[1];

        var rotated = await service.RotateAsync();
        var oldPublic = await service.TryGetPublicByTokenAsync(oldToken);
        var newToken = rotated.ShareUrl.Split("/watchlist/", StringSplitOptions.None)[1];
        var newPublic = await service.TryGetPublicByTokenAsync(newToken);

        Assert.Null(oldPublic);
        Assert.NotNull(newPublic);
    }

    [Fact]
    public async Task TryGetPublicByTokenAsync_ReflectsLiveWatchlistChanges()
    {
        var repo = new FakeShareRepository();
        var items = new FakeWatchlistItemRepository();
        var service = CreateService(repo, items, UserId);
        var enabled = await service.EnableAsync();
        var token = enabled.ShareUrl.Split("/watchlist/", StringSplitOptions.None)[1];

        var first = await service.TryGetPublicByTokenAsync(token);
        items.AddSecondMovie();
        var second = await service.TryGetPublicByTokenAsync(token);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Single(first!.Items);
        Assert.Equal(2, second!.Items.Count);
    }

    [Fact]
    public async Task TryGetPublicByTokenAsync_DoesNotExposeUserId()
    {
        var repo = new FakeShareRepository();
        var items = new FakeWatchlistItemRepository();
        var service = CreateService(repo, items, UserId);
        var enabled = await service.EnableAsync();
        var token = enabled.ShareUrl.Split("/watchlist/", StringSplitOptions.None)[1];

        var publicResult = await service.TryGetPublicByTokenAsync(token);

        Assert.NotNull(publicResult);
        Assert.Equal("Emre", publicResult!.OwnerDisplayName);
        Assert.Single(publicResult.Items);
    }

    private static WatchlistShareService CreateService(
        FakeShareRepository repo,
        FakeWatchlistItemRepository items,
        Guid userId) =>
        new(
            new FakeCurrentUser(userId),
            repo,
            items,
            Options.Create(new CatalogShareOptions { PublicWebBaseUrl = "https://moviecaveapp.com" }));

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
    }

    private sealed class FakeShareRepository : IUserWatchlistShareRepository
    {
        public UserWatchlistShare? Share { get; private set; }

        public Task<UserWatchlistShare?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Share);

        public Task<UserWatchlistShare?> GetActiveByTokenHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default)
        {
            if (Share is null || !Share.IsActive || Share.TokenHash != tokenHash)
            {
                return Task.FromResult<UserWatchlistShare?>(null);
            }

            return Task.FromResult<UserWatchlistShare?>(Share);
        }

        public Task<UserWatchlistShare> AddAsync(UserWatchlistShare share, CancellationToken cancellationToken = default)
        {
            Share = share;
            Share.User = new User { Id = share.UserId, DisplayName = "Emre", Email = "emre@example.com" };
            return Task.FromResult(share);
        }

        public Task UpdateAsync(UserWatchlistShare share, CancellationToken cancellationToken = default)
        {
            Share = share;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWatchlistItemRepository : IWatchlistItemRepository
    {
        private readonly List<WatchlistItem> _items = [];

        public FakeWatchlistItemRepository()
        {
            var movie = new Movie
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Title = "Inception",
                VoteAverage = 8.8m,
            };
            _items.Add(new WatchlistItem { MovieId = movie.Id, Movie = movie, CreatedAt = DateTime.UtcNow });
        }

        public void AddSecondMovie()
        {
            var movie = new Movie
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Title = "Arrival",
                VoteAverage = 7.9m,
            };
            _items.Add(new WatchlistItem { MovieId = movie.Id, Movie = movie, CreatedAt = DateTime.UtcNow });
        }

        public Task<IReadOnlyList<WatchlistItem>> GetDistinctItemsForUserAsync(
            Guid userId,
            int maxItems,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WatchlistItem>>(_items.Take(maxItems).ToList());

        public Task<bool> ExistsForMovieAsync(Guid watchlistId, Guid movieId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExistsForTvShowAsync(Guid watchlistId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> TryAddAsync(WatchlistItem item, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RemoveForMovieAsync(Guid watchlistId, Guid movieId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RemoveForTvShowAsync(Guid watchlistId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<WatchlistItem> Items, int TotalCount)> GetItemsAsync(
            Guid watchlistId,
            Application.Models.Search.SearchContentType mediaType,
            Application.Models.Watchlists.WatchlistItemsSort sort,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<WatchlistItem>> GetAllItemsAsync(Guid watchlistId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> GetItemCountAsync(Guid watchlistId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, int>> GetItemCountsByWatchlistIdsAsync(
            IReadOnlyCollection<Guid> watchlistIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetWatchlistIdsContainingMovieAsync(
            Guid userId,
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetWatchlistIdsContainingTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

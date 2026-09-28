using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Services.WatchlistShare;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.WatchlistShare;

public sealed class WatchlistShareServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid WatchlistA = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid WatchlistB = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task EnableAsync_MultipleLists_ProducesIndependentTokens()
    {
        var repo = new FakeShareRepository();
        var items = new FakeWatchlistItemRepository();
        var service = CreateService(repo, items, UserId, [WatchlistA, WatchlistB]);

        var enabledA = await service.EnableAsync(WatchlistA);
        var enabledB = await service.EnableAsync(WatchlistB);

        var tokenA = TokenFromUrl(enabledA.ShareUrl);
        var tokenB = TokenFromUrl(enabledB.ShareUrl);

        Assert.NotEqual(tokenA, tokenB);
        Assert.Equal(2, repo.ActiveShareCount);

        var publicA = await service.TryGetPublicByTokenAsync(tokenA);
        var publicB = await service.TryGetPublicByTokenAsync(tokenB);

        Assert.NotNull(publicA);
        Assert.NotNull(publicB);
        Assert.Equal("Weekend", publicA!.WatchlistName);
        Assert.Equal("Work", publicB!.WatchlistName);
    }

    [Fact]
    public async Task ListActiveSharesForUserAsync_ReturnsOnlyActiveSharesForCurrentUser()
    {
        var repo = new FakeShareRepository();
        var items = new FakeWatchlistItemRepository();
        var service = CreateService(repo, items, UserId, [WatchlistA, WatchlistB]);

        await service.EnableAsync(WatchlistA);
        await service.EnableAsync(WatchlistB);
        await service.DisableAsync(WatchlistA);

        var active = await service.ListActiveSharesForUserAsync();

        Assert.Single(active);
        Assert.Equal(WatchlistB, active[0].WatchlistId);
        Assert.Equal("Work", active[0].WatchlistName);
    }

    [Fact]
    public async Task DisableAsync_OnlyAffectsRequestedWatchlist()
    {
        var repo = new FakeShareRepository();
        var items = new FakeWatchlistItemRepository();
        var service = CreateService(repo, items, UserId, [WatchlistA, WatchlistB]);

        var enabledA = await service.EnableAsync(WatchlistA);
        var enabledB = await service.EnableAsync(WatchlistB);
        var tokenA = TokenFromUrl(enabledA.ShareUrl);
        var tokenB = TokenFromUrl(enabledB.ShareUrl);

        await service.DisableAsync(WatchlistA);

        Assert.Null(await service.TryGetPublicByTokenAsync(tokenA));
        Assert.NotNull(await service.TryGetPublicByTokenAsync(tokenB));
    }

    [Fact]
    public async Task RotateAsync_OnlyInvalidatesRotatedWatchlistToken()
    {
        var repo = new FakeShareRepository();
        var items = new FakeWatchlistItemRepository();
        var service = CreateService(repo, items, UserId, [WatchlistA, WatchlistB]);

        var enabledA = await service.EnableAsync(WatchlistA);
        var enabledB = await service.EnableAsync(WatchlistB);
        var oldTokenA = TokenFromUrl(enabledA.ShareUrl);
        var tokenB = TokenFromUrl(enabledB.ShareUrl);

        var rotatedA = await service.RotateAsync(WatchlistA);
        var newTokenA = TokenFromUrl(rotatedA.ShareUrl);

        Assert.Null(await service.TryGetPublicByTokenAsync(oldTokenA));
        Assert.NotNull(await service.TryGetPublicByTokenAsync(newTokenA));
        Assert.NotNull(await service.TryGetPublicByTokenAsync(tokenB));
    }

    [Fact]
    public async Task EnableAsync_LegacyUserIdUniqueConstraint_ReassignsShareToSecondWatchlist()
    {
        var repo = new LegacyUserIdUniqueFakeShareRepository();
        var items = new FakeWatchlistItemRepository();
        var service = CreateService(repo, items, UserId, [WatchlistA, WatchlistB]);

        var enabledA = await service.EnableAsync(WatchlistA);
        var tokenA = TokenFromUrl(enabledA.ShareUrl);

        var enabledB = await service.EnableAsync(WatchlistB);
        var tokenB = TokenFromUrl(enabledB.ShareUrl);

        Assert.NotEqual(tokenA, tokenB);
        Assert.Equal(1, repo.StoredShareCount);
        Assert.Null(await service.TryGetPublicByTokenAsync(tokenA));
        Assert.NotNull(await service.TryGetPublicByTokenAsync(tokenB));
        Assert.Equal("Work", (await service.TryGetPublicByTokenAsync(tokenB))!.WatchlistName);
    }

    [Fact]
    public async Task EnableAsync_SecondList_DoesNotRepointFirstToken()
    {
        var repo = new FakeShareRepository();
        var items = new FakeWatchlistItemRepository();
        var service = CreateService(repo, items, UserId, [WatchlistA, WatchlistB]);

        var enabledA = await service.EnableAsync(WatchlistA);
        var tokenA = TokenFromUrl(enabledA.ShareUrl);
        var before = await service.TryGetPublicByTokenAsync(tokenA);

        await service.EnableAsync(WatchlistB);
        var after = await service.TryGetPublicByTokenAsync(tokenA);

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.Equal("Weekend", before!.WatchlistName);
        Assert.Equal("Weekend", after!.WatchlistName);
    }

    [Fact]
    public async Task TryGetPublicByTokenAsync_ReflectsLiveItemsForThatWatchlistOnly()
    {
        var repo = new FakeShareRepository();
        var items = new FakeWatchlistItemRepository();
        var service = CreateService(repo, items, UserId, [WatchlistA, WatchlistB]);

        var enabledA = await service.EnableAsync(WatchlistA);
        var enabledB = await service.EnableAsync(WatchlistB);
        var tokenA = TokenFromUrl(enabledA.ShareUrl);
        var tokenB = TokenFromUrl(enabledB.ShareUrl);

        items.AddSecondMovieTo(WatchlistA);

        var publicA = await service.TryGetPublicByTokenAsync(tokenA);
        var publicB = await service.TryGetPublicByTokenAsync(tokenB);

        Assert.Equal(2, publicA!.Items.Count);
        Assert.Single(publicB!.Items);
    }

    [Fact]
    public async Task EnableAsync_OtherUsersWatchlist_ThrowsNotFound()
    {
        var repo = new FakeShareRepository();
        var items = new FakeWatchlistItemRepository();
        var service = CreateService(repo, items, UserId, [WatchlistA]);

        await Assert.ThrowsAsync<NotFoundException>(() => service.EnableAsync(WatchlistB));
    }

    [Fact]
    public async Task TryGetPublicByTokenAsync_DoesNotExposeUserId()
    {
        var repo = new FakeShareRepository();
        var items = new FakeWatchlistItemRepository();
        var service = CreateService(repo, items, UserId, [WatchlistA]);
        var enabled = await service.EnableAsync(WatchlistA);
        var token = TokenFromUrl(enabled.ShareUrl);

        var publicResult = await service.TryGetPublicByTokenAsync(token);

        Assert.NotNull(publicResult);
        Assert.Equal("Emre", publicResult!.OwnerDisplayName);
        Assert.Single(publicResult.Items);
    }

    private static string TokenFromUrl(string url) =>
        url.Split("/watchlist/", StringSplitOptions.None)[1];

    private static WatchlistShareService CreateService(
        FakeShareRepository repo,
        FakeWatchlistItemRepository items,
        Guid userId,
        IReadOnlyCollection<Guid> ownedWatchlistIds) =>
        new(
            new FakeCurrentUser(userId),
            repo,
            new FakeWatchlistRepository(userId, ownedWatchlistIds),
            items,
            Options.Create(new CatalogShareOptions { PublicWebBaseUrl = "https://moviecaveapp.com" }));

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
    }

    private sealed class FakeWatchlistRepository(Guid ownerId, IReadOnlyCollection<Guid> ownedWatchlistIds)
        : IWatchlistRepository
    {
        private static readonly Dictionary<Guid, string> Names = new()
        {
            [WatchlistA] = "Weekend",
            [WatchlistB] = "Work",
        };

        public Task<Watchlist?> GetByIdForUserAsync(Guid userId, Guid watchlistId, CancellationToken cancellationToken = default) =>
            Task.FromResult(
                userId == ownerId && ownedWatchlistIds.Contains(watchlistId)
                    ? new Watchlist { Id = watchlistId, UserId = ownerId, Name = Names.GetValueOrDefault(watchlistId, "List") }
                    : null);

        public Task<Watchlist?> GetTrackedByIdForUserAsync(Guid userId, Guid watchlistId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Watchlist>> GetUserWatchlistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExistsByNormalizedNameAsync(
            Guid userId,
            string normalizedName,
            Guid? excludeWatchlistId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Watchlist> AddAsync(Watchlist watchlist, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid userId, Guid watchlistId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task TouchAsync(Guid watchlistId, DateTime utcNow, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private class FakeShareRepository : IUserWatchlistShareRepository
    {
        protected readonly Dictionary<Guid, UserWatchlistShare> ByWatchlistId = new();

        public int ActiveShareCount => ByWatchlistId.Count;

        public Task<UserWatchlistShare?> GetByWatchlistIdAsync(
            Guid watchlistId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ByWatchlistId.TryGetValue(watchlistId, out var share) ? share : null);

        public Task<UserWatchlistShare?> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ByWatchlistId.Values.FirstOrDefault(share => share.UserId == userId));

        public Task<IReadOnlyList<UserWatchlistShare>> ListActiveByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var shares = ByWatchlistId.Values
                .Where(entry => entry.UserId == userId && entry.IsActive)
                .Select(entry =>
                {
                    entry.Watchlist = new Watchlist
                    {
                        Id = entry.WatchlistId,
                        UserId = entry.UserId,
                        Name = entry.WatchlistId == WatchlistA ? "Weekend" : "Work",
                    };
                    return entry;
                })
                .OrderBy(entry => entry.Watchlist!.Name)
                .ToList();
            return Task.FromResult<IReadOnlyList<UserWatchlistShare>>(shares);
        }

        public Task<UserWatchlistShare?> GetActiveByTokenHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default)
        {
            var share = ByWatchlistId.Values.FirstOrDefault(
                entry => entry.IsActive && entry.TokenHash == tokenHash);
            if (share is null)
            {
                return Task.FromResult<UserWatchlistShare?>(null);
            }

            share.User = new User { Id = share.UserId, DisplayName = "Emre", Email = "emre@example.com" };
            share.Watchlist = new Watchlist
            {
                Id = share.WatchlistId,
                UserId = share.UserId,
                Name = share.WatchlistId == WatchlistA ? "Weekend" : "Work",
            };
            return Task.FromResult<UserWatchlistShare?>(share);
        }

        public virtual Task<UserWatchlistShare> AddAsync(
            UserWatchlistShare share,
            CancellationToken cancellationToken = default)
        {
            ByWatchlistId[share.WatchlistId] = share;
            return Task.FromResult(share);
        }

        public Task UpdateAsync(UserWatchlistShare share, CancellationToken cancellationToken = default)
        {
            foreach (var key in ByWatchlistId
                         .Where(entry => entry.Value.Id == share.Id && entry.Key != share.WatchlistId)
                         .Select(entry => entry.Key)
                         .ToList())
            {
                ByWatchlistId.Remove(key);
            }

            ByWatchlistId[share.WatchlistId] = share;
            return Task.CompletedTask;
        }
    }

    private sealed class LegacyUserIdUniqueFakeShareRepository : FakeShareRepository
    {
        public int StoredShareCount => ActiveShareCount;

        public override Task<UserWatchlistShare> AddAsync(
            UserWatchlistShare share,
            CancellationToken cancellationToken = default)
        {
            if (ByWatchlistId.Values.Any(entry => entry.UserId == share.UserId))
            {
                throw new ConflictException("Legacy unique UserId index.");
            }

            return base.AddAsync(share, cancellationToken);
        }
    }

    private sealed class FakeWatchlistItemRepository : IWatchlistItemRepository
    {
        private readonly Dictionary<Guid, List<WatchlistItem>> _itemsByWatchlist = new();

        public FakeWatchlistItemRepository()
        {
            Seed(WatchlistA, "Inception");
            Seed(WatchlistB, "Arrival");
        }

        private void Seed(Guid watchlistId, string title)
        {
            var movie = new Movie
            {
                Id = Guid.NewGuid(),
                Title = title,
                VoteAverage = 8m,
            };
            _itemsByWatchlist[watchlistId] =
            [
                new WatchlistItem
                {
                    WatchlistId = watchlistId,
                    MovieId = movie.Id,
                    Movie = movie,
                    CreatedAt = DateTime.UtcNow,
                },
            ];
        }

        public void AddSecondMovieTo(Guid watchlistId)
        {
            var movie = new Movie
            {
                Id = Guid.NewGuid(),
                Title = "Extra",
                VoteAverage = 7m,
            };
            _itemsByWatchlist[watchlistId].Add(new WatchlistItem
            {
                WatchlistId = watchlistId,
                MovieId = movie.Id,
                Movie = movie,
                CreatedAt = DateTime.UtcNow,
            });
        }

        public Task<IReadOnlyList<WatchlistItem>> GetAllItemsAsync(Guid watchlistId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WatchlistItem>>(
                _itemsByWatchlist.TryGetValue(watchlistId, out var items) ? items : []);

        public Task<IReadOnlyList<WatchlistItem>> GetDistinctItemsForUserAsync(
            Guid userId,
            int maxItems,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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

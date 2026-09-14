using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Services.TvShowFollows;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.TvShowFollows;

public sealed class GetTvShowFollowStatusServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TvShowId = Guid.NewGuid();

    [Fact]
    public async Task GetAsyncReturnsNotFollowingDefaultsWhenMissing()
    {
        var service = new GetTvShowFollowStatusService(
            new FakeCurrentUser(UserId),
            new FakeTvShowFollowRepository(null));

        var status = await service.GetAsync(TvShowId);

        Assert.False(status.IsFollowing);
        Assert.True(status.NotifyNewSeasons);
        Assert.True(status.NotifyNewEpisodes);
        Assert.False(status.BaselineEstablished);
    }

    [Fact]
    public async Task GetAsyncReturnsFollowingState()
    {
        var follow = CatalogFollow.CreateTvFollow(UserId, TvShowId, false, true, DateTime.UtcNow);
        var service = new GetTvShowFollowStatusService(
            new FakeCurrentUser(UserId),
            new FakeTvShowFollowRepository(follow));

        var status = await service.GetAsync(TvShowId);

        Assert.True(status.IsFollowing);
        Assert.False(status.NotifyNewSeasons);
        Assert.True(status.NotifyNewEpisodes);
        Assert.False(status.BaselineEstablished);
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class FakeTvShowFollowRepository(CatalogFollow? follow) : ITvShowFollowRepository
    {
        public Task<CatalogFollow?> GetForUserAndTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(follow);

        public Task<CatalogFollow?> GetForUserAndTvShowForUpdateAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(follow);

        public Task<bool> TryAddAsync(CatalogFollow follow, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> RemoveForTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<(IReadOnlyList<CatalogFollow> Follows, int TotalCount)> GetUserFollowsAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<CatalogFollow>, int)>(([], 0));
    }
}

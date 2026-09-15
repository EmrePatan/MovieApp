using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.TvShowFollows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.TvShowFollows;

public sealed class GetTvShowFollowsServiceTests
{
    [Fact]
    public async Task GetAsyncUsesBatchTvShowRetrievalForMultipleFollows()
    {
        var utcNow = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var tvShowIds = Enumerable.Range(0, 3)
            .Select(_ => Guid.NewGuid())
            .ToList();
        var follows = tvShowIds
            .Select(id => CatalogFollow.CreateTvFollow(userId, id, true, true, utcNow))
            .ToList();
        var repository = new TrackingTvShowRepository
        {
            KnownIds = tvShowIds.ToHashSet()
        };
        var service = new GetTvShowFollowsService(
            new FixedCurrentUser(userId),
            new FixedTvShowFollowRepository(follows),
            repository);

        var result = await service.GetAsync(page: 1, pageSize: 20);

        Assert.Equal(3, result.TvShows.Count);
        Assert.Equal(1, repository.GetByIdsCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
        Assert.All(repository.GetByIdsRequests, request => Assert.DoesNotContain(
            request,
            id => repository.LoadedWithSeasonGraph.Contains(id)));
    }

    [Fact]
    public async Task GetAsyncSkipsMissingCatalogRecordsWithoutChangingPagination()
    {
        var utcNow = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var presentId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        var follows = new[]
        {
            CatalogFollow.CreateTvFollow(userId, presentId, true, true, utcNow.AddMinutes(-1)),
            CatalogFollow.CreateTvFollow(userId, missingId, true, true, utcNow)
        };
        var repository = new TrackingTvShowRepository
        {
            KnownIds = [presentId]
        };
        var service = new GetTvShowFollowsService(
            new FixedCurrentUser(userId),
            new FixedTvShowFollowRepository(follows),
            repository);

        var result = await service.GetAsync(page: 1, pageSize: 20);

        Assert.Single(result.TvShows);
        Assert.Equal(presentId, result.TvShows[0].Id);
        Assert.Equal(2, result.TotalCount);
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;

        public bool IsAuthenticated => true;
    }

    private sealed class FixedTvShowFollowRepository(IReadOnlyList<CatalogFollow> follows) : ITvShowFollowRepository
    {
        public Task<CatalogFollow?> GetForUserAndTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CatalogFollow?> GetForUserAndTvShowForUpdateAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> TryAddAsync(CatalogFollow follow, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RemoveForTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<CatalogFollow> Follows, int TotalCount)> GetUserFollowsAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<CatalogFollow>, int)>((follows, follows.Count));
    }

    private sealed class TrackingTvShowRepository : ITvShowRepository
    {
        public HashSet<Guid> KnownIds { get; init; } = [];

        public int GetByIdCallCount { get; private set; }

        public int GetByIdsCallCount { get; private set; }

        public List<IReadOnlyList<Guid>> GetByIdsRequests { get; } = [];

        public HashSet<Guid> LoadedWithSeasonGraph { get; } = [];

        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            LoadedWithSeasonGraph.Add(id);
            return Task.FromResult<TvShow?>(KnownIds.Contains(id) ? CreateTvShow(id) : null);
        }

        public Task<IReadOnlyDictionary<Guid, TvShow>> GetByIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken cancellationToken = default)
        {
            GetByIdsCallCount++;
            GetByIdsRequests.Add(ids);

            return Task.FromResult<IReadOnlyDictionary<Guid, TvShow>>(
                ids
                    .Where(KnownIds.Contains)
                    .Select(CreateTvShow)
                    .ToDictionary(tvShow => tvShow.Id));
        }

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private static TvShow CreateTvShow(Guid id) => new()
        {
            Id = id,
            Title = $"Show {id:N}",
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}

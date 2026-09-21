using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.TvShowFollows;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.TvShowFollows;
using MovieApp.Application.Services.TvShowFollows;
using MovieApp.Domain.Entities;
using TvShowFollowBaselineException = MovieApp.Application.Exceptions.TvShowFollowBaselineException;

namespace MovieApp.UnitTests.TvShowFollows;

public sealed class UpsertTvShowFollowServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TvShowId = Guid.NewGuid();

    [Fact]
    public async Task UpsertAsyncCreatesFollowWithDefaults()
    {
        var repository = new FakeTvShowFollowRepository();
        var enqueuer = new FakeTvShowFollowBaselineJobEnqueuer(repository);
        var service = CreateService(repository, tvShowExists: true, enqueuer);

        var (mutation, status) = await service.UpsertAsync(
            TvShowId,
            new TvShowFollowPreferencesUpdate(null, null));

        Assert.Equal(TvShowFollowMutationResult.Created, mutation);
        Assert.True(status.IsFollowing);
        Assert.True(status.NotifyNewSeasons);
        Assert.True(status.NotifyNewEpisodes);
        Assert.True(status.BaselineEstablished);
        Assert.Equal(1, enqueuer.EnqueueCallCount);
    }

    [Fact]
    public async Task UpsertAsyncReturnsPendingBaselineWhenEnqueueIsDeferred()
    {
        var repository = new FakeTvShowFollowRepository();
        var enqueuer = new FakeTvShowFollowBaselineJobEnqueuer(repository) { ExecuteSynchronously = false };
        var service = CreateService(repository, tvShowExists: true, enqueuer);

        var (_, status) = await service.UpsertAsync(
            TvShowId,
            new TvShowFollowPreferencesUpdate(null, null));

        Assert.True(status.IsFollowing);
        Assert.False(status.BaselineEstablished);
        Assert.Equal(1, enqueuer.EnqueueCallCount);
        Assert.Equal(0, enqueuer.BaselineService.EstablishCallCount);
    }

    [Fact]
    public async Task UpsertAsyncCreatesFollowWithExplicitPreferences()
    {
        var repository = new FakeTvShowFollowRepository();
        var service = CreateService(repository, tvShowExists: true);

        var (_, status) = await service.UpsertAsync(
            TvShowId,
            new TvShowFollowPreferencesUpdate(false, true));

        Assert.True(status.IsFollowing);
        Assert.False(status.NotifyNewSeasons);
        Assert.True(status.NotifyNewEpisodes);
    }

    [Fact]
    public async Task UpsertAsyncUpdatesSinglePreferenceAndPreservesOther()
    {
        var repository = new FakeTvShowFollowRepository();
        var existing = CatalogFollow.CreateTvFollow(UserId, TvShowId, true, true, DateTime.UtcNow);
        repository.Seed(existing);

        var service = CreateService(repository, tvShowExists: true);

        var (mutation, status) = await service.UpsertAsync(
            TvShowId,
            new TvShowFollowPreferencesUpdate(false, null));

        Assert.Equal(TvShowFollowMutationResult.Updated, mutation);
        Assert.False(status.NotifyNewSeasons);
        Assert.True(status.NotifyNewEpisodes);
    }

    [Fact]
    public async Task UpsertAsyncThrowsValidationWhenUpdatingWithoutPreferences()
    {
        var repository = new FakeTvShowFollowRepository();
        var existing = CatalogFollow.CreateTvFollow(UserId, TvShowId, true, true, DateTime.UtcNow);
        existing.SetNotifyFromUtc(DateTime.UtcNow, DateTime.UtcNow);
        existing.EstablishBaseline(DateTime.UtcNow);
        repository.Seed(existing);
        var service = CreateService(repository, tvShowExists: true);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpsertAsync(TvShowId, new TvShowFollowPreferencesUpdate(null, null)));
    }

    [Fact]
    public async Task UpsertAsyncThrowsNotFoundWhenTvShowMissing()
    {
        var service = CreateService(new FakeTvShowFollowRepository(), tvShowExists: false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpsertAsync(TvShowId, new TvShowFollowPreferencesUpdate(null, null)));
    }

    [Fact]
    public async Task UpsertAsyncRetriesCreateWhenConcurrentInsertWins()
    {
        var repository = new ConcurrentWinTvShowFollowRepository();
        var enqueuer = new FakeTvShowFollowBaselineJobEnqueuer(repository);
        var service = CreateService(repository, tvShowExists: true, enqueuer);

        var (mutation, status) = await service.UpsertAsync(
            TvShowId,
            new TvShowFollowPreferencesUpdate(true, true));

        Assert.Equal(TvShowFollowMutationResult.Updated, mutation);
        Assert.True(status.IsFollowing);
        Assert.True(status.NotifyNewSeasons);
        Assert.True(status.NotifyNewEpisodes);
    }

    [Fact]
    public async Task UpsertAsyncEstablishedFollowUpdateDoesNotRerunBaseline()
    {
        var repository = new FakeTvShowFollowRepository();
        var existing = CatalogFollow.CreateTvFollow(UserId, TvShowId, true, true, DateTime.UtcNow);
        existing.SetNotifyFromUtc(DateTime.UtcNow, DateTime.UtcNow);
        existing.EstablishBaseline(DateTime.UtcNow);
        repository.Seed(existing);

        var enqueuer = new FakeTvShowFollowBaselineJobEnqueuer(repository);
        var service = CreateService(repository, tvShowExists: true, enqueuer);

        await service.UpsertAsync(TvShowId, new TvShowFollowPreferencesUpdate(false, null));

        Assert.Equal(0, enqueuer.EnqueueCallCount);
    }

    [Fact]
    public async Task UpsertAsyncUnestablishedFollowRetriesBaseline()
    {
        var repository = new FakeTvShowFollowRepository();
        var existing = CatalogFollow.CreateTvFollow(UserId, TvShowId, true, true, DateTime.UtcNow);
        existing.SetNotifyFromUtc(new DateTime(2026, 9, 14, 21, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
        repository.Seed(existing);

        var enqueuer = new FakeTvShowFollowBaselineJobEnqueuer(repository);
        var service = CreateService(repository, tvShowExists: true, enqueuer);

        var (_, status) = await service.UpsertAsync(TvShowId, new TvShowFollowPreferencesUpdate(null, null));

        Assert.Equal(1, enqueuer.EnqueueCallCount);
        Assert.True(status.BaselineEstablished);
    }

    [Fact]
    public async Task UpsertAsyncProviderBaselineFailurePreservesNotifyFromUtc()
    {
        var repository = new FakeTvShowFollowRepository();
        var enqueuer = new FakeTvShowFollowBaselineJobEnqueuer(repository)
        {
            BaselineService = { ShouldFail = true }
        };
        var service = CreateService(repository, tvShowExists: true, enqueuer);

        await Assert.ThrowsAsync<TvShowFollowBaselineException>(() =>
            service.UpsertAsync(TvShowId, new TvShowFollowPreferencesUpdate(null, null)));

        Assert.NotNull(repository.SeededFollow?.NotifyFromUtc);
    }

    private static UpsertTvShowFollowService CreateService(
        ITvShowFollowRepository repository,
        bool tvShowExists,
        FakeTvShowFollowBaselineJobEnqueuer? enqueuer = null) =>
        new(
            new FakeCurrentUser(UserId),
            repository,
            new FakeTvShowRepository(tvShowExists ? CreateTvShow() : null),
            enqueuer ?? new FakeTvShowFollowBaselineJobEnqueuer(repository));

    private static TvShow CreateTvShow() =>
        new()
        {
            Id = TvShowId,
            Title = "Test Show",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class FakeTvShowRepository(TvShow? tvShow) : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(tvShow);

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<TvShow> UpsertFromProviderAsync(
            Application.Models.Providers.TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTvShowFollowBaselineJobEnqueuer(ITvShowFollowRepository repository)
        : ITvShowFollowBaselineJobEnqueuer
    {
        public FakeTvShowFollowBaselineService BaselineService { get; set; } = new();

        public bool ExecuteSynchronously { get; set; } = true;

        public int EnqueueCallCount { get; private set; }

        public async Task EnqueueAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default)
        {
            EnqueueCallCount++;

            if (!ExecuteSynchronously)
            {
                return;
            }

            var follow = await repository.GetForUserAndTvShowForUpdateAsync(userId, tvShowId, cancellationToken);
            if (follow is not null)
            {
                await BaselineService.EstablishAsync(follow, cancellationToken);
            }
        }
    }

    private sealed class FakeTvShowFollowBaselineService : ITvShowFollowBaselineService
    {
        public int EstablishCallCount { get; private set; }

        public bool ShouldFail { get; set; }

        public Task EstablishAsync(CatalogFollow follow, CancellationToken cancellationToken = default)
        {
            EstablishCallCount++;

            if (ShouldFail)
            {
                throw new TvShowFollowBaselineException("Baseline failed.");
            }

            follow.EstablishBaseline(DateTime.UtcNow);
            return Task.CompletedTask;
        }
    }

    private sealed class ConcurrentWinTvShowFollowRepository : ITvShowFollowRepository
    {
        private CatalogFollow? _follow;

        public Task<CatalogFollow?> GetForUserAndTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_follow);

        public Task<CatalogFollow?> GetForUserAndTvShowForUpdateAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_follow);

        public Task<bool> TryAddAsync(CatalogFollow follow, CancellationToken cancellationToken = default)
        {
            if (_follow is null)
            {
                _follow = CatalogFollow.CreateTvFollow(
                    follow.UserId,
                    follow.ContentId,
                    follow.NotifyNewSeasons,
                    follow.NotifyNewEpisodes,
                    follow.CreatedAt);
            }

            return Task.FromResult(false);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> RemoveForTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(IReadOnlyList<CatalogFollow> Follows, int TotalCount)> GetUserFollowsAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<CatalogFollow>, int)>(([], 0));
    }

    private sealed class FakeTvShowFollowRepository : ITvShowFollowRepository
    {
        private CatalogFollow? _follow;

        public CatalogFollow? SeededFollow => _follow;

        public void Seed(CatalogFollow follow) => _follow = follow;

        public Task<CatalogFollow?> GetForUserAndTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_follow);

        public Task<CatalogFollow?> GetForUserAndTvShowForUpdateAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_follow);

        public Task<bool> TryAddAsync(CatalogFollow follow, CancellationToken cancellationToken = default)
        {
            if (_follow is not null)
            {
                return Task.FromResult(false);
            }

            _follow = follow;
            return Task.FromResult(true);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> RemoveForTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default)
        {
            _follow = null;
            return Task.FromResult(true);
        }

        public Task<(IReadOnlyList<CatalogFollow> Follows, int TotalCount)> GetUserFollowsAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<CatalogFollow>, int)>(
                (_follow is null ? [] : new List<CatalogFollow> { _follow }, _follow is null ? 0 : 1));
    }
}

using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
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
        var service = CreateService(repository, tvShowExists: true);

        var (mutation, status) = await service.UpsertAsync(
            TvShowId,
            new TvShowFollowPreferencesUpdate(null, null));

        Assert.Equal(TvShowFollowMutationResult.Created, mutation);
        Assert.True(status.IsFollowing);
        Assert.True(status.NotifyNewSeasons);
        Assert.True(status.NotifyNewEpisodes);
        Assert.True(status.BaselineEstablished);
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
        var existing = TvShowFollow.Create(UserId, TvShowId, true, true, DateTime.UtcNow);
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
        var existing = TvShowFollow.Create(UserId, TvShowId, true, true, DateTime.UtcNow);
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
        var service = CreateService(repository, tvShowExists: true, new FakeTvShowFollowBaselineService());

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
        var existing = TvShowFollow.Create(UserId, TvShowId, true, true, DateTime.UtcNow);
        existing.SetNotifyFromUtc(DateTime.UtcNow, DateTime.UtcNow);
        existing.EstablishBaseline(DateTime.UtcNow);
        repository.Seed(existing);

        var baselineService = new FakeTvShowFollowBaselineService();
        var service = CreateService(repository, tvShowExists: true, baselineService);

        await service.UpsertAsync(TvShowId, new TvShowFollowPreferencesUpdate(false, null));

        Assert.Equal(0, baselineService.EstablishCallCount);
    }

    [Fact]
    public async Task UpsertAsyncUnestablishedFollowRetriesBaseline()
    {
        var repository = new FakeTvShowFollowRepository();
        var existing = TvShowFollow.Create(UserId, TvShowId, true, true, DateTime.UtcNow);
        existing.SetNotifyFromUtc(new DateTime(2026, 9, 14, 21, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
        repository.Seed(existing);

        var baselineService = new FakeTvShowFollowBaselineService();
        var service = CreateService(repository, tvShowExists: true, baselineService);

        var (_, status) = await service.UpsertAsync(TvShowId, new TvShowFollowPreferencesUpdate(null, null));

        Assert.Equal(1, baselineService.EstablishCallCount);
        Assert.True(status.BaselineEstablished);
    }

    [Fact]
    public async Task UpsertAsyncProviderBaselineFailurePreservesNotifyFromUtc()
    {
        var repository = new FakeTvShowFollowRepository();
        var baselineService = new FakeTvShowFollowBaselineService { ShouldFail = true };
        var service = CreateService(repository, tvShowExists: true, baselineService);

        await Assert.ThrowsAsync<TvShowFollowBaselineException>(() =>
            service.UpsertAsync(TvShowId, new TvShowFollowPreferencesUpdate(null, null)));

        Assert.NotNull(repository.SeededFollow?.NotifyFromUtc);
    }

    private static UpsertTvShowFollowService CreateService(
        ITvShowFollowRepository repository,
        bool tvShowExists,
        FakeTvShowFollowBaselineService? baselineService = null) =>
        new(
            new FakeCurrentUser(UserId),
            repository,
            new FakeTvShowRepository(tvShowExists ? CreateTvShow() : null),
            baselineService ?? new FakeTvShowFollowBaselineService());

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

    private sealed class ConcurrentWinTvShowFollowRepository : ITvShowFollowRepository
    {
        private TvShowFollow? _follow;

        public Task<TvShowFollow?> GetForUserAndTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_follow);

        public Task<TvShowFollow?> GetForUserAndTvShowForUpdateAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_follow);

        public Task<bool> TryAddAsync(TvShowFollow follow, CancellationToken cancellationToken = default)
        {
            if (_follow is null)
            {
                _follow = TvShowFollow.Create(
                    follow.UserId,
                    follow.TvShowId,
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

        public Task<(IReadOnlyList<TvShowFollow> Follows, int TotalCount)> GetUserFollowsAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<TvShowFollow>, int)>(([], 0));
    }

    private sealed class FakeTvShowFollowBaselineService : ITvShowFollowBaselineService
    {
        public int EstablishCallCount { get; private set; }

        public bool ShouldFail { get; init; }

        public Task EstablishAsync(TvShowFollow follow, CancellationToken cancellationToken = default)
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

    private sealed class FakeTvShowFollowRepository : ITvShowFollowRepository
    {
        private TvShowFollow? _follow;

        public TvShowFollow? SeededFollow => _follow;

        public void Seed(TvShowFollow follow) => _follow = follow;

        public Task<TvShowFollow?> GetForUserAndTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_follow);

        public Task<TvShowFollow?> GetForUserAndTvShowForUpdateAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_follow);

        public Task<bool> TryAddAsync(TvShowFollow follow, CancellationToken cancellationToken = default)
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

        public Task<(IReadOnlyList<TvShowFollow> Follows, int TotalCount)> GetUserFollowsAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<TvShowFollow>, int)>(
                (_follow is null ? [] : new List<TvShowFollow> { _follow }, _follow is null ? 0 : 1));
    }
}

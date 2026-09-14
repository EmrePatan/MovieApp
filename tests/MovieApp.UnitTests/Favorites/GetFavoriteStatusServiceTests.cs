using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Favorites;
using MovieApp.Application.Services.Favorites;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Favorites;

public sealed class GetFavoriteStatusServiceTests
{
    [Fact]
    public async Task GetMovieStatusAsyncReturnsRepositoryResult()
    {
        var userId = Guid.NewGuid();
        var movieId = Guid.NewGuid();
        var repository = new FakeFavoriteRepository { MovieStatus = true };
        var service = new GetFavoriteStatusService(new FakeCurrentUser(userId), repository);

        var result = await service.GetMovieStatusAsync(movieId);

        Assert.True(result);
        Assert.Equal(userId, repository.LastUserId);
        Assert.Equal(movieId, repository.LastMovieId);
    }

    [Fact]
    public async Task GetTvShowStatusAsyncReturnsRepositoryResult()
    {
        var userId = Guid.NewGuid();
        var tvShowId = Guid.NewGuid();
        var repository = new FakeFavoriteRepository { TvShowStatus = false };
        var service = new GetFavoriteStatusService(new FakeCurrentUser(userId), repository);

        var result = await service.GetTvShowStatusAsync(tvShowId);

        Assert.False(result);
        Assert.Equal(userId, repository.LastUserId);
        Assert.Equal(tvShowId, repository.LastTvShowId);
    }

    [Fact]
    public async Task GetBatchStatusAsyncReturnsStatusesForMoviesAndTvShows()
    {
        var userId = Guid.NewGuid();
        var movieId = Guid.NewGuid();
        var tvShowId = Guid.NewGuid();
        var repository = new FakeFavoriteRepository
        {
            FavoritedMovieIds = new HashSet<Guid> { movieId },
            FavoritedTvShowIds = new HashSet<Guid>(),
        };
        var service = new GetFavoriteStatusService(new FakeCurrentUser(userId), repository);

        var results = await service.GetBatchStatusAsync(
        [
            new FavoriteContentReference("movie", movieId),
            new FavoriteContentReference("tv", tvShowId),
        ]);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, result => result.ContentType == "movie" && result.Id == movieId && result.IsFavorited);
        Assert.Contains(results, result => result.ContentType == "tv" && result.Id == tvShowId && !result.IsFavorited);
    }

    [Fact]
    public async Task GetBatchStatusAsyncRejectsTooManyItems()
    {
        var service = new GetFavoriteStatusService(
            new FakeCurrentUser(Guid.NewGuid()),
            new FakeFavoriteRepository());

        var items = Enumerable.Range(0, 21)
            .Select(index => new FavoriteContentReference("movie", Guid.NewGuid()))
            .ToList();

        await Assert.ThrowsAsync<ValidationException>(() => service.GetBatchStatusAsync(items));
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class FakeFavoriteRepository : IFavoriteRepository
    {
        public bool MovieStatus { get; init; }

        public bool TvShowStatus { get; init; }

        public Guid? LastUserId { get; private set; }

        public Guid? LastMovieId { get; private set; }

        public Guid? LastTvShowId { get; private set; }

        public Task<bool> ExistsForMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            LastMovieId = movieId;
            return Task.FromResult(MovieStatus);
        }

        public Task<bool> ExistsForTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            LastTvShowId = tvShowId;
            return Task.FromResult(TvShowStatus);
        }

        public IReadOnlySet<Guid> FavoritedMovieIds { get; init; } = new HashSet<Guid>();

        public IReadOnlySet<Guid> FavoritedTvShowIds { get; init; } = new HashSet<Guid>();

        public Task<IReadOnlySet<Guid>> GetFavoritedMovieIdsAsync(
            Guid userId,
            IReadOnlyCollection<Guid> movieIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(FavoritedMovieIds);

        public Task<IReadOnlySet<Guid>> GetFavoritedTvShowIdsAsync(
            Guid userId,
            IReadOnlyCollection<Guid> tvShowIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(FavoritedTvShowIds);

        public Task<bool> TryAddAsync(Favorite favorite, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> RemoveForMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> RemoveForTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<(IReadOnlyList<Favorite> Favorites, int TotalCount)> GetUserFavoritesAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<Favorite>, int)>(([], 0));
    }
}

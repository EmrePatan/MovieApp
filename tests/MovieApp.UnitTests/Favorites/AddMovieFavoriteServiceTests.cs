using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Favorites;
using MovieApp.Application.Services.Favorites;
using MovieApp.Domain.Entities;

using MovieApp.UnitTests.Caching;

namespace MovieApp.UnitTests.Favorites;

public sealed class AddMovieFavoriteServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid MovieId = Guid.NewGuid();

    [Fact]
    public async Task AddAsyncCreatesFavoriteWhenMovieExists()
    {
        var service = CreateService(
            new FakeCurrentUser(UserId),
            new FakeFavoriteRepository(exists: false, tryAddReturns: true),
            new FakeMovieRepository(movie: CreateMovie()));

        var result = await service.AddAsync(MovieId);

        Assert.Equal(FavoriteMutationResult.Created, result);
    }

    [Fact]
    public async Task AddAsyncReturnsAlreadyExistsWhenFavoriteExists()
    {
        var service = CreateService(
            new FakeCurrentUser(UserId),
            new FakeFavoriteRepository(exists: true, tryAddReturns: false),
            new FakeMovieRepository(movie: CreateMovie()));

        var result = await service.AddAsync(MovieId);

        Assert.Equal(FavoriteMutationResult.AlreadyExists, result);
    }

    [Fact]
    public async Task AddAsyncThrowsNotFoundWhenMovieMissing()
    {
        var service = CreateService(
            new FakeCurrentUser(UserId),
            new FakeFavoriteRepository(exists: false, tryAddReturns: true),
            new FakeMovieRepository(movie: null));

        await Assert.ThrowsAsync<NotFoundException>(() => service.AddAsync(MovieId));
    }

    [Fact]
    public async Task AddAsyncThrowsAuthenticationExceptionWhenUserNotAuthenticated()
    {
        var service = CreateService(
            new FakeCurrentUser(null),
            new FakeFavoriteRepository(exists: false, tryAddReturns: true),
            new FakeMovieRepository(movie: CreateMovie()));

        await Assert.ThrowsAsync<AuthenticationException>(() => service.AddAsync(MovieId));
    }

    private static AddMovieFavoriteService CreateService(
        ICurrentUser currentUser,
        IFavoriteRepository favoriteRepository,
        IMovieRepository movieRepository) =>
        new(currentUser, favoriteRepository, movieRepository, new FakeUserAnalyticsCacheInvalidator());

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

    private sealed class FakeFavoriteRepository(bool exists, bool tryAddReturns) : IFavoriteRepository
    {
        public Task<bool> ExistsForMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default) =>
            Task.FromResult(exists);

        public Task<bool> ExistsForTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlySet<Guid>> GetFavoritedMovieIdsAsync(
            Guid userId,
            IReadOnlyCollection<Guid> movieIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

        public Task<IReadOnlySet<Guid>> GetFavoritedTvShowIdsAsync(
            Guid userId,
            IReadOnlyCollection<Guid> tvShowIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

        public Task<bool> TryAddAsync(Favorite favorite, CancellationToken cancellationToken = default) =>
            Task.FromResult(tryAddReturns);

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

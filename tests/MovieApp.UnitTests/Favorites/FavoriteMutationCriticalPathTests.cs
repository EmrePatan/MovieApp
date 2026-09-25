using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Favorites;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Favorites;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Favorites;

public sealed class FavoriteMutationCriticalPathTests
{
    [Fact]
    public async Task AddAsyncChecksExistenceInsteadOfLoadingTheMovieGraph()
    {
        var movieId = Guid.NewGuid();
        var repository = new ExistsOnlyMovieRepository();
        var service = new AddMovieFavoriteService(
            new StubCurrentUser(Guid.NewGuid()),
            new StubFavoriteRepository(),
            repository,
            new CompletingInvalidator());

        var result = await service.AddAsync(movieId);

        Assert.Equal(FavoriteMutationResult.Created, result);
        Assert.Equal(1, repository.ExistsCalls);
    }

    [Fact]
    public async Task AddAsyncDoesNotWaitForAnalyticsInvalidationWhenAScopeFactoryIsAvailable()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();
        services.AddSingleton<IUserAnalyticsCacheInvalidator>(new GatedInvalidator(gate.Task));
        await using var provider = services.BuildServiceProvider();
        var service = new AddMovieFavoriteService(
            new StubCurrentUser(Guid.NewGuid()),
            new StubFavoriteRepository(),
            new ExistsOnlyMovieRepository(),
            new CompletingInvalidator(),
            provider.GetRequiredService<IServiceScopeFactory>());

        var add = service.AddAsync(Guid.NewGuid());
        var completed = await Task.WhenAny(add, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(add, completed);
        Assert.Equal(FavoriteMutationResult.Created, await add);
        gate.TrySetResult();
    }

    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class StubFavoriteRepository : IFavoriteRepository
    {
        public Task<bool> ExistsForMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> ExistsForTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlySet<Guid>> GetFavoritedMovieIdsAsync(
            Guid userId,
            IReadOnlyCollection<Guid> movieIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlySet<Guid>> GetFavoritedTvShowIdsAsync(
            Guid userId,
            IReadOnlyCollection<Guid> tvShowIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> TryAddAsync(Favorite favorite, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> RemoveForMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RemoveForTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<Favorite> Favorites, int TotalCount)> GetUserFavoritesAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ExistsOnlyMovieRepository : IMovieRepository
    {
        public int ExistsCalls { get; private set; }

        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            ExistsCalls++;
            return Task.FromResult(true);
        }

        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Movie favorite toggles must not load the movie graph.");

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CompletingInvalidator : IUserAnalyticsCacheInvalidator
    {
        public Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class GatedInvalidator(Task gate) : IUserAnalyticsCacheInvalidator
    {
        public async Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            await gate;
    }
}

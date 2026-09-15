using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.MovieFollows;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.MovieFollows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.MovieFollows;

public sealed class UpsertMovieFollowServiceTests
{
    private static readonly Guid MovieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task UpsertAsync_GlobalFutureWithoutRegionalRow_AllowsFollow()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today.AddDays(30));
        var service = CreateService(movie, regionalRelease: null);

        var (mutation, status) = await service.UpsertAsync(MovieId);

        Assert.Equal(MovieFollowMutationResult.Created, mutation);
        Assert.True(status.IsFollowing);
    }

    [Fact]
    public async Task UpsertAsync_GlobalPastRegionalFuture_AllowsFollow()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today.AddDays(-10));
        var regional = CreateRegionalRelease(today.AddDays(30));
        var service = CreateService(movie, regional);

        var (_, status) = await service.UpsertAsync(MovieId);

        Assert.True(status.IsFollowing);
    }

    [Fact]
    public async Task UpsertAsync_GlobalFutureRegionalPast_RejectsFollow()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today.AddDays(30));
        var regional = CreateRegionalRelease(today.AddDays(-1));
        var service = CreateService(movie, regional);

        await Assert.ThrowsAsync<ValidationException>(() => service.UpsertAsync(MovieId));
    }

    [Fact]
    public async Task UpsertAsync_RegionalFuture_AllowsFollow()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today.AddDays(-10));
        var regional = CreateRegionalRelease(today.AddDays(30));
        var service = CreateService(movie, regional);

        var (_, status) = await service.UpsertAsync(MovieId);

        Assert.True(status.IsFollowing);
    }

    [Fact]
    public async Task UpsertAsync_SyncedNullEffective_RejectsFollow()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today.AddDays(30));
        var regional = CreateRegionalRelease(null);
        var service = CreateService(movie, regional);

        await Assert.ThrowsAsync<ValidationException>(() => service.UpsertAsync(MovieId));
    }

    private static UpsertMovieFollowService CreateService(
        Movie movie,
        MovieRegionalRelease? regionalRelease) =>
        new(
            new FakeCurrentUser(UserId),
            new FakeCatalogFollowRepository(),
            new FakeMovieRepository(movie),
            new FakeMovieRegionalReleaseRepository(regionalRelease),
            Options.Create(new ReleaseRegionOptions { DefaultRegion = "TR" }));

    private static Movie CreateMovie(DateOnly? releaseDate) =>
        new()
        {
            Id = MovieId,
            Title = "Movie",
            ReleaseDate = releaseDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static MovieRegionalRelease CreateRegionalRelease(DateOnly? effectiveReleaseDate) =>
        new()
        {
            MovieId = MovieId,
            Region = "TR",
            EffectiveReleaseDate = effectiveReleaseDate,
            IsFallbackGlobal = effectiveReleaseDate is null,
            SyncedAtUtc = DateTime.UtcNow
        };

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class FakeMovieRepository(Movie movie) : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(movie);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeMovieRegionalReleaseRepository(MovieRegionalRelease? regionalRelease)
        : IMovieRegionalReleaseRepository
    {
        public Task<MovieRegionalRelease?> GetByMovieIdAndRegionAsync(
            Guid movieId,
            string region,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(regionalRelease);

        public Task<MovieRegionalRelease> UpsertAsync(
            MovieRegionalRelease regionalRelease,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeCatalogFollowRepository : ICatalogFollowRepository
    {
        public Task<CatalogFollow?> GetForUserAndContentForUpdateAsync(
            Guid userId,
            CatalogContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CatalogFollow?>(null);

        public Task<bool> TryAddAsync(CatalogFollow follow, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<Guid>> GetFollowedMovieIdsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CatalogFollow?> GetForUserAndContentAsync(
            Guid userId,
            CatalogContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> RemoveAsync(
            Guid userId,
            CatalogContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<CatalogFollow> Follows, int TotalCount)> GetUserFollowsAsync(
            Guid userId,
            int page,
            int pageSize,
            CatalogContentType? contentType = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CatalogFollow>> GetEstablishedTvFollowsByTvShowIdsAsync(
            IReadOnlyCollection<Guid> tvShowIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CatalogFollow>> GetMovieFollowsForReleaseCheckAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveMovieFollowsByMovieIdAsync(Guid movieId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

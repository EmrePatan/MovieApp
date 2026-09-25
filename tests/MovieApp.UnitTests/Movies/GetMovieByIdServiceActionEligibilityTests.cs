using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Movies;
using MovieApp.Domain.Entities;
using MovieApp.UnitTests.Keywords;

namespace MovieApp.UnitTests.Movies;

public sealed class GetMovieByIdServiceActionEligibilityTests
{
    private static readonly Guid MovieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task GetByIdAsync_FutureEffectiveDate_AllowsFollowActions()
    {
        var service = CreateService(CreateMovie(Today.AddDays(30)), regionalRelease: null);

        var result = await service.GetByIdAsync(MovieId);

        Assert.True(result.CanFollowForRelease);
        Assert.True(result.CanSetReleaseAlert);
    }

    [Fact]
    public async Task GetByIdAsync_RegionalFutureOverridesGlobalPast_AllowsFollowActions()
    {
        var service = CreateService(
            CreateMovie(Today.AddDays(-10)),
            CreateRegionalRelease(Today.AddDays(30)));

        var result = await service.GetByIdAsync(MovieId);

        Assert.True(result.CanFollowForRelease);
        Assert.True(result.CanSetReleaseAlert);
    }

    [Fact]
    public async Task GetByIdAsync_RegionalPastOverridesGlobalFuture_DisallowsFollowActions()
    {
        var service = CreateService(
            CreateMovie(Today.AddDays(30)),
            CreateRegionalRelease(Today.AddDays(-1)));

        var result = await service.GetByIdAsync(MovieId);

        Assert.False(result.CanFollowForRelease);
        Assert.False(result.CanSetReleaseAlert);
    }

    [Fact]
    public async Task GetByIdAsync_RegionalEffectiveToday_DisallowsFollowActions()
    {
        var service = CreateService(
            CreateMovie(Today.AddDays(30)),
            CreateRegionalRelease(Today));

        var result = await service.GetByIdAsync(MovieId);

        Assert.False(result.CanFollowForRelease);
        Assert.False(result.CanSetReleaseAlert);
    }

    [Fact]
    public async Task GetByIdAsync_NullEffectiveDate_AllowsFollowActions()
    {
        var service = CreateService(CreateMovie(null), regionalRelease: null);

        var result = await service.GetByIdAsync(MovieId);

        Assert.True(result.CanFollowForRelease);
        Assert.True(result.CanSetReleaseAlert);
    }

    private static GetMovieByIdService CreateService(
        Movie movie,
        MovieRegionalRelease? regionalRelease) =>
        new(
            new FakeMovieRepository(movie),
            new FakeMovieRegionalReleaseRepository(regionalRelease),
            Options.Create(new ReleaseRegionOptions { DefaultRegion = "TR" }),
            new NoOpCatalogKeywordReadPathScheduler(),
            new NullMovieDataProvider(),
            new NoOpCatalogProviderUpsertService(),
            new NoOpCacheService());

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
            Task.FromResult(regionalRelease);
    }

    private sealed class NoOpCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult<T?>(null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

}

using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Movies;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Movies;

public sealed class GetMovieByIdServiceReleaseGuardrailTests
{
    private static readonly Guid MovieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task GetByIdAsync_GlobalFutureWithoutRegionalRow_IsNotReleased()
    {
        var service = CreateService(CreateMovie(Today.AddDays(30)), regionalRelease: null);

        var result = await service.GetByIdAsync(MovieId);

        Assert.False(result.IsReleased);
    }

    [Fact]
    public async Task GetByIdAsync_GlobalPastWithoutRegionalRow_IsReleased()
    {
        var service = CreateService(CreateMovie(Today.AddDays(-10)), regionalRelease: null);

        var result = await service.GetByIdAsync(MovieId);

        Assert.True(result.IsReleased);
    }

    [Fact]
    public async Task GetByIdAsync_GlobalFutureRegionalFuture_IsNotReleased()
    {
        var service = CreateService(
            CreateMovie(Today.AddDays(-10)),
            CreateRegionalRelease(Today.AddDays(30)));

        var result = await service.GetByIdAsync(MovieId);

        Assert.False(result.IsReleased);
    }

    [Fact]
    public async Task GetByIdAsync_GlobalFutureRegionalPast_IsReleased()
    {
        var service = CreateService(
            CreateMovie(Today.AddDays(30)),
            CreateRegionalRelease(Today.AddDays(-1)));

        var result = await service.GetByIdAsync(MovieId);

        Assert.True(result.IsReleased);
    }

    [Fact]
    public async Task GetByIdAsync_RegionalEffectiveToday_IsReleased()
    {
        var service = CreateService(
            CreateMovie(Today.AddDays(30)),
            CreateRegionalRelease(Today));

        var result = await service.GetByIdAsync(MovieId);

        Assert.True(result.IsReleased);
    }

    [Fact]
    public async Task GetByIdAsync_SyncedNullEffective_IsReleased()
    {
        var service = CreateService(
            CreateMovie(Today.AddDays(30)),
            CreateRegionalRelease(null));

        var result = await service.GetByIdAsync(MovieId);

        Assert.True(result.IsReleased);
    }

    [Fact]
    public async Task GetByIdAsync_NullGlobalWithoutRegionalRow_IsReleased()
    {
        var service = CreateService(CreateMovie(null), regionalRelease: null);

        var result = await service.GetByIdAsync(MovieId);

        Assert.True(result.IsReleased);
    }

    private static GetMovieByIdService CreateService(
        Movie movie,
        MovieRegionalRelease? regionalRelease) =>
        new(
            new FakeMovieRepository(movie),
            new FakeMovieRegionalReleaseRepository(regionalRelease),
            Options.Create(new ReleaseRegionOptions { DefaultRegion = "TR" }),
            new NoOpCatalogKeywordIngestionService(),
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

    private sealed class NoOpCatalogKeywordIngestionService : ICatalogKeywordIngestionService
    {
        public Task TryEnrichMovieKeywordsAsync(Guid movieId, bool refreshKeywords, IReadOnlyList<ProviderKeywordSummary>? prefetchedKeywords = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task TryEnrichTvShowKeywordsAsync(Guid tvShowId, bool refreshKeywords, IReadOnlyList<ProviderKeywordSummary>? prefetchedKeywords = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

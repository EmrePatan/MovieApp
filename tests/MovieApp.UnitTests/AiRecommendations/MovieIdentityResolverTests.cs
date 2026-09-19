using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.AiRecommendations;
using MovieApp.Application.Services.Movies;
using MovieApp.Application.Services.TvShows;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class MovieIdentityResolverTests
{
    [Fact]
    public async Task ResolveAsyncUsesValidTmdbHint()
    {
        var movieId = Guid.NewGuid();
        var resolver = CreateResolver(
            new FakeGetMovieByTmdbIdService(_ => new MovieDetailsResult(
                movieId,
                329996,
                null,
                null,
                "Arrival",
                "Arrival",
                "Overview",
                new DateOnly(2016, 11, 11),
                116,
                "/poster.jpg",
                "/backdrop.jpg",
                "en",
                7.8m,
                1000,
                ["Science Fiction", "Drama"],
                null,
                true,
                false,
                false)),
            new FakeSearchMoviesService());

        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", 329996, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("movie", result!.MediaType);
        Assert.Equal(movieId, result.MovieId);
    }

    [Fact]
    public async Task ResolveAsyncResolvesTvSuggestion()
    {
        var tvShowId = Guid.NewGuid();
        var resolver = CreateResolver(
            new FakeGetMovieByTmdbIdService(_ => throw new InvalidOperationException()),
            new FakeSearchMoviesService(),
            new FakeGetTvShowByTmdbIdService(_ => new TvShowDetailsResult(
                tvShowId,
                1396,
                null,
                null,
                "Breaking Bad",
                "Breaking Bad",
                "Overview",
                new DateOnly(2008, 1, 20),
                null,
                "/poster.jpg",
                null,
                "en",
                9m,
                1000,
                "Ended",
                ["Drama", "Crime"],
                [],
                true)),
            new FakeSearchTvShowsService());

        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Breaking Bad", 2008, "tv", 1396, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("tv", result!.MediaType);
        Assert.Equal(tvShowId, result.MovieId);
    }

    [Fact]
    public async Task ResolveAsyncIgnoresMismatchedTmdbHintAndFallsBackToSearch()
    {
        var movieId = Guid.NewGuid();
        var resolver = CreateResolver(
            new FakeGetMovieByTmdbIdService(tmdbId => tmdbId == 42
                ? new MovieDetailsResult(
                    movieId,
                    42,
                    null,
                    null,
                    "Arrival",
                    null,
                    null,
                    new DateOnly(2016, 1, 1),
                    116,
                    null,
                    null,
                    "en",
                    7m,
                    100,
                    ["Science Fiction"],
                    null,
                    true,
                    false,
                    false)
                : new MovieDetailsResult(
                    movieId,
                    1,
                    null,
                    null,
                    "Different Title",
                    null,
                    null,
                    new DateOnly(2000, 1, 1),
                    90,
                    null,
                    null,
                    "en",
                    5m,
                    10,
                    ["Drama"],
                    null,
                    true,
                    false,
                    false)),
            new FakeSearchMoviesService(query => new PaginatedResult<MovieSearchResult>(
                [
                    new MovieSearchResult(
                        movieId,
                        42,
                        null,
                        null,
                        "Arrival",
                        null,
                        new DateOnly(2016, 1, 1),
                        null,
                        7m,
                        100)
                ],
                1,
                10,
                1,
                1)));

        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", 1, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("movie", result!.MediaType);
        Assert.Equal("Arrival", result.Title);
    }

    [Fact]
    public async Task ResolveAsyncReturnsNullForAmbiguousSearch()
    {
        var resolver = CreateResolver(
            new FakeGetMovieByTmdbIdService(_ => throw new InvalidOperationException()),
            new FakeSearchMoviesService(_ => new PaginatedResult<MovieSearchResult>(
                [
                    new MovieSearchResult(Guid.NewGuid(), 1, null, null, "Arrival", null, new DateOnly(2016, 1, 1), null, 7m, 1),
                    new MovieSearchResult(Guid.NewGuid(), 2, null, null, "Arrival", null, new DateOnly(2016, 6, 1), null, 6m, 1)
                ],
                1,
                10,
                2,
                1)));

        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", null, "Reason"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsyncReturnsNullForUnsupportedMediaType()
    {
        var resolver = CreateResolver(
            new FakeGetMovieByTmdbIdService(_ => throw new InvalidOperationException()),
            new FakeSearchMoviesService());

        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Podcast", 2020, "podcast", null, "Reason"));

        Assert.Null(result);
    }

    private static MovieIdentityResolver CreateResolver(
        FakeGetMovieByTmdbIdService getMovieByTmdbIdService,
        FakeSearchMoviesService searchMoviesService,
        FakeGetTvShowByTmdbIdService? getTvShowByTmdbIdService = null,
        FakeSearchTvShowsService? searchTvShowsService = null) =>
        new(
            getMovieByTmdbIdService,
            searchMoviesService,
            getTvShowByTmdbIdService ?? new FakeGetTvShowByTmdbIdService(),
            searchTvShowsService ?? new FakeSearchTvShowsService(),
            NullAiRecommendationPerfContext.Instance);

    private sealed class FakeGetMovieByTmdbIdService(Func<int, MovieDetailsResult> factory)
        : IGetMovieByTmdbIdService
    {
        public Task<MovieDetailsResult> GetAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult(factory(tmdbId));
    }

    private sealed class FakeSearchMoviesService(Func<string, PaginatedResult<MovieSearchResult>>? factory = null)
        : ISearchMoviesService
    {
        public Task<PaginatedResult<MovieSearchResult>> SearchAsync(
            MovieSearchRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(factory?.Invoke(request.Query) ?? new PaginatedResult<MovieSearchResult>([], 1, 10, 0, 0));
    }

    private sealed class FakeGetTvShowByTmdbIdService(Func<int, TvShowDetailsResult>? factory = null)
        : IGetTvShowByTmdbIdService
    {
        public Task<TvShowDetailsResult> GetAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            factory is null
                ? throw new InvalidOperationException()
                : Task.FromResult(factory(tmdbId));
    }

    private sealed class FakeSearchTvShowsService(Func<string, PaginatedResult<TvShowSearchResult>>? factory = null)
        : ISearchTvShowsService
    {
        public Task<PaginatedResult<TvShowSearchResult>> SearchAsync(
            TvShowSearchRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(factory?.Invoke(request.Query) ?? new PaginatedResult<TvShowSearchResult>([], 1, 10, 0, 0));
    }
}

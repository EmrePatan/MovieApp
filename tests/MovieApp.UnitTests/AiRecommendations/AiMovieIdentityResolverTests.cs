using System.Globalization;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.AiRecommendations;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class AiMovieIdentityResolverTests
{
    [Fact]
    public async Task ResolveAsyncUsesCatalogMovieByTmdbIdWithoutProviderOrDetailServices()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository
        {
            MovieByTmdbId = CreateMovie(movieId, 329996, "Arrival", 2016)
        };
        var movieProvider = new TrackingMovieDataProvider();
        var catalogUpsert = new TrackingCatalogProviderUpsertService();

        var resolver = CreateResolver(movieRepository, movieProvider, catalogUpsert: catalogUpsert);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", 329996, "Reason"));

        Assert.NotNull(result);
        Assert.Equal(movieId, result!.MovieId);
        Assert.Equal(1, movieRepository.GetByTmdbIdCallCount);
        Assert.Equal(0, movieProvider.GetMovieCallCount);
        Assert.Equal(0, movieProvider.SearchCallCount);
        Assert.Equal(0, catalogUpsert.MovieUpsertCount);
        Assert.Equal(1, catalogUpsert.PerfContext.Metrics.ValidationCatalogHits);
    }

    [Fact]
    public async Task ResolveAsyncUsesProviderFallbackWhenCatalogMisses()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository();
        var movieProvider = new TrackingMovieDataProvider
        {
            MovieDetails = CreateMovieProviderDetails(329996, "Arrival", 2016)
        };
        var catalogUpsert = new TrackingCatalogProviderUpsertService
        {
            UpsertedMovie = CreateMovie(movieId, 329996, "Arrival", 2016)
        };

        var resolver = CreateResolver(movieRepository, movieProvider, catalogUpsert: catalogUpsert);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", 329996, "Reason"));

        Assert.NotNull(result);
        Assert.Equal(1, movieRepository.GetByTmdbIdCallCount);
        Assert.Equal(1, movieProvider.GetMovieCallCount);
        Assert.Equal(0, movieProvider.SearchCallCount);
        Assert.Equal(1, catalogUpsert.MovieUpsertCount);
        Assert.False(catalogUpsert.LastMovieEnrichKeywords);
        Assert.Equal(1, catalogUpsert.PerfContext.Metrics.ValidationProviderFallbacks);
    }

    [Fact]
    public async Task ResolveAsyncResolvesTvSuggestionFromCatalog()
    {
        var tvShowId = Guid.NewGuid();
        var tvRepository = new TrackingTvShowRepository
        {
            TvShowByTmdbId = CreateTvShow(tvShowId, 1396, "Breaking Bad", 2008)
        };

        var resolver = CreateResolver(
            tvShowRepository: tvRepository,
            tvShowProvider: new TrackingTvShowDataProvider());
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Breaking Bad", 2008, "tv", 1396, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("tv", result!.MediaType);
        Assert.Equal(tvShowId, result.MovieId);
        Assert.Equal(1, tvRepository.GetByTmdbIdCallCount);
        Assert.Equal(1, tvRepository.PerfContext.Metrics.ValidationCatalogHits);
    }

    [Fact]
    public async Task ResolveAsyncUsesLightweightProviderSearchFallback()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository();
        movieRepository.MoviesByTmdbId[1] = CreateMovie(Guid.NewGuid(), 1, "Different Title", 2000);
        movieRepository.MoviesByTmdbId[42] = CreateMovie(movieId, 42, "Arrival", 2016);
        var movieProvider = new TrackingMovieDataProvider
        {
            SearchResults =
            [
                new MovieProviderSummary("42", 42, null, null, "Arrival", null, new DateOnly(2016, 1, 1), null, 7m, 100)
            ]
        };

        var resolver = CreateResolver(movieRepository, movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", 1, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("Arrival", result!.Title);
        Assert.Equal(1, movieProvider.SearchCallCount);
        Assert.Equal(0, movieProvider.GetMovieCallCount);
        Assert.Equal(2, movieRepository.GetByTmdbIdCallCount);
        Assert.Equal(1, movieProvider.PerfContext.Metrics.ValidationSearchFallbacks);
        Assert.Equal(1, movieRepository.PerfContext.Metrics.ValidationCatalogHits);
    }

    [Fact]
    public async Task ResolveAsyncDedupesRepeatedTmdbSuggestionsInSameRequest()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository
        {
            MovieByTmdbId = CreateMovie(movieId, 329996, "Arrival", 2016)
        };

        var resolver = CreateResolver(movieRepository, new TrackingMovieDataProvider());
        var suggestion = new AiProviderSuggestion("Arrival", 2016, "movie", 329996, "Reason");

        var first = await resolver.ResolveAsync(suggestion);
        var second = await resolver.ResolveAsync(suggestion);

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Equal(1, movieRepository.GetByTmdbIdCallCount);
        Assert.Equal(1, movieRepository.PerfContext.Metrics.ValidationCatalogHits);
        Assert.Equal(1, movieRepository.PerfContext.Metrics.ValidationDedupHits);
    }

    [Fact]
    public async Task ResolveAsyncFallsBackToSearchWhenHintedProviderLookupReturnsNull()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository();
        movieRepository.MoviesByTmdbId[42] = CreateMovie(movieId, 42, "Arrival", 2016);
        var movieProvider = new TrackingMovieDataProvider
        {
            GetMovieFactory = _ => null,
            SearchResults =
            [
                new MovieProviderSummary("42", 42, null, null, "Arrival", null, new DateOnly(2016, 1, 1), null, 7m, 100)
            ]
        };

        var resolver = CreateResolver(movieRepository, movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", 9866, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("Arrival", result!.Title);
        Assert.Equal(1, movieProvider.GetMovieCallCount);
        Assert.Equal(1, movieProvider.SearchCallCount);
        Assert.Equal(2, movieRepository.GetByTmdbIdCallCount);
        Assert.Equal(1, movieProvider.PerfContext.Metrics.ValidationProviderFallbacks);
        Assert.Equal(1, movieProvider.PerfContext.Metrics.ValidationSearchFallbacks);
        Assert.Equal(1, movieProvider.PerfContext.Metrics.ValidationCatalogHits);
    }

    [Fact]
    public async Task ResolveAsyncFallsBackToSearchWhenHintedProviderTitleYearMismatch()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository();
        movieRepository.MoviesByTmdbId[42] = CreateMovie(movieId, 42, "Arrival", 2016);
        var movieProvider = new TrackingMovieDataProvider
        {
            MovieDetails = CreateMovieProviderDetails(545, "Different Title", 2000),
            SearchResults =
            [
                new MovieProviderSummary("42", 42, null, null, "Arrival", null, new DateOnly(2016, 1, 1), null, 7m, 100)
            ]
        };

        var resolver = CreateResolver(movieRepository, movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", 545, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("Arrival", result!.Title);
        Assert.Equal(1, movieProvider.GetMovieCallCount);
        Assert.Equal(1, movieProvider.SearchCallCount);
        Assert.Equal(1, movieProvider.PerfContext.Metrics.ValidationProviderFallbacks);
        Assert.Equal(1, movieProvider.PerfContext.Metrics.ValidationSearchFallbacks);
    }

    [Fact]
    public async Task ResolveAsyncReturnsNullWhenHintedProviderAndSearchBothFail()
    {
        var movieProvider = new TrackingMovieDataProvider
        {
            GetMovieFactory = _ => null,
            SearchResults =
            [
                new MovieProviderSummary("1", 1, null, null, "Arrival", null, new DateOnly(2016, 1, 1), null, 7m, 1),
                new MovieProviderSummary("2", 2, null, null, "Arrival", null, new DateOnly(2016, 6, 1), null, 6m, 1)
            ]
        };

        var resolver = CreateResolver(new TrackingMovieRepository(), movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", 9866, "Reason"));

        Assert.Null(result);
        Assert.Equal(1, movieProvider.GetMovieCallCount);
        Assert.Equal(1, movieProvider.SearchCallCount);
        Assert.Equal(1, movieProvider.PerfContext.Metrics.ValidationProviderFallbacks);
        Assert.Equal(1, movieProvider.PerfContext.Metrics.ValidationSearchFallbacks);
    }

    [Fact]
    public async Task ResolveAsyncReturnsNullForAmbiguousSearch()
    {
        var movieProvider = new TrackingMovieDataProvider
        {
            SearchResults =
            [
                new MovieProviderSummary("1", 1, null, null, "Arrival", null, new DateOnly(2016, 1, 1), null, 7m, 1),
                new MovieProviderSummary("2", 2, null, null, "Arrival", null, new DateOnly(2016, 6, 1), null, 6m, 1)
            ]
        };

        var resolver = CreateResolver(new TrackingMovieRepository(), movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", null, "Reason"));

        Assert.Null(result);
        Assert.Equal(1, movieProvider.SearchCallCount);
        Assert.Equal(1, movieProvider.PerfContext.Metrics.ValidationSearchFallbacks);
    }

    [Fact]
    public async Task ResolveAsyncReturnsNullForUnsupportedMediaType()
    {
        var resolver = CreateResolver(new TrackingMovieRepository(), new TrackingMovieDataProvider());
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Podcast", 2020, "podcast", null, "Reason"));

        Assert.Null(result);
    }

    private static AiMovieIdentityResolver CreateResolver(
        TrackingMovieRepository? movieRepository = null,
        TrackingMovieDataProvider? movieProvider = null,
        TrackingTvShowRepository? tvShowRepository = null,
        TrackingTvShowDataProvider? tvShowProvider = null,
        TrackingCatalogProviderUpsertService? catalogUpsert = null)
    {
        movieRepository ??= new TrackingMovieRepository();
        movieProvider ??= new TrackingMovieDataProvider();
        tvShowRepository ??= new TrackingTvShowRepository();
        tvShowProvider ??= new TrackingTvShowDataProvider();
        catalogUpsert ??= new TrackingCatalogProviderUpsertService();

        movieRepository.PerfContext = catalogUpsert.PerfContext;
        tvShowRepository.PerfContext = catalogUpsert.PerfContext;
        movieProvider.PerfContext = catalogUpsert.PerfContext;
        tvShowProvider.PerfContext = catalogUpsert.PerfContext;

        return new AiMovieIdentityResolver(
            movieRepository,
            tvShowRepository,
            movieProvider,
            tvShowProvider,
            catalogUpsert,
            catalogUpsert.PerfContext);
    }

    private static Movie CreateMovie(Guid id, int tmdbId, string title, int year)
    {
        var genre = new Genre { Id = Guid.NewGuid(), Name = "Science Fiction" };
        return new Movie
        {
            Id = id,
            TmdbId = tmdbId,
            Title = title,
            ReleaseDate = new DateOnly(year, 1, 1),
            RuntimeMinutes = 116,
            VoteAverage = 7.8m,
            VoteCount = 1000,
            MovieGenres =
            [
                new MovieGenre
                {
                    Genre = genre
                }
            ]
        };
    }

    private static TvShow CreateTvShow(Guid id, int tmdbId, string title, int year)
    {
        var genre = new Genre { Id = Guid.NewGuid(), Name = "Drama" };
        return new TvShow
        {
            Id = id,
            TmdbId = tmdbId,
            Title = title,
            FirstAirDate = new DateOnly(year, 1, 20),
            VoteAverage = 9m,
            VoteCount = 1000,
            TvShowGenres =
            [
                new TvShowGenre
                {
                    Genre = genre
                }
            ]
        };
    }

    private static MovieProviderDetails CreateMovieProviderDetails(int tmdbId, string title, int year) =>
        new(
            tmdbId.ToString(CultureInfo.InvariantCulture),
            tmdbId,
            null,
            null,
            title,
            title,
            "Overview",
            new DateOnly(year, 1, 1),
            116,
            "/poster.jpg",
            "/backdrop.jpg",
            "en",
            7.8m,
            1000,
            ["Science Fiction"]);

    private sealed class TrackingMovieRepository : IMovieRepository
    {
        public Movie? MovieByTmdbId { get; set; }

        public Dictionary<int, Movie?> MoviesByTmdbId { get; } = new();

        public int GetByTmdbIdCallCount { get; private set; }

        public AiRecommendationPerfContext PerfContext { get; set; } = new();

        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default)
        {
            GetByTmdbIdCallCount++;
            if (MoviesByTmdbId.TryGetValue(tmdbId, out var mapped))
            {
                return Task.FromResult(mapped);
            }

            return Task.FromResult(MovieByTmdbId);
        }

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TrackingTvShowRepository : ITvShowRepository
    {
        public TvShow? TvShowByTmdbId { get; set; }

        public int GetByTmdbIdCallCount { get; private set; }

        public AiRecommendationPerfContext PerfContext { get; set; } = new();

        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default)
        {
            GetByTmdbIdCallCount++;
            return Task.FromResult(TvShowByTmdbId);
        }

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TrackingMovieDataProvider : IMovieDataProvider
    {
        public MovieProviderDetails? MovieDetails { get; set; }

        public Func<string, MovieProviderDetails?>? GetMovieFactory { get; set; }

        public IReadOnlyList<MovieProviderSummary> SearchResults { get; set; } = [];

        public int GetMovieCallCount { get; private set; }

        public int SearchCallCount { get; private set; }

        public AiRecommendationPerfContext PerfContext { get; set; } = new();

        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            SearchCallCount++;
            return Task.FromResult(new MovieProviderSearchResult(SearchResults, page, pageSize, SearchResults.Count, 1));
        }

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(
            string externalId,
            CancellationToken cancellationToken = default)
        {
            GetMovieCallCount++;
            if (GetMovieFactory is not null)
            {
                return Task.FromResult(GetMovieFactory(externalId));
            }

            return Task.FromResult(MovieDetails);
        }
    }

    private sealed class TrackingTvShowDataProvider : ITvShowDataProvider
    {
        public AiRecommendationPerfContext PerfContext { get; set; } = new();

        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TvShowProviderSearchResult([], page, pageSize, 0, 0));

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderDetails?> GetTvShowAsync(
            string externalId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShowProviderDetails?>(null);

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalTvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SeasonProviderDetails?>(null);

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalTvShowId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EpisodeProviderDetails?>(null);
    }

    private sealed class TrackingCatalogProviderUpsertService : ICatalogProviderUpsertService
    {
        public Movie? UpsertedMovie { get; set; }

        public int MovieUpsertCount { get; private set; }

        public bool LastMovieEnrichKeywords { get; private set; }

        public AiRecommendationPerfContext PerfContext { get; } = new();

        public Task<Movie> UpsertMovieFromProviderAsync(
            MovieProviderDetails details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default)
        {
            MovieUpsertCount++;
            LastMovieEnrichKeywords = enrichKeywords;
            return Task.FromResult(UpsertedMovie ?? throw new InvalidOperationException("Upserted movie not configured."));
        }

        public Task<IReadOnlyList<Movie>> UpsertMoviesFromProviderBatchAsync(
            IReadOnlyList<MovieProviderDetails> details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow> UpsertTvShowFromProviderAsync(
            TvShowProviderDetails details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TvShow>> UpsertTvShowsFromProviderBatchAsync(
            IReadOnlyList<TvShowProviderDetails> details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

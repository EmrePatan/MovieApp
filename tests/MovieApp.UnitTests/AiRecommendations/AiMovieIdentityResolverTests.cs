using System.Globalization;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.AiRecommendations;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class AiMovieIdentityResolverTests
{
    [Fact]
    public async Task ResolveAsyncUsesTmdbDetailThenCatalogWhenCatalogEntityExists()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository
        {
            MovieByTmdbId = CreateMovie(movieId, 329996, "Arrival", 2016)
        };
        var movieProvider = new TrackingMovieDataProvider();
        movieProvider.MovieDetailsByTmdbId[329996] = CreateMovieProviderDetails(329996, "Arrival", 2016);
        var catalogUpsert = new TrackingCatalogProviderUpsertService();

        var resolver = CreateResolver(movieRepository, movieProvider, catalogUpsert: catalogUpsert);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", 329996, "Reason"));

        Assert.NotNull(result);
        Assert.Equal(movieId, result!.MovieId);
        Assert.Equal(1, movieRepository.GetByTmdbIdCallCount);
        Assert.Equal(1, movieProvider.GetMovieCallCount);
        Assert.Equal(0, movieProvider.SearchCallCount);
        Assert.Equal(0, catalogUpsert.MovieUpsertCount);
        Assert.Equal(1, catalogUpsert.PerfContext.Metrics.ValidationCatalogHits);
        Assert.Equal(0, catalogUpsert.PerfContext.Metrics.ValidationProviderFallbacks);
    }

    [Fact]
    public async Task ResolveAsyncUsesProviderFallbackWhenCatalogMisses()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository();
        var movieProvider = new TrackingMovieDataProvider();
        movieProvider.MovieDetailsByTmdbId[329996] = CreateMovieProviderDetails(329996, "Arrival", 2016);
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

        var tvProvider = new TrackingTvShowDataProvider();
        tvProvider.TvShowDetailsByTmdbId[1396] = CreateTvShowProviderDetails(1396, "Breaking Bad", 2008);

        var resolver = CreateResolver(
            tvShowRepository: tvRepository,
            tvShowProvider: tvProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Breaking Bad", 2008, "tv", 1396, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("tv", result!.MediaType);
        Assert.Equal(tvShowId, result.MovieId);
        Assert.Equal(1, tvRepository.GetByTmdbIdCallCount);
        Assert.Equal(1, tvRepository.PerfContext.Metrics.ValidationCatalogHits);
    }

    [Fact]
    public async Task ResolveAsyncRejectsWrongCatalogTmdbHintAndFallsBackToTitleSearch()
    {
        var wrongCatalogId = Guid.NewGuid();
        var arrivalId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository();
        movieRepository.MoviesByTmdbId[1] = CreateMovie(wrongCatalogId, 1, "Different Title", 2000);
        movieRepository.MoviesByTmdbId[42] = CreateMovie(arrivalId, 42, "Arrival", 2016);
        var movieProvider = new TrackingMovieDataProvider
        {
            SearchResults =
            [
                new MovieProviderSummary("42", 42, null, null, "Arrival", null, new DateOnly(2016, 1, 1), null, 7m, 100)
            ]
        };
        movieProvider.MovieDetailsByTmdbId[1] = CreateMovieProviderDetails(1, "Different Title", 2000);
        movieProvider.MovieDetailsByTmdbId[42] = CreateMovieProviderDetails(42, "Arrival", 2016);

        var resolver = CreateResolver(movieRepository, movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", 1, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("Arrival", result!.Title);
        Assert.Equal(arrivalId, result.MovieId);
        Assert.Equal(1, movieProvider.SearchCallCount);
        Assert.Equal(2, movieProvider.GetMovieCallCount);
        Assert.Equal(1, movieRepository.GetByTmdbIdCallCount);
        Assert.Equal(1, movieProvider.PerfContext.Metrics.ValidationSearchFallbacks);
        Assert.Equal(1, movieRepository.PerfContext.Metrics.ValidationCatalogHits);
    }

    [Fact]
    public async Task ResolveAsyncRejectsWrongCatalogTvTmdbHintForTurkishTitle()
    {
        var voiceId = Guid.NewGuid();
        var icindeId = Guid.NewGuid();
        var tvRepository = new TrackingTvShowRepository();
        tvRepository.TvShowsByTmdbId[56676] = CreateTvShow(voiceId, 56676, "The Voice of Romania", 2011);
        tvRepository.TvShowsByTmdbId[67750] = CreateTvShow(icindeId, 67750, "İçerde", 2016);

        var tvProvider = new TrackingTvShowDataProvider
        {
            SearchResults =
            [
                new TvShowProviderSummary(
                    "67750",
                    67750,
                    null,
                    null,
                    "İçerde",
                    null,
                    null,
                    new DateOnly(2016, 9, 19),
                    null,
                    null,
                    null,
                    8m,
                    100)
            ]
        };
        tvProvider.TvShowDetailsByTmdbId[56676] = CreateTvShowProviderDetails(56676, "The Voice of Romania", 2011);
        tvProvider.TvShowDetailsByTmdbId[67750] = CreateTvShowProviderDetails(
            67750,
            "İçerde",
            2016,
            originalTitle: "İçerde");

        var resolver = CreateResolver(tvShowRepository: tvRepository, tvShowProvider: tvProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("İçerde", 2016, "tv", 56676, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("İçerde", result!.Title);
        Assert.Equal(icindeId, result.MovieId);
        Assert.NotEqual(voiceId, result.MovieId);
        Assert.Equal(1, tvProvider.SearchCallCount);
        Assert.Equal(2, tvProvider.GetTvShowCallCount);
        Assert.Equal(1, tvRepository.GetByTmdbIdCallCount);
        Assert.Equal(1, tvProvider.PerfContext.Metrics.ValidationSearchFallbacks);
    }

    [Fact]
    public async Task ResolveAsyncReturnsNullWhenWrongTmdbHintAndTitleSearchFails()
    {
        var voiceId = Guid.NewGuid();
        var tvRepository = new TrackingTvShowRepository();
        tvRepository.TvShowsByTmdbId[56676] = CreateTvShow(voiceId, 56676, "The Voice of Romania", 2011);

        var tvProvider = new TrackingTvShowDataProvider
        {
            SearchResults = []
        };
        tvProvider.TvShowDetailsByTmdbId[56676] = CreateTvShowProviderDetails(56676, "The Voice of Romania", 2011);

        var resolver = CreateResolver(tvShowRepository: tvRepository, tvShowProvider: tvProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("İçerde", 2016, "tv", 56676, "Reason"));

        Assert.Null(result);
        Assert.Equal(2, tvProvider.SearchCallCount);
        Assert.Equal(1, tvProvider.GetTvShowCallCount);
        Assert.Equal(0, tvRepository.GetByTmdbIdCallCount);
    }

    [Fact]
    public async Task ResolveAsyncSearchFallbackAcceptsLocalizedTitleViaTmdbProviderSearchTitles()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository();
        movieRepository.MoviesByTmdbId[42444] = CreateMovie(movieId, 42444, "G.O.R.A.", 2004);

        var movieProvider = new TrackingMovieDataProvider
        {
            SearchResults =
            [
                new MovieProviderSummary(
                    "42444",
                    42444,
                    null,
                    null,
                    "G.O.R.A.",
                    null,
                    new DateOnly(2004, 1, 1),
                    null,
                    7m,
                    100)
            ]
        };
        movieProvider.MovieDetailsByTmdbId[42444] = CreateMovieProviderDetails(
            42444,
            "G.O.R.A.",
            2004,
            originalTitle: "G.O.R.A.",
            providerSearchTitles:
            [
                new ProviderSearchTitleEntry(
                    "Gora",
                    ContentSearchTitleKind.Translation,
                    ContentSearchTitleSource.TmdbTranslation,
                    "tr",
                    "TR",
                    null)
            ]);

        var resolver = CreateResolver(movieRepository, movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Gora", 2004, "movie", 34589, "Reason"));

        Assert.NotNull(result);
        Assert.Equal(movieId, result!.MovieId);
        Assert.Equal(1, movieProvider.SearchCallCount);
        Assert.Equal(2, movieProvider.GetMovieCallCount);
    }

    [Fact]
    public async Task ResolveAsyncAcceptsCorrectTmdbHintWhenLocalizedTitleMatchesTmdbProviderSearchTitles()
    {
        var icindeId = Guid.NewGuid();
        var tvRepository = new TrackingTvShowRepository();
        var insider = CreateTvShow(icindeId, 67750, "Insider", 2016);
        insider.OriginalTitle = null;
        tvRepository.TvShowsByTmdbId[67750] = insider;

        var tvProvider = new TrackingTvShowDataProvider();
        tvProvider.TvShowDetailsByTmdbId[67750] = CreateTvShowProviderDetails(
            67750,
            "Insider",
            2016,
            originalTitle: null,
            providerSearchTitles:
            [
                new ProviderSearchTitleEntry(
                    "İçerde",
                    ContentSearchTitleKind.Translation,
                    ContentSearchTitleSource.TmdbTranslation,
                    "tr",
                    "TR",
                    null)
            ]);

        var resolver = CreateResolver(tvShowRepository: tvRepository, tvShowProvider: tvProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("İçerde", 2016, "tv", 67750, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("Insider", result!.Title);
        Assert.Equal(icindeId, result.MovieId);
        Assert.Equal(1, tvProvider.GetTvShowCallCount);
        Assert.Equal(0, tvRepository.PerfContext.Metrics.ValidationSearchFallbacks);
    }

    [Fact]
    public async Task ResolveAsyncRejectsWhenCatalogExistsButTmdbMetadataConflictsWithSuggestion()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository
        {
            MovieByTmdbId = CreateMovie(movieId, 329996, "Arrival", 2016)
        };
        var movieProvider = new TrackingMovieDataProvider();
        movieProvider.MovieDetailsByTmdbId[329996] = CreateMovieProviderDetails(329996, "Arrival", 2016);

        var resolver = CreateResolver(movieRepository, movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("The Voice of Romania", 2016, "movie", 329996, "Reason"));

        Assert.Null(result);
        Assert.Equal(1, movieProvider.GetMovieCallCount);
        Assert.Equal(0, movieRepository.GetByTmdbIdCallCount);
    }

    [Fact]
    public async Task ResolveAsyncRejectsUnrelatedTitleDespiteValidTmdbId()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository
        {
            MovieByTmdbId = CreateMovie(movieId, 329996, "Arrival", 2016)
        };

        var movieProvider = new TrackingMovieDataProvider();
        movieProvider.MovieDetailsByTmdbId[329996] = CreateMovieProviderDetails(329996, "Arrival", 2016);

        var resolver = CreateResolver(movieRepository, movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("The Voice of Romania", 2016, "movie", 329996, "Reason"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsyncAcceptsCorrectTmdbHintWhenTitleMatchesWithPunctuationVariant()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository
        {
            MovieByTmdbId = CreateMovie(movieId, 42, "Spider-Man", 2002)
        };

        var movieProvider = new TrackingMovieDataProvider();
        movieProvider.MovieDetailsByTmdbId[42] = CreateMovieProviderDetails(42, "Spider-Man", 2002);

        var resolver = CreateResolver(movieRepository, movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Spider Man", 2002, "movie", 42, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("Spider-Man", result!.Title);
        Assert.Equal(movieId, result.MovieId);
        Assert.Equal(0, movieRepository.PerfContext.Metrics.ValidationSearchFallbacks);
    }

    [Fact]
    public async Task ResolveAsyncUsesLightweightProviderSearchFallbackWhenTmdbIdMissing()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository();
        movieRepository.MoviesByTmdbId[42] = CreateMovie(movieId, 42, "Arrival", 2016);
        var movieProvider = new TrackingMovieDataProvider
        {
            SearchResults =
            [
                new MovieProviderSummary("42", 42, null, null, "Arrival", null, new DateOnly(2016, 1, 1), null, 7m, 100)
            ]
        };
        movieProvider.MovieDetailsByTmdbId[42] = CreateMovieProviderDetails(42, "Arrival", 2016);

        var resolver = CreateResolver(movieRepository, movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", null, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("Arrival", result!.Title);
        Assert.Equal(1, movieProvider.SearchCallCount);
        Assert.Equal(1, movieProvider.GetMovieCallCount);
        Assert.Equal(1, movieRepository.GetByTmdbIdCallCount);
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

        var movieProvider = new TrackingMovieDataProvider();
        movieProvider.MovieDetailsByTmdbId[329996] = CreateMovieProviderDetails(329996, "Arrival", 2016);

        var resolver = CreateResolver(movieRepository, movieProvider);
        var suggestion = new AiProviderSuggestion("Arrival", 2016, "movie", 329996, "Reason");

        var first = await resolver.ResolveAsync(suggestion);
        var second = await resolver.ResolveAsync(suggestion);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.MovieId, second!.MovieId);
        Assert.Equal(1, movieProvider.GetMovieCallCount);
        Assert.Equal(2, movieRepository.GetByTmdbIdCallCount);
        Assert.Equal(2, movieRepository.PerfContext.Metrics.ValidationCatalogHits);
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
            SearchResults =
            [
                new MovieProviderSummary("42", 42, null, null, "Arrival", null, new DateOnly(2016, 1, 1), null, 7m, 100)
            ]
        };
        movieProvider.MovieDetailsByTmdbId[42] = CreateMovieProviderDetails(42, "Arrival", 2016);

        var resolver = CreateResolver(movieRepository, movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", 9866, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("Arrival", result!.Title);
        Assert.Equal(2, movieProvider.GetMovieCallCount);
        Assert.Equal(1, movieProvider.SearchCallCount);
        Assert.Equal(1, movieRepository.GetByTmdbIdCallCount);
        Assert.Equal(0, movieProvider.PerfContext.Metrics.ValidationProviderFallbacks);
        Assert.Equal(1, movieProvider.PerfContext.Metrics.ValidationSearchFallbacks);
        Assert.Equal(1, movieProvider.PerfContext.Metrics.ValidationCatalogHits);
    }

    [Fact]
    public async Task ResolveAsyncRejectsWrongProviderTmdbHintAndFallsBackToTitleSearch()
    {
        var arrivalId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository();
        movieRepository.MoviesByTmdbId[42] = CreateMovie(arrivalId, 42, "Arrival", 2016);
        var movieProvider = new TrackingMovieDataProvider
        {
            SearchResults =
            [
                new MovieProviderSummary("42", 42, null, null, "Arrival", null, new DateOnly(2016, 1, 1), null, 7m, 100)
            ]
        };
        movieProvider.MovieDetailsByTmdbId[545] = CreateMovieProviderDetails(545, "Different Title", 2000);
        movieProvider.MovieDetailsByTmdbId[42] = CreateMovieProviderDetails(42, "Arrival", 2016);
        var catalogUpsert = new TrackingCatalogProviderUpsertService
        {
            UpsertedMovie = CreateMovie(Guid.NewGuid(), 545, "Different Title", 2000)
        };

        var resolver = CreateResolver(movieRepository, movieProvider, catalogUpsert: catalogUpsert);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", 545, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("Arrival", result!.Title);
        Assert.Equal(arrivalId, result.MovieId);
        Assert.Equal(2, movieProvider.GetMovieCallCount);
        Assert.Equal(1, movieProvider.SearchCallCount);
        Assert.Equal(0, movieProvider.PerfContext.Metrics.ValidationProviderFallbacks);
        Assert.Equal(1, movieProvider.PerfContext.Metrics.ValidationSearchFallbacks);
    }

    [Fact]
    public async Task ResolveAsyncReturnsNullWhenHintedProviderAndSearchBothFail()
    {
        var movieProvider = new TrackingMovieDataProvider
        {
            SearchResults =
            [
                new MovieProviderSummary("1", 1, null, null, "Arrival", null, new DateOnly(2016, 1, 1), null, 7m, 1),
                new MovieProviderSummary("2", 2, null, null, "Arrival", null, new DateOnly(2016, 6, 1), null, 6m, 1)
            ]
        };
        movieProvider.MovieDetailsByTmdbId[1] = CreateMovieProviderDetails(1, "Arrival", 2016);
        movieProvider.MovieDetailsByTmdbId[2] = CreateMovieProviderDetails(2, "Arrival", 2016);

        var resolver = CreateResolver(new TrackingMovieRepository(), movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2016, "movie", 9866, "Reason"));

        Assert.Null(result);
        Assert.Equal(3, movieProvider.GetMovieCallCount);
        Assert.Equal(1, movieProvider.SearchCallCount);
        Assert.Equal(0, movieProvider.PerfContext.Metrics.ValidationProviderFallbacks);
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
        movieProvider.MovieDetailsByTmdbId[1] = CreateMovieProviderDetails(1, "Arrival", 2016);
        movieProvider.MovieDetailsByTmdbId[2] = CreateMovieProviderDetails(2, "Arrival", 2016);

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

    [Fact]
    public async Task ResolveAsyncSearchFallbackMatchesMovieOriginalTitle()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository();
        movieRepository.MoviesByTmdbId[1] = CreateMovie(Guid.NewGuid(), 1, "Different Title", 2000);
        var spanishApartment = CreateMovie(movieId, 42, "The Spanish Apartment", 2002);
        spanishApartment.OriginalTitle = "L'Auberge Espagnole";
        movieRepository.MoviesByTmdbId[42] = spanishApartment;
        var movieProvider = new TrackingMovieDataProvider
        {
            SearchResults =
            [
                new MovieProviderSummary(
                    "42",
                    42,
                    null,
                    null,
                    "The Spanish Apartment",
                    null,
                    new DateOnly(2002, 5, 17),
                    null,
                    7m,
                    100,
                    "L'Auberge Espagnole")
            ]
        };
        movieProvider.MovieDetailsByTmdbId[42] = CreateMovieProviderDetails(
            42,
            "The Spanish Apartment",
            2002,
            originalTitle: "L'Auberge Espagnole");

        var resolver = CreateResolver(movieRepository, movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("L'Auberge Espagnole", 2002, "movie", null, "Reason"));

        Assert.NotNull(result);
        Assert.Equal(movieId, result!.MovieId);
        Assert.Equal("The Spanish Apartment", result.Title);
    }

    [Fact]
    public async Task ResolveAsyncSearchFallbackMatchesPunctuationVariantTitle()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository();
        movieRepository.MoviesByTmdbId[42] = CreateMovie(movieId, 42, "Spider-Man", 2002);
        var movieProvider = new TrackingMovieDataProvider
        {
            SearchResults =
            [
                new MovieProviderSummary(
                    "42",
                    42,
                    null,
                    null,
                    "Spider-Man",
                    null,
                    new DateOnly(2002, 5, 3),
                    null,
                    7m,
                    100)
            ]
        };
        movieProvider.MovieDetailsByTmdbId[999] = CreateMovieProviderDetails(999, "Unrelated", 2002);
        movieProvider.MovieDetailsByTmdbId[42] = CreateMovieProviderDetails(42, "Spider-Man", 2002);

        var resolver = CreateResolver(movieRepository, movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Spider Man", 2002, "movie", 999, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("Spider-Man", result!.Title);
    }

    [Fact]
    public async Task ResolveAsyncSearchFallbackAllowsYearWithinOne()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository();
        movieRepository.MoviesByTmdbId[42] = CreateMovie(movieId, 42, "Blade Runner 2049", 2017);
        var movieProvider = new TrackingMovieDataProvider
        {
            SearchResults =
            [
                new MovieProviderSummary(
                    "42",
                    42,
                    null,
                    null,
                    "Blade Runner 2049",
                    null,
                    new DateOnly(2017, 10, 6),
                    null,
                    8m,
                    1000)
            ]
        };
        movieProvider.MovieDetailsByTmdbId[999] = CreateMovieProviderDetails(999, "Unrelated", 2000);
        movieProvider.MovieDetailsByTmdbId[42] = CreateMovieProviderDetails(42, "Blade Runner 2049", 2017);

        var resolver = CreateResolver(movieRepository, movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Blade Runner 2049", 2018, "movie", 999, "Reason"));

        Assert.NotNull(result);
        Assert.Equal(2017, result!.Year);
    }

    [Fact]
    public async Task ResolveAsyncSearchFallbackRejectsYearOutsideOne()
    {
        var movieProvider = new TrackingMovieDataProvider
        {
            SearchResults =
            [
                new MovieProviderSummary(
                    "42",
                    42,
                    null,
                    null,
                    "Arrival",
                    null,
                    new DateOnly(2016, 11, 11),
                    null,
                    7m,
                    100)
            ]
        };

        var resolver = CreateResolver(new TrackingMovieRepository(), movieProvider);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Arrival", 2020, "movie", null, "Reason"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsyncSearchFallbackRejectsAmbiguousMatches()
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
        Assert.Equal(2, movieProvider.SearchCallCount);
    }

    [Fact]
    public async Task ResolveAsyncPassesTurkishLanguageToTmdbSearch()
    {
        var movieId = Guid.NewGuid();
        var movieRepository = new TrackingMovieRepository
        {
            MovieByTmdbId = CreateMovie(movieId, 42, "G.O.R.A.", 2004)
        };
        var movieProvider = new TrackingMovieDataProvider();
        movieProvider.MovieDetailsByTmdbId[42] = CreateMovieProviderDetails(
            42,
            "G.O.R.A.",
            2004,
            providerSearchTitles:
            [
                new ProviderSearchTitleEntry(
                    "Gora",
                    ContentSearchTitleKind.Translation,
                    ContentSearchTitleSource.TmdbTranslation,
                    "tr",
                    "TR",
                    null)
            ]);

        var tmdbSearch = new TrackingAiRecommendationTmdbSearch(movieProvider, new TrackingTvShowDataProvider())
        {
            MovieSearchResults =
            [
                new MovieProviderSummary("42", 42, null, null, "Gora", null, new DateOnly(2004, 1, 1), null, 7m, 100)
            ]
        };

        var resolver = CreateResolver(movieRepository, movieProvider, tmdbSearch: tmdbSearch);
        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Gora", 2004, "movie", null, "Reason"),
            searchLanguage: "tr");

        Assert.NotNull(result);
        Assert.Equal("tr-TR", tmdbSearch.LastMovieSearchLanguage);
    }

    [Fact]
    public async Task ResolveAsyncFallsBackToAlternateMediaTypeAfterPrimarySearchFails()
    {
        var tvShowId = Guid.NewGuid();
        var tvRepository = new TrackingTvShowRepository
        {
            TvShowByTmdbId = CreateTvShow(tvShowId, 99, "Sample Series", 2020)
        };
        var movieProvider = new TrackingMovieDataProvider();
        var tvProvider = new TrackingTvShowDataProvider();
        tvProvider.TvShowDetailsByTmdbId[99] = CreateTvShowProviderDetails(99, "Sample Series", 2020);

        var tmdbSearch = new TrackingAiRecommendationTmdbSearch(movieProvider, tvProvider)
        {
            MovieSearchResults = [],
            TvSearchResults =
            [
                new TvShowProviderSummary(
                    "99",
                    99,
                    null,
                    null,
                    "Sample Series",
                    null,
                    null,
                    new DateOnly(2020, 1, 1),
                    null,
                    null,
                    null,
                    8m,
                    100)
            ]
        };

        var resolver = CreateResolver(
            movieRepository: new TrackingMovieRepository(),
            movieProvider: movieProvider,
            tvShowRepository: tvRepository,
            tvShowProvider: tvProvider,
            tmdbSearch: tmdbSearch);

        var result = await resolver.ResolveAsync(
            new AiProviderSuggestion("Sample Series", 0, "movie", null, "Reason"));

        Assert.NotNull(result);
        Assert.Equal("tv", result!.MediaType);
        Assert.Equal(tvShowId, result.MovieId);
        Assert.Equal(1, tmdbSearch.MovieSearchCallCount);
        Assert.Equal(1, tmdbSearch.TvSearchCallCount);
    }

    private static AiMovieIdentityResolver CreateResolver(
        TrackingMovieRepository? movieRepository = null,
        TrackingMovieDataProvider? movieProvider = null,
        TrackingTvShowRepository? tvShowRepository = null,
        TrackingTvShowDataProvider? tvShowProvider = null,
        TrackingCatalogProviderUpsertService? catalogUpsert = null,
        TrackingAiRecommendationTmdbSearch? tmdbSearch = null)
    {
        movieRepository ??= new TrackingMovieRepository();
        movieProvider ??= new TrackingMovieDataProvider();
        tvShowRepository ??= new TrackingTvShowRepository();
        tvShowProvider ??= new TrackingTvShowDataProvider();
        catalogUpsert ??= new TrackingCatalogProviderUpsertService();
        tmdbSearch ??= new TrackingAiRecommendationTmdbSearch(movieProvider, tvShowProvider);

        movieRepository.PerfContext = catalogUpsert.PerfContext;
        tvShowRepository.PerfContext = catalogUpsert.PerfContext;
        movieProvider.PerfContext = catalogUpsert.PerfContext;
        tvShowProvider.PerfContext = catalogUpsert.PerfContext;
        tmdbSearch.MovieProvider = movieProvider;
        tmdbSearch.TvShowProvider = tvShowProvider;

        return new AiMovieIdentityResolver(
            movieRepository,
            tvShowRepository,
            movieProvider,
            tvShowProvider,
            catalogUpsert,
            tmdbSearch,
            catalogUpsert.PerfContext);
    }

    private sealed class TrackingAiRecommendationTmdbSearch : IAiRecommendationTmdbSearch
    {
        public TrackingAiRecommendationTmdbSearch(
            TrackingMovieDataProvider movieProvider,
            TrackingTvShowDataProvider tvShowProvider)
        {
            MovieProvider = movieProvider;
            TvShowProvider = tvShowProvider;
        }

        public TrackingMovieDataProvider MovieProvider { get; set; }

        public TrackingTvShowDataProvider TvShowProvider { get; set; }

        public IReadOnlyList<MovieProviderSummary> MovieSearchResults { get; set; } = [];

        public IReadOnlyList<TvShowProviderSummary> TvSearchResults { get; set; } = [];

        public int MovieSearchCallCount { get; private set; }

        public int TvSearchCallCount { get; private set; }

        public string? LastMovieSearchLanguage { get; private set; }

        public async Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            string? language,
            CancellationToken cancellationToken = default)
        {
            MovieSearchCallCount++;
            LastMovieSearchLanguage = language;
            _ = await MovieProvider.SearchMoviesAsync(query, page, pageSize, cancellationToken);
            var results = MovieSearchResults.Count > 0 ? MovieSearchResults : MovieProvider.SearchResults;
            return new MovieProviderSearchResult(results, page, pageSize, results.Count, 1);
        }

        public async Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            string? language,
            CancellationToken cancellationToken = default)
        {
            TvSearchCallCount++;
            _ = await TvShowProvider.SearchTvShowsAsync(query, page, pageSize, cancellationToken);
            var results = TvSearchResults.Count > 0 ? TvSearchResults : TvShowProvider.SearchResults;
            return new TvShowProviderSearchResult(results, page, pageSize, results.Count, 1);
        }
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

    private static MovieProviderDetails CreateMovieProviderDetails(
        int tmdbId,
        string title,
        int year,
        string? originalTitle = null,
        IReadOnlyList<ProviderSearchTitleEntry>? providerSearchTitles = null) =>
        new(
            tmdbId.ToString(CultureInfo.InvariantCulture),
            tmdbId,
            null,
            null,
            title,
            originalTitle ?? title,
            "Overview",
            new DateOnly(year, 1, 1),
            116,
            "/poster.jpg",
            "/backdrop.jpg",
            "en",
            7.8m,
            1000,
            ["Science Fiction"],
            ProviderSearchTitles: providerSearchTitles);

    private static TvShowProviderDetails CreateTvShowProviderDetails(
        int tmdbId,
        string title,
        int year,
        string? originalTitle = null,
        IReadOnlyList<ProviderSearchTitleEntry>? providerSearchTitles = null) =>
        new(
            tmdbId.ToString(CultureInfo.InvariantCulture),
            tmdbId,
            null,
            null,
            title,
            originalTitle ?? title,
            "Overview",
            new DateOnly(year, 1, 20),
            null,
            "/poster.jpg",
            "/backdrop.jpg",
            "en",
            8m,
            1000,
            "Ended",
            ["Drama"],
            [],
            ProviderSearchTitles: providerSearchTitles);

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

        public Dictionary<int, TvShow?> TvShowsByTmdbId { get; } = new();

        public int GetByTmdbIdCallCount { get; private set; }

        public AiRecommendationPerfContext PerfContext { get; set; } = new();

        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default)
        {
            GetByTmdbIdCallCount++;
            if (TvShowsByTmdbId.TryGetValue(tmdbId, out var mapped))
            {
                return Task.FromResult(mapped);
            }

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

        public Dictionary<int, MovieProviderDetails> MovieDetailsByTmdbId { get; } = new();

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
            bool includeKeywords = false,
            CancellationToken cancellationToken = default)
        {
            GetMovieCallCount++;
            if (GetMovieFactory is not null)
            {
                return Task.FromResult(GetMovieFactory(externalId));
            }

            if (int.TryParse(externalId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tmdbId) &&
                MovieDetailsByTmdbId.TryGetValue(tmdbId, out var mapped))
            {
                return Task.FromResult<MovieProviderDetails?>(mapped);
            }

            return Task.FromResult(MovieDetails);
        }
    }

    private sealed class TrackingTvShowDataProvider : ITvShowDataProvider
    {
        public Dictionary<int, TvShowProviderDetails> TvShowDetailsByTmdbId { get; } = new();

        public IReadOnlyList<TvShowProviderSummary> SearchResults { get; set; } = [];

        public int SearchCallCount { get; private set; }

        public int GetTvShowCallCount { get; private set; }

        public AiRecommendationPerfContext PerfContext { get; set; } = new();

        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            SearchCallCount++;
            return Task.FromResult(
                new TvShowProviderSearchResult(SearchResults, page, pageSize, SearchResults.Count, 1));
        }

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderDetails?> GetTvShowAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default)
        {
            GetTvShowCallCount++;
            if (int.TryParse(externalId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tmdbId) &&
                TvShowDetailsByTmdbId.TryGetValue(tmdbId, out var mapped))
            {
                return Task.FromResult<TvShowProviderDetails?>(mapped);
            }

            return Task.FromResult<TvShowProviderDetails?>(null);
        }

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

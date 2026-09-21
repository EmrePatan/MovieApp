using System.Net;
using Microsoft.Extensions.Options;
using MovieApp.Application.Models.Movies;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbCanonicalCatalogLanguageTests
{
    private const string CanonicalLanguage = "en-US";

    [Fact]
    public async Task GetMovieAsync_RequestsCanonicalLanguage_ForCatalogPersistencePath()
    {
        var handler = CreateHandlerReturningMovieTitle("Interstellar");
        var provider = CreateMovieProvider(handler);

        var result = await provider.GetMovieAsync("tmdb-157336");

        Assert.NotNull(result);
        Assert.Equal("Interstellar", result.Title);
        AssertCanonicalLanguage(handler.Requests.Single().RequestUri?.Query);
    }

    [Fact]
    public async Task SearchMoviesAsync_RequestsCanonicalLanguage_EvenWhenPathContainsAlternateLanguage()
    {
        var handler = CreateHandlerReturningSearchTitle("Interstellar");
        var provider = CreateMovieProvider(handler);

        await provider.SearchMoviesAsync(
            "Interstellar",
            MovieSearchPagination.DefaultPage,
            MovieSearchPagination.DefaultPageSize);

        AssertCanonicalLanguage(handler.Requests.Single().RequestUri?.Query);
        Assert.DoesNotContain("tr-TR", handler.Requests.Single().RequestUri?.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTvShowAsync_RequestsCanonicalLanguage_ForCatalogPersistencePath()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": 1399,
                  "name": "Game of Thrones",
                  "original_name": "Game of Thrones",
                  "overview": "Seven noble families fight for control.",
                  "first_air_date": "2011-04-17",
                  "poster_path": "/poster.jpg",
                  "vote_average": 8.4,
                  "vote_count": 21000,
                  "seasons": [],
                  "external_ids": { "tvdb_id": 121361 }
                }
                """)
        });

        var provider = CreateTvProvider(handler);
        var result = await provider.GetTvShowAsync("tmdb-1399");

        Assert.NotNull(result);
        Assert.Equal("Game of Thrones", result.Title);
        AssertCanonicalLanguage(handler.Requests.Single().RequestUri?.Query);
    }

    [Fact]
    public async Task GetSeasonAsync_RequestsCanonicalLanguage_ForSeasonPersistencePath()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "season_number": 1,
                  "name": "Season 1",
                  "overview": "Season overview",
                  "episodes": []
                }
                """)
        });

        var provider = CreateTvProvider(handler);
        var result = await provider.GetSeasonAsync("tmdb-1399", 1);

        Assert.NotNull(result);
        Assert.Equal("Season 1", result.Name);
        AssertCanonicalLanguage(handler.Requests.Single().RequestUri?.Query);
    }

    [Fact]
    public async Task GetPersonAsync_RequestsCanonicalLanguage_ForPersonPersistencePath()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": 1892,
                  "name": "Matthew McConaughey",
                  "biography": "Actor biography",
                  "profile_path": "/profile.jpg",
                  "combined_credits": {
                    "cast": [],
                    "crew": []
                  }
                }
                """)
        });

        var provider = CreatePersonProvider(handler);
        var result = await provider.GetPersonAsync(1892);

        Assert.NotNull(result);
        Assert.Equal("Matthew McConaughey", result.Name);
        Assert.Single(handler.Requests);
        Assert.All(handler.Requests, request => AssertCanonicalLanguage(request.RequestUri?.Query));
    }

    [Fact]
    public async Task GetCollectionAsync_RequestsCanonicalLanguage_ForCollectionPersistencePath()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": 10,
                  "name": "Star Wars Collection",
                  "overview": "Collection overview",
                  "parts": []
                }
                """)
        });

        var provider = CreateCollectionProvider(handler);
        var result = await provider.GetCollectionAsync(10);

        Assert.NotNull(result);
        Assert.Equal("Star Wars Collection", result.Name);
        AssertCanonicalLanguage(handler.Requests.Single().RequestUri?.Query);
    }

    [Fact]
    public async Task GetCanonicalAsync_UsesConfiguredCanonicalLanguage_NotRequestLocale()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":1,"title":"Interstellar"}""")
        });

        var client = CreateApiClient(handler, new TmdbOptions
        {
            ApiKey = "test-api-key",
            CanonicalLanguage = CanonicalLanguage
        });

        await client.GetCanonicalAsync<TestMovieResponse>("movie/1?language=tr-TR");

        var query = handler.Requests.Single().RequestUri?.Query ?? string.Empty;
        AssertCanonicalLanguage(query);
        Assert.DoesNotContain("tr-TR", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureFromSummariesIngest_UsesCanonicalEnglishTitle_FromCanonicalProviderFetch()
    {
        var handler = CreateHandlerReturningMovieTitle("Interstellar");
        var provider = CreateMovieProvider(handler);
        var result = await provider.GetMovieAsync("tmdb-157336");

        Assert.NotNull(result);
        Assert.Equal("Interstellar", result.Title);
        Assert.NotEqual("Yıldızlararası", result.Title);
    }

    private static void AssertCanonicalLanguage(string? query)
    {
        Assert.NotNull(query);
        Assert.Contains($"language={CanonicalLanguage}", query, StringComparison.Ordinal);
    }

    private static MockHttpMessageHandler CreateHandlerReturningMovieTitle(string title)
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""
                  {
                    "id": 157336,
                    "title": "{{title}}",
                    "original_title": "{{title}}",
                    "overview": "Overview",
                    "release_date": "2014-11-07",
                    "poster_path": "/poster.jpg",
                    "vote_average": 8.7,
                    "vote_count": 25000,
                    "runtime": 169,
                    "genres": [],
                    "external_ids": { "imdb_id": "tt0816692" }
                  }
                  """)
        });

        return handler;
    }

    private static MockHttpMessageHandler CreateHandlerReturningSearchTitle(string title)
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""
                  {
                    "page": 1,
                    "total_pages": 1,
                    "total_results": 1,
                    "results": [
                      {
                        "id": 157336,
                        "title": "{{title}}",
                        "overview": "Overview",
                        "release_date": "2014-11-07",
                        "poster_path": "/poster.jpg",
                        "vote_average": 8.7,
                        "vote_count": 25000
                      }
                    ]
                  }
                  """)
        });

        return handler;
    }

    private static TmdbMovieDataProvider CreateMovieProvider(MockHttpMessageHandler handler) =>
        new(CreateApiClient(handler));

    private static TmdbTvShowDataProvider CreateTvProvider(MockHttpMessageHandler handler) =>
        new(CreateApiClient(handler));

    private static TmdbPersonDataProvider CreatePersonProvider(MockHttpMessageHandler handler) =>
        new(CreateApiClient(handler));

    private static TmdbCollectionDataProvider CreateCollectionProvider(MockHttpMessageHandler handler) =>
        new(CreateApiClient(handler));

    private static TmdbApiClient CreateApiClient(
        MockHttpMessageHandler handler,
        TmdbOptions? options = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };

        return new TmdbApiClient(
            httpClient,
            Options.Create(options ?? new TmdbOptions
            {
                ApiKey = "test-api-key",
                CanonicalLanguage = CanonicalLanguage
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TmdbApiClient>.Instance);
    }

    private sealed class TestMovieResponse
    {
        public int Id { get; init; }

        public string Title { get; init; } = string.Empty;
    }
}

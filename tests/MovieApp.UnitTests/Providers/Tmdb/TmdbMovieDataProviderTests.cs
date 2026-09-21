using System.Net;
using Microsoft.Extensions.Options;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbMovieDataProviderTests
{
    [Fact]
    public async Task SearchMoviesAsyncMapsTmdbSearchResponse()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "page": 1,
                  "total_pages": 1,
                  "total_results": 1,
                  "results": [
                    {
                      "id": 157336,
                      "title": "Interstellar",
                      "overview": "A team of explorers travel through a wormhole in space.",
                      "release_date": "2014-11-07",
                      "poster_path": "/poster.jpg",
                      "vote_average": 8.7,
                      "vote_count": 25000
                    }
                  ]
                }
                """)
        });

        var provider = CreateProvider(handler);
        var result = await provider.SearchMoviesAsync(
            "Interstellar",
            MovieSearchPagination.DefaultPage,
            MovieSearchPagination.DefaultPageSize);

        Assert.Single(result.Results);
        Assert.Equal("tmdb-157336", result.Results[0].ExternalId);
        Assert.Equal("Interstellar", result.Results[0].Title);
        Assert.Equal(1, result.Page);
        Assert.Equal(TmdbSearchDefaults.ResultsPerPage, result.PageSize);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task SearchMoviesAsyncPassesRequestedPageToTmdb()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "page": 2,
                  "total_pages": 5,
                  "total_results": 100,
                  "results": []
                }
                """)
        });

        var provider = CreateProvider(handler);
        await provider.SearchMoviesAsync("Interstellar", 2, MovieSearchPagination.DefaultPageSize);

        var requestUri = handler.Requests.Single().RequestUri?.ToString();
        Assert.Contains("page=2", requestUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMovieAsyncReturnsNullForUnknownExternalIdFormat()
    {
        var provider = CreateProvider(new MockHttpMessageHandler());

        var result = await provider.GetMovieAsync("fake-tmdb-900001");

        Assert.Null(result);
    }

    [Fact]
    public async Task DiscoverMoviesAsyncRequestsCanonicalLanguage()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "page": 1,
                  "total_pages": 1,
                  "total_results": 0,
                  "results": []
                }
                """)
        });

        var provider = CreateProvider(handler);
        await provider.DiscoverMoviesAsync(
            new DiscoverProviderCriteria(
                DiscoverBrowseMode.Trending,
                1,
                [],
                null,
                null,
                null,
                null));

        Assert.Contains("language=en-US", handler.Requests.Single().RequestUri?.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMovieAsync_WithIncludeKeywords_UsesSingleAppendRequestAndMapsKeywords()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": 157336,
                  "title": "Interstellar",
                  "external_ids": { "imdb_id": "tt0816692" },
                  "keywords": {
                    "keywords": [
                      { "id": 310, "name": "space travel" }
                    ]
                  }
                }
                """)
        });

        var provider = CreateProvider(handler);
        var result = await provider.GetMovieAsync("tmdb-157336", includeKeywords: true);

        Assert.NotNull(result);
        Assert.Single(result!.Keywords!);
        Assert.Equal("space travel", result.Keywords![0].Name);
        Assert.Single(handler.Requests);
        Assert.Contains("append_to_response=external_ids,keywords", handler.Requests[0].RequestUri?.Query);
    }

    [Fact]
    public async Task GetMovieAsyncReturnsNullWhenTmdbRespondsWithNotFound()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.NotFound));

        var provider = CreateProvider(handler);
        var result = await provider.GetMovieAsync("tmdb-157336");

        Assert.Null(result);
    }

    private static TmdbMovieDataProvider CreateProvider(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };

        var apiClient = new TmdbApiClient(
            httpClient,
            Options.Create(new TmdbOptions
            {
                ApiKey = "test-api-key",
                CanonicalLanguage = "en-US"
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TmdbApiClient>.Instance);

        return new TmdbMovieDataProvider(apiClient);
    }
}

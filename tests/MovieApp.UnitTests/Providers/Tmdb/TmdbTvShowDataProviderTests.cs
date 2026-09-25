using System.Net;
using Microsoft.Extensions.Options;
using MovieApp.Application.Models.Movies;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbTvShowDataProviderTests
{
    [Fact]
    public async Task SearchTvShowsAsyncMapsTmdbSearchResponse()
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
                      "id": 1399,
                      "name": "Game of Thrones",
                      "original_name": "Game of Thrones",
                      "overview": "Nine noble families fight for control over the lands of Westeros.",
                      "first_air_date": "2011-04-17",
                      "poster_path": "/poster.jpg",
                      "vote_average": 8.3,
                      "vote_count": 21000
                    }
                  ]
                }
                """)
        });

        var provider = CreateProvider(handler);
        var result = await provider.SearchTvShowsAsync(
            "Game of Thrones",
            MovieSearchPagination.DefaultPage,
            MovieSearchPagination.DefaultPageSize);

        Assert.Single(result.Results);
        Assert.Equal("tmdb-1399", result.Results[0].ExternalId);
        Assert.Equal("Game of Thrones", result.Results[0].Title);
        Assert.Equal(new DateOnly(2011, 4, 17), result.Results[0].FirstAirDate);
        Assert.Equal(1, result.Page);
        Assert.Equal(TmdbSearchDefaults.ResultsPerPage, result.PageSize);
    }

    [Fact]
    public async Task SearchTvShowsAsyncPassesRequestedPageToTmdb()
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
        await provider.SearchTvShowsAsync("Thrones", 2, MovieSearchPagination.DefaultPageSize);

        var requestUri = handler.Requests.Single().RequestUri?.ToString();
        Assert.Contains("page=2", requestUri, StringComparison.Ordinal);
        Assert.Contains("search/tv", requestUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTvShowAsyncReturnsNullForUnknownExternalIdFormat()
    {
        var provider = CreateProvider(new MockHttpMessageHandler());

        var result = await provider.GetTvShowAsync("fake-tv-900101");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTvShowAsyncReturnsNullWhenTmdbRespondsWithNotFound()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.NotFound));

        var provider = CreateProvider(handler);
        var result = await provider.GetTvShowAsync("tmdb-1399");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTvShowAsync_WithIncludeKeywords_UsesSingleAppendRequestAndMapsKeywords()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": 1399,
                  "name": "Game of Thrones",
                  "external_ids": { "imdb_id": "tt0944947" },
                  "keywords": {
                    "results": [
                      { "id": 9715, "name": "dragon" }
                    ]
                  }
                }
                """)
        });

        var provider = CreateProvider(handler);
        var result = await provider.GetTvShowAsync("tmdb-1399", includeKeywords: true);

        Assert.NotNull(result);
        Assert.Single(result!.Keywords!);
        Assert.Equal("dragon", result.Keywords![0].Name);
        Assert.Single(handler.Requests);
        Assert.Contains("append_to_response=external_ids,keywords", handler.Requests[0].RequestUri?.Query);
    }

    [Fact]
    public async Task GetTvShowAsyncMapsDetailsResponse()
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
                  "overview": "Overview",
                  "first_air_date": "2011-04-17",
                  "last_air_date": "2019-05-19",
                  "status": "Ended",
                  "poster_path": "/poster.jpg",
                  "vote_average": 8.3,
                  "vote_count": 21000,
                  "genres": [
                    { "id": 18, "name": "Drama" }
                  ],
                  "seasons": [
                    {
                      "id": 3627,
                      "name": "Season 1",
                      "season_number": 1,
                      "episode_count": 10,
                      "air_date": "2011-04-17",
                      "poster_path": "/season1.jpg"
                    }
                  ],
                  "external_ids": {
                    "imdb_id": "tt0944947",
                    "tvdb_id": 121361
                  }
                }
                """)
        });

        var provider = CreateProvider(handler);
        var result = await provider.GetTvShowAsync("tmdb-1399");

        Assert.NotNull(result);
        Assert.Equal("tmdb-1399", result.ExternalId);
        Assert.Equal("Game of Thrones", result.Title);
        Assert.Equal("tt0944947", result.ImdbId);
        Assert.Equal(121361, result.TvdbId);
        Assert.Single(result.Seasons);
        Assert.Equal(1, result.Seasons[0].SeasonNumber);
    }

    [Fact]
    public async Task GetSeasonAsyncMapsSeasonAndEpisodes()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": 3627,
                  "name": "Season 1",
                  "overview": "Season overview",
                  "air_date": "2011-04-17",
                  "episode_count": 1,
                  "poster_path": "/season1.jpg",
                  "season_number": 1,
                  "episodes": [
                    {
                      "id": 63056,
                      "name": "Winter Is Coming",
                      "overview": "Episode overview",
                      "air_date": "2011-04-17",
                      "episode_number": 1,
                      "season_number": 1,
                      "runtime": 62,
                      "still_path": "/still.jpg",
                      "vote_average": 8.0,
                      "vote_count": 100
                    }
                  ]
                }
                """)
        });

        var provider = CreateProvider(handler);
        var result = await provider.GetSeasonAsync("tmdb-1399", 1);

        Assert.NotNull(result);
        Assert.Equal(1, result.SeasonNumber);
        Assert.Single(result.Episodes);
        Assert.Equal("Winter Is Coming", result.Episodes[0].Name);
        Assert.Equal(62, result.Episodes[0].RuntimeMinutes);
    }

    [Fact]
    public async Task GetEpisodeAsyncReturnsNullWhenTmdbRespondsWithNotFound()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.NotFound));

        var provider = CreateProvider(handler);
        var result = await provider.GetEpisodeAsync("tmdb-1399", 1, 99);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetEpisodeAsyncMapsEpisodeResponse()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": 63056,
                  "name": "Winter Is Coming",
                  "overview": "Episode overview",
                  "air_date": "2011-04-17",
                  "episode_number": 1,
                  "season_number": 1,
                  "runtime": 62,
                  "still_path": "/still.jpg",
                  "vote_average": 8.0,
                  "vote_count": 100,
                  "external_ids": {
                    "imdb_id": "tt1480055",
                    "tvdb_id": 3254641
                  }
                }
                """)
        });

        var provider = CreateProvider(handler);
        var result = await provider.GetEpisodeAsync("tmdb-1399", 1, 1);

        Assert.NotNull(result);
        Assert.Equal(1, result.EpisodeNumber);
        Assert.Equal("tt1480055", result.ImdbId);
        Assert.Equal(3254641, result.TvdbId);
    }

    private static TmdbTvShowDataProvider CreateProvider(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };

        var apiClient = new TmdbApiClient(
            httpClient,
            Options.Create(new TmdbOptions { ApiKey = "test-api-key" }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TmdbApiClient>.Instance);

        return new TmdbTvShowDataProvider(apiClient);
    }
}

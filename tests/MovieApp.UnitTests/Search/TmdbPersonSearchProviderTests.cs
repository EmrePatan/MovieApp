using System.Net;
using Microsoft.Extensions.Options;
using MovieApp.Infrastructure.Providers;
using MovieApp.Infrastructure.Providers.Tmdb;
using MovieApp.UnitTests.Providers.Tmdb;

namespace MovieApp.UnitTests.Search;

public sealed class TmdbPersonSearchProviderTests
{
    [Fact]
    public async Task SearchPersonsAsyncMapsTmdbResponse()
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
                      "id": 6384,
                      "name": "Keanu Reeves",
                      "profile_path": "/keanu.jpg",
                      "known_for_department": "Acting",
                      "popularity": 84.2
                    }
                  ]
                }
                """)
        });

        var provider = CreateProvider(handler);
        var result = await provider.SearchPersonsAsync("keanu", 1, 20);

        Assert.Single(result.Results);
        Assert.Equal(6384, result.Results[0].TmdbId);
        Assert.Equal("Keanu Reeves", result.Results[0].Name);
        Assert.Equal("/keanu.jpg", result.Results[0].ProfilePath);
        Assert.Equal("Acting", result.Results[0].KnownForDepartment);
        Assert.Equal(84.2m, result.Results[0].Popularity);
    }

    [Fact]
    public async Task FakePersonSearchReturnsKeanuAndNolan()
    {
        var provider = new FakePersonDataProvider();

        var keanuResults = await provider.SearchPersonsAsync("keanu", 1, 20);
        var nolanResults = await provider.SearchPersonsAsync("nolan", 1, 20);

        Assert.Contains(keanuResults.Results, summary => summary.TmdbId == FakePersonDataProvider.KeanuReevesTmdbId);
        Assert.Contains(nolanResults.Results, summary => summary.TmdbId == FakePersonDataProvider.ChristopherNolanTmdbId);
    }

    private static TmdbPersonDataProvider CreateProvider(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };

        var apiClient = new TmdbApiClient(
            httpClient,
            Options.Create(new TmdbOptions { ApiKey = "test-api-key" }));

        return new TmdbPersonDataProvider(apiClient);
    }
}

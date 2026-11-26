using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbDetailExtrasMappingTests
{
    [Fact]
    public async Task GetMovieAsync_MapsExtras_PreferringTheTheatricalCertification()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": 1,
                  "title": "Film",
                  "tagline": " Tag ",
                  "budget": 0,
                  "revenue": 500,
                  "production_companies": [ { "name": "A24" } ],
                  "release_dates": { "results": [
                    { "iso_3166_1": "TR", "release_dates": [
                      { "certification": "18+", "type": 4 },
                      { "certification": "13+", "type": 3 }
                    ] },
                    { "iso_3166_1": "US", "release_dates": [ { "certification": "", "type": 3 } ] }
                  ] }
                }
                """)
        });
        var apiClient = new TmdbApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") },
            Options.Create(new TmdbOptions { ApiKey = "test-api-key", CanonicalLanguage = "en-US" }),
            NullLogger<TmdbApiClient>.Instance);

        var extras = (await new TmdbMovieDataProvider(apiClient).GetMovieAsync("tmdb-1"))!.Extras!;

        Assert.Equal("Tag", extras.Tagline);
        Assert.Equal("13+", extras.Certifications["TR"]);
        Assert.False(extras.Certifications.ContainsKey("US"));
        Assert.Equal(["A24"], extras.ProductionCompanies);
        Assert.Null(extras.Budget);
        Assert.Equal(500, extras.Revenue);
        Assert.Contains("release_dates", handler.Requests.Single().RequestUri!.Query, StringComparison.Ordinal);
    }
}

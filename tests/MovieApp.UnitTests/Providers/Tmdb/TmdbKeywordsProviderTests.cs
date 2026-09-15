using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbKeywordsProviderTests
{
    [Fact]
    public async Task GetMovieKeywordsAsyncMapsMovieEnvelope()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": 157336,
                  "keywords": [
                    { "id": 42, "name": "time travel" },
                    { "id": 0, "name": "invalid" }
                  ]
                }
                """)
        });

        var provider = CreateProvider(handler);
        var keywords = await provider.GetMovieKeywordsAsync(157336);

        Assert.Single(keywords);
        Assert.Equal(42, keywords[0].TmdbKeywordId);
        Assert.Equal("time travel", keywords[0].Name);
        Assert.Contains("movie/157336/keywords", handler.Requests.Single().RequestUri?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTvShowKeywordsAsyncMapsTvResultsEnvelope()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": 1396,
                  "results": [
                    { "id": 9715, "name": "drug dealer" }
                  ]
                }
                """)
        });

        var provider = CreateProvider(handler);
        var keywords = await provider.GetTvShowKeywordsAsync(1396);

        Assert.Single(keywords);
        Assert.Equal(9715, keywords[0].TmdbKeywordId);
        Assert.Contains("tv/1396/keywords", handler.Requests.Single().RequestUri?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMovieKeywordsAsyncPropagatesCancellation()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "id": 1, "keywords": [] }""")
        });

        var provider = CreateProvider(handler);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider.GetMovieKeywordsAsync(1, cts.Token));
    }

    private static TmdbKeywordsProvider CreateProvider(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };

        var apiClient = new TmdbApiClient(
            httpClient,
            Options.Create(new TmdbOptions { ApiKey = "test-api-key" }),
            NullLogger<TmdbApiClient>.Instance);

        return new TmdbKeywordsProvider(apiClient);
    }
}

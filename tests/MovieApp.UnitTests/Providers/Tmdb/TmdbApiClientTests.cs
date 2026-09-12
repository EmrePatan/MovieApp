using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbApiClientTests
{
    [Fact]
    public async Task GetAsyncUsesBearerTokenWhenConfigured()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"results":[]}""")
        });

        var client = CreateClient(handler, new TmdbOptions
        {
            BaseUrl = "https://api.themoviedb.org/3/",
            ReadAccessToken = "test-read-access-token"
        });

        await client.GetAsync<TestResponse>("search/movie?query=test");

        var request = handler.Requests.Single();
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("test-read-access-token", request.Headers.Authorization?.Parameter);
        Assert.DoesNotContain("api_key", request.RequestUri?.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsyncUsesApiKeyQueryWhenBearerTokenMissing()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"results":[]}""")
        });

        var client = CreateClient(handler, new TmdbOptions
        {
            BaseUrl = "https://api.themoviedb.org/3/",
            ApiKey = "test-api-key"
        });

        await client.GetAsync<TestResponse>("search/movie?query=test");

        Assert.Contains("api_key=test-api-key", handler.Requests.Single().RequestUri?.Query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task GetAsyncMapsClientErrorsWithoutRetry(HttpStatusCode statusCode)
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(statusCode));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<TmdbApiException>(() =>
            client.GetAsync<TestResponse>("movie/1"));

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetAsyncRetriesTooManyRequestsAndEventuallySucceeds()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":1}""")
        });

        var client = CreateClient(handler);
        var response = await client.GetAsync<TestResponse>("movie/1");

        Assert.NotNull(response);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetAsyncRetriesServerErrorsWithBoundedAttempts()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<TmdbApiException>(() =>
            client.GetAsync<TestResponse>("movie/1"));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task GetAsyncThrowsWhenResponseBodyIsInvalidJson()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json")
        });

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<JsonException>(() =>
            client.GetAsync<TestResponse>("movie/1"));
    }

    [Fact]
    public async Task GetAsyncMapsConnectionFailuresToServiceUnavailable()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("connection refused"));

        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<TmdbApiException>(() =>
            client.GetAsync<TestResponse>("movie/1"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetAsyncMapsTimeoutToServiceUnavailable()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueException(new TaskCanceledException("timed out"));

        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<TmdbApiException>(() =>
            client.GetAsync<TestResponse>("movie/1"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetAsyncHonorsCancellationToken()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueException(new TaskCanceledException("cancelled"));

        var client = CreateClient(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetAsync<TestResponse>("movie/1", cts.Token));
    }

    private static TmdbApiClient CreateClient(
        MockHttpMessageHandler handler,
        TmdbOptions? options = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };

        return new TmdbApiClient(
            httpClient,
            Options.Create(options ?? new TmdbOptions { ApiKey = "test-api-key" }));
    }

    private sealed class TestResponse
    {
        public int Id { get; set; }
    }
}

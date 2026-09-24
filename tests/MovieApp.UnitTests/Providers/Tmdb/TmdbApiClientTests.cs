using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public async Task GetAsyncDoesNotRetryWhenRetryAfterExceedsRemainingBudget()
    {
        var handler = new MockHttpMessageHandler();
        var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        throttled.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60));
        handler.EnqueueResponse(throttled);

        var client = CreateClient(handler, new TmdbOptions { ApiKey = "test-api-key", RequestBudgetSeconds = 5 });

        var exception = await Assert.ThrowsAsync<TmdbApiException>(() =>
            client.GetAsync<TestResponse>("movie/1"));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetAsyncTimesOutStalledAttemptAsTransientFailure()
    {
        var client = CreateClient(
            new StallingHttpMessageHandler(),
            new TmdbOptions { ApiKey = "test-api-key", RequestTimeoutSeconds = 1, RequestBudgetSeconds = 5 });
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var exception = await Assert.ThrowsAsync<TmdbApiException>(() =>
            client.GetAsync<TestResponse>("movie/1"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(4), $"elapsed={stopwatch.Elapsed}");
    }

    [Fact]
    public async Task GetAsyncPropagatesCallerCancellationDuringStalledAttempt()
    {
        var client = CreateClient(
            new StallingHttpMessageHandler(),
            new TmdbOptions { ApiKey = "test-api-key", RequestTimeoutSeconds = 10, RequestBudgetSeconds = 20 });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetAsync<TestResponse>("movie/1", cts.Token));

        Assert.IsNotType<TmdbApiException>(exception);
    }

    private static TmdbApiClient CreateClient(
        HttpMessageHandler handler,
        TmdbOptions? options = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };

        return new TmdbApiClient(
            httpClient,
            Options.Create(options ?? new TmdbOptions { ApiKey = "test-api-key" }),
            NullLogger<TmdbApiClient>.Instance);
    }

    private sealed class TestResponse
    {
        public int Id { get; set; }
    }

    private sealed class StallingHttpMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

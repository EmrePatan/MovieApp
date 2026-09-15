using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbApiClientLoggingTests
{
    [Fact]
    public async Task GetAsync_LogsFailureWithoutQueryStringInPath()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.NotFound));
        var logger = new TestLogger<TmdbApiClient>();
        var client = CreateClient(handler, logger);

        await Assert.ThrowsAsync<TmdbApiException>(() =>
            client.GetAsync<TestResponse>("search/movie?query=secret&api_key=hidden"));

        var failureLog = Assert.Single(logger.Messages.Where(message => message.Contains("TMDB request failed", StringComparison.Ordinal)));
        Assert.Contains("path=search/movie", failureLog, StringComparison.Ordinal);
        Assert.DoesNotContain("api_key", failureLog, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", failureLog, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_LogsRetrySchedulingForTransientFailures()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":1}""")
        });

        var logger = new TestLogger<TmdbApiClient>();
        var client = CreateClient(handler, logger);

        var response = await client.GetAsync<TestResponse>("movie/1");

        Assert.NotNull(response);
        Assert.Contains(logger.Messages, message => message.Contains("retry scheduled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SanitizePathForLogging_StripsQueryString()
    {
        Assert.Equal("movie/1", TmdbApiClient.SanitizePathForLogging("movie/1?api_key=test"));
    }

    private static TmdbApiClient CreateClient(MockHttpMessageHandler handler, ILogger<TmdbApiClient> logger)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };

        return new TmdbApiClient(
            httpClient,
            Options.Create(new TmdbOptions { ApiKey = "test-api-key" }),
            logger);
    }

    private sealed class TestResponse
    {
        public int Id { get; set; }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}

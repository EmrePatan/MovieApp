using System.Net;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Models.PushNotifications;
using MovieApp.Infrastructure.PushNotifications;
using MovieApp.UnitTests.Providers.Tmdb;

namespace MovieApp.UnitTests.PushNotifications;

public sealed class ExpoPushClientLoggingTests
{
    [Fact]
    public async Task SendAsync_LogsHttpFailureWithoutTokens()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var logger = new TestLogger<ExpoPushClient>();
        var client = CreateClient(handler, logger);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SendAsync(
                [
                    new PushNotificationMessage(
                        Guid.NewGuid(),
                        "ExponentPushToken[abcdefghijklmnopqrstuvwxyz123456]",
                        "Title",
                        "Body",
                        new Dictionary<string, string>())
                ],
                CancellationToken.None));

        var failureLog = Assert.Single(
            logger.Messages,
            message => message.Contains("Expo push send HTTP request failed", StringComparison.Ordinal));
        Assert.Contains("503", failureLog, StringComparison.Ordinal);
        Assert.DoesNotContain("ExponentPushToken", failureLog, StringComparison.Ordinal);
    }

    private static ExpoPushClient CreateClient(MockHttpMessageHandler handler, ILogger<ExpoPushClient> logger)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://exp.host/--/api/v2/")
        };

        return new ExpoPushClient(httpClient, logger);
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

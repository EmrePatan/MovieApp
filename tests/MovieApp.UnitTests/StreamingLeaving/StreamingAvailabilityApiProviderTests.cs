using System.Net;
using System.Text;
using MovieApp.Application.Models.StreamingLeaving;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Providers.StreamingAvailability;

namespace MovieApp.UnitTests.StreamingLeaving;

public sealed class StreamingAvailabilityApiProviderTests
{
    [Fact]
    public async Task OnlySubscriptionServicesAreReturned()
    {
        var provider = Provider(HttpStatusCode.OK, """
            {"countryCode":"tr","services":[
              {"id":"netflix","name":"Netflix","streamingOptionTypes":{"subscription":true}},
              {"id":"apple","name":"Apple TV","streamingOptionTypes":{"subscription":false,"rent":true}}]}
            """, out _);

        var services = await provider.GetSubscriptionServicesAsync("TR");

        Assert.Equal([new StreamingService("netflix", "Netflix")], services);
    }

    [Fact]
    public async Task AnUnknownCountryHasNoServices()
    {
        var provider = Provider(HttpStatusCode.NotFound, "{}", out _);

        Assert.Empty(await provider.GetSubscriptionServicesAsync("XK"));
    }

    [Fact]
    public async Task OtherErrorsAreThrown()
    {
        var provider = Provider(HttpStatusCode.TooManyRequests, "{}", out _);

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetSubscriptionServicesAsync("TR"));
    }

    [Fact]
    public async Task ChangesAreReadWithTmdbIdsAndUnusableRowsAreSkipped()
    {
        var provider = Provider(HttpStatusCode.OK, """
            {"changes":[
              {"showId":"1","service":{"id":"netflix"},"streamingOptionType":"subscription","timestamp":1795737600,"link":"https://example.test/a"},
              {"showId":"2","service":{"id":"prime"},"streamingOptionType":"subscription","timestamp":1795737600},
              {"showId":"1","service":{"id":"prime"},"streamingOptionType":"addon","timestamp":1795737600},
              {"showId":"1","service":{"id":"hbo"},"streamingOptionType":"subscription"},
              {"showId":"3","service":{"id":"hbo"},"streamingOptionType":"subscription","timestamp":1795737600}],
             "shows":{"1":{"tmdbId":"movie/654"},"2":{"tmdbId":"tv/83054"},"3":{}},
             "hasMore":true,"nextCursor":"abc"}
            """, out var handler);

        var page = await provider.GetExpiringAsync("TR", ["netflix", "prime"], "x:y");

        Assert.Equal("abc", page.NextCursor);
        Assert.Equal(
            [
                new StreamingTitleKey(CatalogContentType.Movie, 654),
                new StreamingTitleKey(CatalogContentType.Tv, 83054)
            ],
            page.Changes.Select(change => change.Title));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1795737600).UtcDateTime, page.Changes[0].LeavesAtUtc);

        var query = handler.LastUri!.Query;
        Assert.Contains("country=tr", query, StringComparison.Ordinal);
        Assert.Contains("change_type=expiring", query, StringComparison.Ordinal);
        Assert.Contains("catalogs=netflix.subscription%2Cprime.subscription", query, StringComparison.Ordinal);
        Assert.Contains("cursor=x%3Ay", query, StringComparison.Ordinal);
    }

    private static StreamingAvailabilityApiProvider Provider(
        HttpStatusCode status,
        string body,
        out StubHandler handler)
    {
        handler = new StubHandler(status, body);
        return new StreamingAvailabilityApiProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v4/") });
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}

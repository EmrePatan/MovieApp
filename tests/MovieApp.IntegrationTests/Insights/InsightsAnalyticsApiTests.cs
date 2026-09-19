using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MovieApp.Contracts.Auth;
using MovieApp.Contracts.Insights;
using MovieApp.Contracts.Movies;
using MovieApp.Contracts.Ratings;
using MovieApp.IntegrationTests.Auth;
using MovieApp.IntegrationTests.UserProfile;

namespace MovieApp.IntegrationTests.Insights;

[Collection("UserProfileApi")]
public sealed class InsightsAnalyticsApiTests(UserProfileApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetAnalyticsRequiresAuthentication()
    {
        var response = await _client.GetAsync("/api/insights/analytics?timeZone=UTC");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAnalyticsRejectsInvalidTimezone()
    {
        await fixture.ResetAsync();
        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedGetAsync("/api/insights/analytics?timeZone=Invalid/Zone", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAnalyticsReturnsStructuredSectionsForAuthenticatedUser()
    {
        await fixture.ResetAsync();
        var token = await RegisterAndGetTokenAsync();
        var movieId = await SeedMovieAsync();
        await SendAuthorizedPostAsync($"/api/watch-history/movies/{movieId}", token);
        await SendAuthorizedPostAsync(
            $"/api/ratings/movies/{movieId}",
            token,
            new CreateRatingRequest(8));

        var response = await SendAuthorizedGetAsync("/api/insights/analytics?timeZone=UTC", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var analytics = await response.Content.ReadFromJsonAsync<InsightsAnalyticsResponse>();
        Assert.NotNull(analytics);
        Assert.Equal(364, analytics.Activity.Days.Count);
        Assert.NotNull(analytics.Taste);
        Assert.NotNull(analytics.Eras);
        Assert.NotNull(analytics.EstimatedTimeWatched);
        Assert.Equal(1, analytics.Ratings.RatingCount);
        Assert.NotEmpty(analytics.Milestones);
        Assert.True(analytics.GeneratedAtUtc <= DateTime.UtcNow);
    }

    private Task<string> RegisterAndGetTokenAsync() =>
        AuthIntegrationHelpers.RegisterVerifyAndGetAccessTokenAsync(
            _client,
            fixture.Factory.Services,
            $"insights-analytics-{Guid.NewGuid():N}@example.com");

    private async Task<Guid> SeedMovieAsync()
    {
        var response = await _client.GetAsync("/api/movies/search?q=Interstellar");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<MovieSearchResponse>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Items);
        return payload.Items[0].Id;
    }

    private Task<HttpResponseMessage> SendAuthorizedGetAsync(string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SendAuthorizedPostAsync(string path, string token, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return _client.SendAsync(request);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MovieApp.Contracts.Auth;
using MovieApp.Contracts.Insights;
using MovieApp.Contracts.Movies;
using MovieApp.IntegrationTests.UserProfile;

namespace MovieApp.IntegrationTests.Insights;

[Collection("UserProfileApi")]
public sealed class InsightsV3ApiTests(UserProfileApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetV3RequiresAuthentication()
    {
        var response = await _client.GetAsync("/api/insights/v3?timeZone=UTC");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetV3ReturnsStructuredSectionsForAuthenticatedUser()
    {
        await fixture.ResetAsync();
        var token = await RegisterAndGetTokenAsync();
        var movieId = await SeedMovieAsync();
        await SendAuthorizedPostAsync($"/api/watch-history/movies/{movieId}", token);
        await SendAuthorizedPostAsync(
            $"/api/ratings/movies/{movieId}",
            token,
            new MovieApp.Contracts.Ratings.CreateRatingRequest(8));

        var response = await SendAuthorizedGetAsync("/api/insights/v3?timeZone=UTC", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var insights = await response.Content.ReadFromJsonAsync<InsightsV3Response>();
        Assert.NotNull(insights);
        Assert.Equal("UTC", insights.Meta.TimeZone);
        Assert.NotEmpty(insights.MovieDna.IdentityTitle);
        Assert.Equal(12, insights.YourYear.Months.Count);
        Assert.NotNull(insights.YourTaste);
        Assert.NotNull(insights.TimeInStories);
        Assert.Equal(1, insights.YourRatings.Count);
        Assert.NotNull(insights.YourEra);
        Assert.NotNull(insights.YourRecords);
        Assert.NotEmpty(insights.Achievements);
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest($"insights-v3-{Guid.NewGuid():N}@example.com", "StrongPassword123", "Insights User"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth.AccessToken;
    }

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

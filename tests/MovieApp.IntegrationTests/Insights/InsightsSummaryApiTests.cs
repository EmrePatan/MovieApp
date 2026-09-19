using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MovieApp.Contracts.Auth;
using MovieApp.Contracts.Insights;
using MovieApp.Contracts.Movies;
using MovieApp.Contracts.Ratings;
using MovieApp.Contracts.Users;
using MovieApp.IntegrationTests.Auth;
using MovieApp.IntegrationTests.UserProfile;

namespace MovieApp.IntegrationTests.Insights;

[Collection("UserProfileApi")]
public sealed class InsightsSummaryApiTests(UserProfileApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetSummaryRequiresAuthentication()
    {
        var response = await _client.GetAsync("/api/insights/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSummaryReturnsMemberSinceAndCountsForAuthenticatedUser()
    {
        await fixture.ResetAsync();

        var email = $"insights-{Guid.NewGuid():N}@example.com";
        var token = await RegisterAndGetTokenAsync(email);
        var profileResponse = await SendAuthorizedGetAsync("/api/users/me", token);
        var profile = await profileResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);

        var movieId = await SeedMovieAsync();
        await SendAuthorizedPostAsync($"/api/watch-history/movies/{movieId}", token);
        await SendAuthorizedPostAsync(
            $"/api/ratings/movies/{movieId}",
            token,
            new CreateRatingRequest(8));

        var response = await SendAuthorizedGetAsync("/api/insights/summary?timeZone=Europe/Istanbul", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<InsightsSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(profile.CreatedAt, summary.MemberSince);
        Assert.Equal(1, summary.Summary.MoviesWatched);
        Assert.Equal(1, summary.Summary.RatingsCount);
        Assert.Equal(4.0m, summary.Summary.AverageStarRating);
        Assert.Equal(1, summary.WatchingMix.MovieTitleCount);
        Assert.Equal(0, summary.WatchingMix.SeriesTitleCount);
        Assert.True(summary.GeneratedAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public async Task GetSummaryAcceptsMissingTimezoneLikeStatisticsEndpoint()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var response = await SendAuthorizedGetAsync("/api/insights/summary", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<string> RegisterAndGetTokenAsync(string? email = null) =>
        AuthIntegrationHelpers.RegisterVerifyAndGetAccessTokenAsync(
            _client,
            fixture.Factory.Services,
            email ?? $"insights-{Guid.NewGuid():N}@example.com");

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

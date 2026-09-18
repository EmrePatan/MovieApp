using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MovieApp.Contracts.Auth;
using MovieApp.Contracts.Movies;
using MovieApp.Contracts.Ratings;
using MovieApp.Contracts.Reviews;
using MovieApp.Contracts.TvShows;

namespace MovieApp.IntegrationTests.RatingsReviews;

[CollectionDefinition("RatingsReviewsApi")]
public sealed class RatingsReviewsApiTestsFixture : ICollectionFixture<RatingsReviewsApiFixture>;

[Collection("RatingsReviewsApi")]
public sealed class RatingsReviewsApiTests(RatingsReviewsApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task UserCanRateReviewAndReadPublicAggregates()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("ratings-user");
        var movieId = await SeedMovieAsync();
        var tvShowId = await SeedTvShowAsync();

        var createRatingResponse = await SendAuthorizedPostAsync(
            $"/api/ratings/movies/{movieId}",
            token,
            new CreateRatingRequest(9));

        Assert.Equal(HttpStatusCode.Created, createRatingResponse.StatusCode);

        var updateRatingResponse = await SendAuthorizedPostAsync(
            $"/api/ratings/movies/{movieId}",
            token,
            new CreateRatingRequest(8));

        Assert.Equal(HttpStatusCode.OK, updateRatingResponse.StatusCode);

        var publicSummaryResponse = await _client.GetAsync($"/api/ratings/movies/{movieId}");
        Assert.Equal(HttpStatusCode.OK, publicSummaryResponse.StatusCode);

        var summary = await publicSummaryResponse.Content.ReadFromJsonAsync<RatingSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(1, summary.RatingCount);
        Assert.Equal(8m, summary.AverageScore);
        Assert.Equal(1, summary.ScoreDistribution[8]);

        var createReviewResponse = await SendAuthorizedPostAsync(
            $"/api/reviews/movies/{movieId}",
            token,
            new CreateReviewRequest("Excellent movie."));

        Assert.Equal(HttpStatusCode.Created, createReviewResponse.StatusCode);

        var duplicateReviewResponse = await SendAuthorizedPostAsync(
            $"/api/reviews/movies/{movieId}",
            token,
            new CreateReviewRequest("Duplicate"));

        Assert.Equal(HttpStatusCode.Conflict, duplicateReviewResponse.StatusCode);

        var publicReviewsResponse = await _client.GetAsync($"/api/reviews/movies/{movieId}?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, publicReviewsResponse.StatusCode);

        var reviews = await publicReviewsResponse.Content.ReadFromJsonAsync<ReviewListResponse>();
        Assert.NotNull(reviews);
        Assert.Single(reviews.Items);
        Assert.Equal("Excellent movie.", reviews.Items[0].Content);
        Assert.Equal(8, reviews.Items[0].UserRating);

        var tvRatingResponse = await SendAuthorizedPostAsync(
            $"/api/ratings/tvshows/{tvShowId}",
            token,
            new CreateRatingRequest(7));

        Assert.Equal(HttpStatusCode.Created, tvRatingResponse.StatusCode);

        var meRatingResponse = await SendAuthorizedGetAsync($"/api/ratings/tvshows/{tvShowId}/me", token);
        Assert.Equal(HttpStatusCode.OK, meRatingResponse.StatusCode);
    }

    [Fact]
    public async Task PublicReviewsCanFilterByRatingStars()
    {
        await fixture.ResetAsync();

        var highRaterToken = await RegisterAndGetTokenAsync("high-rater");
        var lowRaterToken = await RegisterAndGetTokenAsync("low-rater");
        var movieId = await SeedMovieAsync();

        await SendAuthorizedPostAsync(
            $"/api/ratings/movies/{movieId}",
            highRaterToken,
            new CreateRatingRequest(8));
        await SendAuthorizedPostAsync(
            $"/api/reviews/movies/{movieId}",
            highRaterToken,
            new CreateReviewRequest("Loved it."));

        await SendAuthorizedPostAsync(
            $"/api/ratings/movies/{movieId}",
            lowRaterToken,
            new CreateRatingRequest(3));
        await SendAuthorizedPostAsync(
            $"/api/reviews/movies/{movieId}",
            lowRaterToken,
            new CreateReviewRequest("Not for me."));

        var filteredResponse = await _client.GetAsync(
            $"/api/reviews/movies/{movieId}?page=1&pageSize=20&ratingStars=4");
        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);

        var filteredReviews = await filteredResponse.Content.ReadFromJsonAsync<ReviewListResponse>();
        Assert.NotNull(filteredReviews);
        Assert.Single(filteredReviews.Items);
        Assert.Equal("Loved it.", filteredReviews.Items[0].Content);
        Assert.Equal(8, filteredReviews.Items[0].UserRating);
    }

    [Fact]
    public async Task UserBCannotUpdateUserAReview()
    {
        await fixture.ResetAsync();

        var userAToken = await RegisterAndGetTokenAsync("review-owner");
        var userBToken = await RegisterAndGetTokenAsync("review-intruder");
        var movieId = await SeedMovieAsync();

        await SendAuthorizedPostAsync(
            $"/api/reviews/movies/{movieId}",
            userAToken,
            new CreateReviewRequest("Owner review"));

        var updateResponse = await SendAuthorizedPutAsync(
            $"/api/reviews/movies/{movieId}",
            userBToken,
            new UpdateReviewRequest("Hacked review"));

        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
    }

    [Fact]
    public async Task AnonymousUserCannotAccessProtectedRatingEndpoints()
    {
        await fixture.ResetAsync();

        var movieId = await SeedMovieAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/ratings/movies/{movieId}",
            new CreateRatingRequest(5));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RatingNonexistentMovieReturnsNotFound()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("missing-movie");
        var response = await SendAuthorizedPostAsync(
            $"/api/ratings/movies/{Guid.NewGuid()}",
            token,
            new CreateRatingRequest(5));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExistingMovieSearchStillWorks()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/movies/search?q=interstellar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> RegisterAndGetTokenAsync(string prefix)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            $"{prefix}-{Guid.NewGuid():N}@example.com",
            "StrongPassword123",
            "Integration User"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(payload);
        return payload.AccessToken;
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

    private async Task<Guid> SeedTvShowAsync()
    {
        var response = await _client.GetAsync("/api/tvshows/search?q=breaking");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TvShowSearchResponse>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Items);
        return payload.Items[0].Id;
    }

    private Task<HttpResponseMessage> SendAuthorizedGetAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SendAuthorizedPostAsync(string url, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SendAuthorizedPutAsync(string url, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}

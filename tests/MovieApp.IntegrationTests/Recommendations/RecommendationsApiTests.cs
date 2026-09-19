using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MovieApp.Contracts.Auth;
using MovieApp.IntegrationTests.Auth;
using MovieApp.Contracts.Movies;
using MovieApp.Contracts.Ratings;
using MovieApp.Contracts.Recommendations;
using MovieApp.Contracts.TvShows;

namespace MovieApp.IntegrationTests.Recommendations;

[CollectionDefinition("RecommendationsApi")]
public sealed class RecommendationsApiTestsFixture : ICollectionFixture<RecommendationsApiFixture>;

[Collection("RecommendationsApi")]
public sealed class RecommendationsApiTests(RecommendationsApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task SimilarMoviesReturnsResultsForExistingMovie()
    {
        await fixture.ResetAsync();
        var movieId = await SeedMovieAsync();

        var response = await _client.GetAsync($"/api/recommendations/movies/{movieId}/similar?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RecommendationResponse>();
        Assert.NotNull(payload);
        Assert.All(payload.Items, item => Assert.NotEqual(movieId, item.Id));
    }

    [Fact]
    public async Task SimilarMoviesReturnsNotFoundForMissingMovie()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync($"/api/recommendations/movies/{Guid.NewGuid()}/similar");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SimilarTvShowsReturnsResultsForExistingTvShow()
    {
        await fixture.ResetAsync();
        var tvShowId = await SeedTvShowAsync();

        var response = await _client.GetAsync($"/api/recommendations/tvshows/{tvShowId}/similar?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RecommendationResponse>();
        Assert.NotNull(payload);
        Assert.All(payload.Items, item => Assert.NotEqual(tvShowId, item.Id));
    }

    [Fact]
    public async Task PersonalizedRecommendationsRequireAuthentication()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/recommendations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ColdStartUserReceivesPopularRecommendations()
    {
        await fixture.ResetAsync();
        await SeedCatalogAsync();
        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedGetAsync("/api/recommendations?page=1&pageSize=20&type=all", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RecommendationResponse>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Items);
        Assert.Contains(payload.Items, item => item.Reason == "Popular right now");
    }

    [Fact]
    public async Task PersonalizedUserReceivesRecommendationsAfterInteractions()
    {
        await fixture.ResetAsync();
        await SeedPagedMovieCatalogAsync();
        var movieId = await SeedMovieAsync();
        var tvShowId = await SeedTvShowAsync();
        var token = await RegisterAndGetTokenAsync();

        var watchlistResponse = await SendAuthorizedPostAsync(
            "/api/watchlists",
            token,
            new MovieApp.Contracts.Watchlists.CreateWatchlistRequest("Sci-Fi Queue"));

        var watchlist = await watchlistResponse.Content.ReadFromJsonAsync<MovieApp.Contracts.Watchlists.WatchlistSummaryResponse>();
        Assert.NotNull(watchlist);

        var catalogResponse = await _client.GetAsync("/api/movies/search?q=paged-catalog&page=1&pageSize=25");
        var catalog = await catalogResponse.Content.ReadFromJsonAsync<MovieSearchResponse>();
        Assert.NotNull(catalog);
        Assert.NotEmpty(catalog.Items);

        var catalogMovieId = catalog.Items[0].Id;

        await SendAuthorizedPostAsync($"/api/ratings/movies/{movieId}", token, new CreateRatingRequest(10));
        await SendAuthorizedPostAsync($"/api/favorites/tvshows/{tvShowId}", token);
        await SendAuthorizedPostAsync($"/api/watchlists/{watchlist.Id}/movies/{catalogMovieId}", token);

        var response = await SendAuthorizedGetAsync("/api/recommendations?page=1&pageSize=20&type=all", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RecommendationResponse>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Items);
        Assert.DoesNotContain(payload.Items, item => item.Id == movieId);
        Assert.DoesNotContain(payload.Items, item => item.Id == tvShowId);
        Assert.DoesNotContain(payload.Items, item => item.Id == catalogMovieId);
        Assert.DoesNotContain(payload.Items, item => item.Reason == "Popular right now");
    }

    [Fact]
    public async Task HomeRecommendationsReturnColdStartSectionsForNewUser()
    {
        await fixture.ResetAsync();
        await SeedCatalogAsync();
        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedGetAsync("/api/recommendations/home", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RecommendationHomeResponse>();
        Assert.NotNull(payload);
        Assert.Contains(payload.Sections, section => section.Key == "popular");
        Assert.Contains(payload.Sections, section => section.Key == "trending");
        Assert.Contains(payload.Sections, section => section.Key == "top-rated");
    }

    [Fact]
    public async Task HomeRecommendationsReturnPersonalizedSectionsForActiveUser()
    {
        await fixture.ResetAsync();
        await SeedCatalogAsync();
        await SeedPagedMovieCatalogAsync();
        var movieId = await SeedMovieAsync();
        var tvShowId = await SeedTvShowAsync();
        var secondMovieId = await SeedSecondMovieAsync();
        var token = await RegisterAndGetTokenAsync();

        await SendAuthorizedPostAsync($"/api/favorites/movies/{movieId}", token);
        await SendAuthorizedPostAsync($"/api/favorites/tvshows/{tvShowId}", token);
        await SendAuthorizedPostAsync($"/api/watch-history/movies/{secondMovieId}", token);

        var response = await SendAuthorizedGetAsync("/api/recommendations/home", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RecommendationHomeResponse>();
        Assert.NotNull(payload);
        Assert.Contains(payload.Sections, section => section.Key == "recommended-for-you");
        Assert.DoesNotContain(payload.Sections, section => section.Key == "similar-to-favorites");
    }

    [Fact]
    public async Task InvalidPaginationReturnsBadRequest()
    {
        await fixture.ResetAsync();
        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedGetAsync("/api/recommendations?page=0&pageSize=20", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidTypeReturnsBadRequest()
    {
        await fixture.ResetAsync();
        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedGetAsync("/api/recommendations?type=invalid", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExistingMovieSearchStillWorks()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/movies/search?q=interstellar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task SeedCatalogAsync()
    {
        await SeedMovieAsync();
        await SeedTvShowAsync();
    }

    private async Task SeedPagedMovieCatalogAsync()
    {
        var response = await _client.GetAsync("/api/movies/search?q=paged-catalog&page=1&pageSize=25");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

    private async Task<Guid> SeedSecondMovieAsync()
    {
        var response = await _client.GetAsync("/api/movies/search?q=paged-catalog&page=1&pageSize=25");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<MovieSearchResponse>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Items);
        return payload.Items[0].Id;
    }

    private Task<string> RegisterAndGetTokenAsync() =>
        AuthIntegrationHelpers.RegisterVerifyAndGetAccessTokenAsync(
            _client,
            fixture.Factory.Services,
            $"recommendations-{Guid.NewGuid():N}@example.com");

    private Task<HttpResponseMessage> SendAuthorizedGetAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SendAuthorizedPostAsync(string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return _client.SendAsync(request);
    }
}

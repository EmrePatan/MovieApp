using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MovieApp.Contracts.Auth;
using MovieApp.Contracts.Home;
using MovieApp.Contracts.Movies;
using MovieApp.Contracts.Ratings;
using MovieApp.Contracts.TvShows;

namespace MovieApp.IntegrationTests.Home;

[CollectionDefinition("HomeApi")]
public sealed class HomeApiTestsFixture : ICollectionFixture<HomeApiFixture>;

[Collection("HomeApi")]
public sealed class HomeApiTests(HomeApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task HomeRequiresAuthentication()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/home");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ColdStartUserReceivesDiscoverySections()
    {
        await fixture.ResetAsync();
        await SeedCatalogAsync();
        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedGetAsync("/api/home", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HomeResponse>();
        Assert.NotNull(payload);
        Assert.False(payload.IsPersonalized);
        Assert.Contains(payload.Sections, section => section.Type == "Trending");
        Assert.Contains(payload.Sections, section => section.Type == "Popular");
        Assert.Contains(payload.Sections, section => section.Type == "NewReleases");
        Assert.Contains(payload.Sections, section => section.Type == "TopRated");
        Assert.DoesNotContain(payload.Sections, section => section.Type == "RecommendedForYou");
    }

    [Fact]
    public async Task PersonalizedUserReceivesRecommendationSections()
    {
        await fixture.ResetAsync();
        var movieId = await SeedMovieAsync();
        var token = await RegisterAndGetTokenAsync();

        await SendAuthorizedPostAsync($"/api/favorites/movies/{movieId}", token);
        await SendAuthorizedPostAsync($"/api/watch-history/movies/{movieId}", token);
        await SendAuthorizedPostAsync($"/api/ratings/movies/{movieId}", token, new CreateRatingRequest(9));

        var response = await SendAuthorizedGetAsync("/api/home", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HomeResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.IsPersonalized);
        Assert.Contains(payload.Sections, section => section.Type == "RecommendedForYou");
    }

    [Fact]
    public async Task HomeMovieTypeFilterReturnsOnlyMovies()
    {
        await fixture.ResetAsync();
        await SeedCatalogAsync();
        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedGetAsync("/api/home?type=movie", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HomeResponse>();
        Assert.NotNull(payload);
        Assert.All(
            payload.Sections.SelectMany(section => section.Items),
            item => Assert.Equal("movie", item.ContentType));
    }

    [Fact]
    public async Task HomeTvTypeFilterReturnsOnlyTvShows()
    {
        await fixture.ResetAsync();
        await SeedCatalogAsync();
        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedGetAsync("/api/home?type=tv", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HomeResponse>();
        Assert.NotNull(payload);
        Assert.All(
            payload.Sections.SelectMany(section => section.Items),
            item => Assert.Equal("tv", item.ContentType));
    }

    [Fact]
    public async Task HomeSectionSizeIsRespected()
    {
        await fixture.ResetAsync();
        await SeedCatalogAsync();
        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedGetAsync("/api/home?sectionSize=5", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HomeResponse>();
        Assert.NotNull(payload);
        Assert.All(payload.Sections, section => Assert.True(section.Items.Count <= 5));
    }

    [Fact]
    public async Task HomeSectionSizeAboveMaximumReturnsBadRequest()
    {
        await fixture.ResetAsync();
        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedGetAsync("/api/home?sectionSize=21", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HomeInvalidTypeReturnsBadRequest()
    {
        await fixture.ResetAsync();
        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedGetAsync("/api/home?type=invalid", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ContinueWatchingIncludesPartiallyWatchedTvShow()
    {
        await fixture.ResetAsync();
        var token = await RegisterAndGetTokenAsync();
        var tvShowId = await SeedTvShowAsync();
        var episodeId = await SeedEpisodeAsync(tvShowId, 1, 1);

        await SendAuthorizedPostAsync($"/api/watch-history/episodes/{episodeId}", token);

        var response = await SendAuthorizedGetAsync("/api/home?type=tv", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HomeResponse>();
        Assert.NotNull(payload);

        var continueWatching = payload.Sections.SingleOrDefault(section => section.Type == "ContinueWatching");
        Assert.NotNull(continueWatching);
        Assert.Contains(continueWatching.Items, item => item.Id == tvShowId);
    }

    [Fact]
    public async Task HomeResponsesAreIsolatedPerUser()
    {
        await fixture.ResetAsync();
        var movieId = await SeedMovieAsync();
        var tokenA = await RegisterAndGetTokenAsync();
        var tokenB = await RegisterAndGetTokenAsync();

        await SendAuthorizedPostAsync($"/api/favorites/movies/{movieId}", tokenA);
        await SendAuthorizedPostAsync($"/api/watch-history/movies/{movieId}", tokenA);
        await SendAuthorizedPostAsync($"/api/ratings/movies/{movieId}", tokenA, new CreateRatingRequest(9));

        var personalizedResponse = await SendAuthorizedGetAsync("/api/home", tokenA);
        var coldStartResponse = await SendAuthorizedGetAsync("/api/home", tokenB);

        var personalizedPayload = await personalizedResponse.Content.ReadFromJsonAsync<HomeResponse>();
        var coldStartPayload = await coldStartResponse.Content.ReadFromJsonAsync<HomeResponse>();

        Assert.NotNull(personalizedPayload);
        Assert.NotNull(coldStartPayload);
        Assert.True(personalizedPayload.IsPersonalized);
        Assert.False(coldStartPayload.IsPersonalized);
    }

    [Fact]
    public async Task ExistingRecommendationEndpointStillWorks()
    {
        await fixture.ResetAsync();
        await SeedCatalogAsync();
        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedGetAsync("/api/recommendations/home", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task SeedCatalogAsync()
    {
        await SeedMovieAsync();
        await SeedTvShowAsync();
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

    private async Task<Guid> SeedEpisodeAsync(Guid tvShowId, int seasonNumber, int episodeNumber)
    {
        var response = await _client.GetAsync(
            $"/api/tvshows/{tvShowId}/seasons/{seasonNumber}/episodes/{episodeNumber}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<EpisodeResponse>();
        Assert.NotNull(payload);
        return payload.Id;
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            $"home-{Guid.NewGuid():N}@example.com",
            "StrongPassword123",
            "Integration User"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(payload);
        return payload.AccessToken;
    }

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

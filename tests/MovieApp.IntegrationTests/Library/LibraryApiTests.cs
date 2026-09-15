using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MovieApp.Contracts.Auth;
using MovieApp.Contracts.Favorites;
using MovieApp.Contracts.Library;
using MovieApp.Contracts.Movies;
using MovieApp.Contracts.TvShows;
using MovieApp.Contracts.Watchlists;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.IntegrationTests.Library;

[CollectionDefinition("LibraryApi")]
public sealed class LibraryApiTestsFixture : ICollectionFixture<Home.HomeApiFixture>;

[Collection("LibraryApi")]
public sealed class LibraryApiTests(Home.HomeApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task LibraryRequiresAuthentication()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/library");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DefaultCategoryIsWatching()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var tvShowId = await SeedTvShowAsync();
        var episodeId = await SeedEpisodeAsync(tvShowId, 1, 1);
        await SendAuthorizedPostAsync($"/api/watch-history/episodes/{episodeId}", token);

        var response = await SendAuthorizedGetAsync("/api/library?page=1&pageSize=24", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LibraryListResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal("watching", payload.Items[0].CollectionStatus);
        Assert.Equal("tv", payload.Items[0].Type);
    }

    [Fact]
    public async Task WatchingExcludesMovies()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var movieId = await SeedMovieAsync();
        await SendAuthorizedPostAsync($"/api/watch-history/movies/{movieId}", token);

        var response = await SendAuthorizedGetAsync("/api/library?category=watching&mediaType=all", token);
        var payload = await response.Content.ReadFromJsonAsync<LibraryListResponse>();

        Assert.NotNull(payload);
        Assert.Empty(payload.Items);
    }

    [Fact]
    public async Task WatchingIncludesPartiallyWatchedTvShowWithProgress()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var tvShowId = await SeedTvShowAsync();
        await _client.GetAsync($"/api/tvshows/{tvShowId}/seasons/1");
        var episode1 = await SeedEpisodeAsync(tvShowId, 1, 1);
        var episode2 = await SeedEpisodeAsync(tvShowId, 1, 2);

        await SendAuthorizedPostAsync($"/api/watch-history/episodes/{episode1}", token);

        var response = await SendAuthorizedGetAsync("/api/library?category=watching&mediaType=tv", token);
        var payload = await response.Content.ReadFromJsonAsync<LibraryListResponse>();

        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal(tvShowId, payload.Items[0].Id);
        Assert.Equal("watching", payload.Items[0].CollectionStatus);
        Assert.NotNull(payload.Items[0].ProgressPercentage);
        Assert.True(payload.Items[0].ProgressPercentage > 0);
        Assert.NotNull(payload.Items[0].NextEpisode);
        Assert.Equal(episode2, payload.Items[0].NextEpisode!.EpisodeId);
    }

    [Fact]
    public async Task CompletedTvShowAppearsInWatchedNotWatching()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var tvShowId = await SeedTvShowWithAllEpisodesAsync();

        var watchStateResponse = await SendAuthorizedPostJsonAsync(
            $"/api/watch-history/tvshows/{tvShowId}/watch-state",
            token,
            new MovieApp.Contracts.WatchHistory.SetWatchStateRequest(true));

        Assert.Equal(HttpStatusCode.OK, watchStateResponse.StatusCode);

        var watchingResponse = await SendAuthorizedGetAsync("/api/library?category=watching", token);
        var watchingPayload = await watchingResponse.Content.ReadFromJsonAsync<LibraryListResponse>();
        Assert.NotNull(watchingPayload);
        Assert.DoesNotContain(watchingPayload.Items, item => item.Id == tvShowId);

        var watchedResponse = await SendAuthorizedGetAsync("/api/library?category=watched&mediaType=tv", token);
        var watchedPayload = await watchedResponse.Content.ReadFromJsonAsync<LibraryListResponse>();
        Assert.NotNull(watchedPayload);
        Assert.Contains(watchedPayload.Items, item => item.Id == tvShowId && item.CollectionStatus == "watched");
    }

    [Fact]
    public async Task WatchedMoviesAppearInWatchedCategory()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var movieId = await SeedMovieAsync();
        await SendAuthorizedPostAsync($"/api/watch-history/movies/{movieId}", token);

        var response = await SendAuthorizedGetAsync("/api/library?category=watched&mediaType=movie", token);
        var payload = await response.Content.ReadFromJsonAsync<LibraryListResponse>();

        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal(movieId, payload.Items[0].Id);
        Assert.Equal("movie", payload.Items[0].Type);
        Assert.Equal("watched", payload.Items[0].CollectionStatus);
    }

    [Fact]
    public async Task LikedCategoryReturnsFavorites()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var movieId = await SeedMovieAsync();
        await SendAuthorizedPostAsync($"/api/favorites/movies/{movieId}", token);

        var response = await SendAuthorizedGetAsync("/api/library?category=liked", token);
        var payload = await response.Content.ReadFromJsonAsync<LibraryListResponse>();

        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal(movieId, payload.Items[0].Id);
        Assert.Equal("liked", payload.Items[0].CollectionStatus);
    }

    [Fact]
    public async Task WatchlistUnionsAndDedupesAcrossMultipleLists()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var movieId = await SeedMovieAsync();

        var firstList = await CreateWatchlistAsync(token, "List A");
        var secondList = await CreateWatchlistAsync(token, "List B");

        await SendAuthorizedPostAsync($"/api/watchlists/{firstList.Id}/movies/{movieId}", token);
        await SendAuthorizedPostAsync($"/api/watchlists/{secondList.Id}/movies/{movieId}", token);

        var response = await SendAuthorizedGetAsync("/api/library?category=watchlist&mediaType=movie", token);
        var payload = await response.Content.ReadFromJsonAsync<LibraryListResponse>();

        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal(movieId, payload.Items[0].Id);
        Assert.Equal("watchlist", payload.Items[0].CollectionStatus);
    }

    [Fact]
    public async Task MediaTypeMovieFilterExcludesTvShows()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var movieId = await SeedMovieAsync();
        var tvShowId = await SeedTvShowAsync();

        await SendAuthorizedPostAsync($"/api/favorites/movies/{movieId}", token);
        await SendAuthorizedPostAsync($"/api/favorites/tvshows/{tvShowId}", token);

        var response = await SendAuthorizedGetAsync("/api/library?category=liked&mediaType=movie", token);
        var payload = await response.Content.ReadFromJsonAsync<LibraryListResponse>();

        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal("movie", payload.Items[0].Type);
    }

    [Fact]
    public async Task PaginationIsStableWithoutOverlap()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var (firstMovieId, secondMovieId) = await SeedTwoDistinctMoviesAsync();

        await SendAuthorizedPostAsync($"/api/favorites/movies/{firstMovieId}", token);
        await SendAuthorizedPostAsync($"/api/favorites/movies/{secondMovieId}", token);

        var pageOneResponse = await SendAuthorizedGetAsync(
            "/api/library?category=liked&mediaType=movie&page=1&pageSize=1",
            token);
        var pageOne = await pageOneResponse.Content.ReadFromJsonAsync<LibraryListResponse>();

        var pageTwoResponse = await SendAuthorizedGetAsync(
            "/api/library?category=liked&mediaType=movie&page=2&pageSize=1",
            token);
        var pageTwo = await pageTwoResponse.Content.ReadFromJsonAsync<LibraryListResponse>();

        Assert.NotNull(pageOne);
        Assert.NotNull(pageTwo);
        Assert.Equal(2, pageOne.TotalCount);
        Assert.Single(pageOne.Items);
        Assert.Single(pageTwo.Items);
        Assert.NotEqual(pageOne.Items[0].Id, pageTwo.Items[0].Id);
        Assert.True(pageOne.HasNextPage);
        Assert.False(pageTwo.HasNextPage);
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            $"library-{Guid.NewGuid():N}@example.com",
            "StrongPassword123",
            "Library User"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(payload);
        return payload.AccessToken;
    }

    private async Task<Guid> SeedMovieAsync()
    {
        var response = await _client.GetAsync("/api/movies/search?q=Interstellar");
        var payload = await response.Content.ReadFromJsonAsync<MovieSearchResponse>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Items);
        return payload.Items[0].Id;
    }

    private async Task<(Guid FirstMovieId, Guid SecondMovieId)> SeedTwoDistinctMoviesAsync()
    {
        var response = await _client.GetAsync(
            $"/api/movies/search?q={FakeMovieDataProvider.PagedCatalogQueryToken}&page=1&pageSize=2");
        var payload = await response.Content.ReadFromJsonAsync<MovieSearchResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.Items.Count >= 2, "Expected at least two catalog movies for pagination coverage.");
        return (payload.Items[0].Id, payload.Items[1].Id);
    }

    private async Task<Guid> SeedTvShowAsync()
    {
        var response = await _client.GetAsync("/api/tvshows/search?q=breaking");
        var payload = await response.Content.ReadFromJsonAsync<TvShowSearchResponse>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Items);
        return payload.Items[0].Id;
    }

    private async Task<Guid> SeedTvShowWithAllEpisodesAsync()
    {
        var tvShowId = await SeedTvShowAsync();
        await _client.GetAsync($"/api/tvshows/{tvShowId}/seasons/1");
        await _client.GetAsync($"/api/tvshows/{tvShowId}/seasons/2");
        await _client.GetAsync($"/api/tvshows/{tvShowId}/seasons/3");
        return tvShowId;
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

    private async Task<WatchlistSummaryResponse> CreateWatchlistAsync(string token, string name)
    {
        var response = await SendAuthorizedPostJsonAsync(
            "/api/watchlists",
            token,
            new CreateWatchlistRequest(name));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var watchlist = await response.Content.ReadFromJsonAsync<WatchlistSummaryResponse>();
        Assert.NotNull(watchlist);
        return watchlist;
    }

    private Task<HttpResponseMessage> SendAuthorizedGetAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SendAuthorizedPostAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SendAuthorizedPostJsonAsync<TPayload>(
        string url,
        string token,
        TPayload payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}

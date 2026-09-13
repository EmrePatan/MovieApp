using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MovieApp.Contracts.Auth;
using MovieApp.Contracts.Favorites;
using MovieApp.Contracts.Movies;
using MovieApp.Contracts.TvShows;
using MovieApp.Contracts.Watchlists;

namespace MovieApp.IntegrationTests.FavoritesWatchlists;

[CollectionDefinition("FavoritesWatchlistsApi")]
public sealed class FavoritesWatchlistsApiTestsFixture : ICollectionFixture<FavoritesWatchlistsApiFixture>;

[Collection("FavoritesWatchlistsApi")]
public sealed class FavoritesWatchlistsApiTests(FavoritesWatchlistsApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task UserACanManageFavoritesAndWatchlists()
    {
        await fixture.ResetAsync();

        var userAToken = await RegisterAndGetTokenAsync("user-a");
        var movieId = await SeedMovieAsync();
        var tvShowId = await SeedTvShowAsync();

        var createWatchlistResponse = await SendAuthorizedPostAsync(
            "/api/watchlists",
            userAToken,
            new CreateWatchlistRequest("Weekend Watch"));

        Assert.Equal(HttpStatusCode.Created, createWatchlistResponse.StatusCode);

        var watchlist = await createWatchlistResponse.Content.ReadFromJsonAsync<WatchlistSummaryResponse>();
        Assert.NotNull(watchlist);

        var addMovieResponse = await SendAuthorizedPostAsync(
            $"/api/watchlists/{watchlist.Id}/movies/{movieId}",
            userAToken);

        Assert.Equal(HttpStatusCode.Created, addMovieResponse.StatusCode);

        var duplicateMovieResponse = await SendAuthorizedPostAsync(
            $"/api/watchlists/{watchlist.Id}/movies/{movieId}",
            userAToken);

        Assert.Equal(HttpStatusCode.OK, duplicateMovieResponse.StatusCode);

        var addTvShowResponse = await SendAuthorizedPostAsync(
            $"/api/watchlists/{watchlist.Id}/tvshows/{tvShowId}",
            userAToken);

        Assert.Equal(HttpStatusCode.Created, addTvShowResponse.StatusCode);

        var watchlistDetailResponse = await SendAuthorizedGetAsync(
            $"/api/watchlists/{watchlist.Id}",
            userAToken);

        Assert.Equal(HttpStatusCode.OK, watchlistDetailResponse.StatusCode);

        var watchlistDetail = await watchlistDetailResponse.Content.ReadFromJsonAsync<WatchlistDetailResponse>();
        Assert.NotNull(watchlistDetail);
        Assert.Single(watchlistDetail.Movies);
        Assert.Single(watchlistDetail.TvShows);

        var favoriteMovieResponse = await SendAuthorizedPostAsync(
            $"/api/favorites/movies/{movieId}",
            userAToken);

        Assert.Equal(HttpStatusCode.Created, favoriteMovieResponse.StatusCode);

        var duplicateFavoriteResponse = await SendAuthorizedPostAsync(
            $"/api/favorites/movies/{movieId}",
            userAToken);

        Assert.Equal(HttpStatusCode.OK, duplicateFavoriteResponse.StatusCode);

        var favoritesResponse = await SendAuthorizedGetAsync("/api/favorites?page=1&pageSize=20", userAToken);
        Assert.Equal(HttpStatusCode.OK, favoritesResponse.StatusCode);

        var favorites = await favoritesResponse.Content.ReadFromJsonAsync<FavoritesResponse>();
        Assert.NotNull(favorites);
        Assert.Single(favorites.Movies);
        Assert.Equal(1, favorites.TotalCount);
    }

    [Fact]
    public async Task UserBCannotAccessUserAWatchlist()
    {
        await fixture.ResetAsync();

        var userAToken = await RegisterAndGetTokenAsync("owner");
        var userBToken = await RegisterAndGetTokenAsync("intruder");
        var movieId = await SeedMovieAsync();

        var createWatchlistResponse = await SendAuthorizedPostAsync(
            "/api/watchlists",
            userAToken,
            new CreateWatchlistRequest("Private List"));

        var watchlist = await createWatchlistResponse.Content.ReadFromJsonAsync<WatchlistSummaryResponse>();
        Assert.NotNull(watchlist);

        var getResponse = await SendAuthorizedGetAsync($"/api/watchlists/{watchlist.Id}", userBToken);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        var addResponse = await SendAuthorizedPostAsync(
            $"/api/watchlists/{watchlist.Id}/movies/{movieId}",
            userBToken);

        Assert.Equal(HttpStatusCode.NotFound, addResponse.StatusCode);

        var deleteResponse = await SendAuthorizedDeleteAsync($"/api/watchlists/{watchlist.Id}", userBToken);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task FavoriteStatusEndpointsReturnExpectedValues()
    {
        await fixture.ResetAsync();

        var userAToken = await RegisterAndGetTokenAsync("status-user-a");
        var userBToken = await RegisterAndGetTokenAsync("status-user-b");
        var movieId = await SeedMovieAsync();

        var unauthorizedResponse = await _client.GetAsync($"/api/favorites/movies/{movieId}/status");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);

        var beforeFavorite = await SendAuthorizedGetAsync($"/api/favorites/movies/{movieId}/status", userAToken);
        Assert.Equal(HttpStatusCode.OK, beforeFavorite.StatusCode);
        var beforeFavoriteStatus = await beforeFavorite.Content.ReadFromJsonAsync<FavoriteStatusResponse>();
        Assert.NotNull(beforeFavoriteStatus);
        Assert.False(beforeFavoriteStatus.IsFavorited);

        await SendAuthorizedPostAsync($"/api/favorites/movies/{movieId}", userAToken);

        var afterFavorite = await SendAuthorizedGetAsync($"/api/favorites/movies/{movieId}/status", userAToken);
        var afterFavoriteStatus = await afterFavorite.Content.ReadFromJsonAsync<FavoriteStatusResponse>();
        Assert.NotNull(afterFavoriteStatus);
        Assert.True(afterFavoriteStatus.IsFavorited);

        var otherUserStatusResponse = await SendAuthorizedGetAsync(
            $"/api/favorites/movies/{movieId}/status",
            userBToken);
        var otherUserStatus = await otherUserStatusResponse.Content.ReadFromJsonAsync<FavoriteStatusResponse>();
        Assert.NotNull(otherUserStatus);
        Assert.False(otherUserStatus.IsFavorited);
    }

    [Fact]
    public async Task WatchlistMembershipEndpointReturnsExpectedValues()
    {
        await fixture.ResetAsync();

        var userAToken = await RegisterAndGetTokenAsync("membership-user-a");
        var userBToken = await RegisterAndGetTokenAsync("membership-user-b");
        var movieId = await SeedMovieAsync();

        var unauthorizedResponse = await _client.GetAsync(
            $"/api/watchlists/membership?mediaType=movie&contentId={movieId}");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);

        var emptyMembershipResponse = await SendAuthorizedGetAsync(
            $"/api/watchlists/membership?mediaType=movie&contentId={movieId}",
            userAToken);
        Assert.Equal(HttpStatusCode.OK, emptyMembershipResponse.StatusCode);
        var emptyMembership = await emptyMembershipResponse.Content.ReadFromJsonAsync<WatchlistMembershipResponse>();
        Assert.NotNull(emptyMembership);
        Assert.False(emptyMembership.IsInWatchlist);
        Assert.Empty(emptyMembership.WatchlistIds);

        var createWatchlistResponse = await SendAuthorizedPostAsync(
            "/api/watchlists",
            userAToken,
            new CreateWatchlistRequest("Status List"));
        var watchlist = await createWatchlistResponse.Content.ReadFromJsonAsync<WatchlistSummaryResponse>();
        Assert.NotNull(watchlist);

        await SendAuthorizedPostAsync($"/api/watchlists/{watchlist.Id}/movies/{movieId}", userAToken);

        var membershipResponse = await SendAuthorizedGetAsync(
            $"/api/watchlists/membership?mediaType=movie&contentId={movieId}",
            userAToken);
        var membership = await membershipResponse.Content.ReadFromJsonAsync<WatchlistMembershipResponse>();
        Assert.NotNull(membership);
        Assert.True(membership.IsInWatchlist);
        Assert.Equal([watchlist.Id], membership.WatchlistIds);

        var otherUserMembershipResponse = await SendAuthorizedGetAsync(
            $"/api/watchlists/membership?mediaType=movie&contentId={movieId}",
            userBToken);
        var otherUserMembership = await otherUserMembershipResponse.Content.ReadFromJsonAsync<WatchlistMembershipResponse>();
        Assert.NotNull(otherUserMembership);
        Assert.False(otherUserMembership.IsInWatchlist);
        Assert.Empty(otherUserMembership.WatchlistIds);
    }

    [Fact]
    public async Task MissingTokenReturnsUnauthorized()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/favorites");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FavoriteNonexistentMovieReturnsNotFound()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("favorite-user");
        var response = await SendAuthorizedPostAsync(
            $"/api/favorites/movies/{Guid.NewGuid()}",
            token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateWatchlistNameReturnsConflict()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("watchlist-user");

        await SendAuthorizedPostAsync("/api/watchlists", token, new CreateWatchlistRequest("Weekend Watch"));
        var duplicateResponse = await SendAuthorizedPostAsync(
            "/api/watchlists",
            token,
            new CreateWatchlistRequest("  weekend watch  "));

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task ExistingMovieSearchStillWorksWithoutAuthentication()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/movies/search?q=interstellar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExistingAuthRegisterStillWorks()
    {
        await fixture.ResetAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            $"auth-{Guid.NewGuid():N}@example.com",
            "StrongPassword123",
            "Auth User"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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

    private Task<HttpResponseMessage> SendAuthorizedDeleteAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}

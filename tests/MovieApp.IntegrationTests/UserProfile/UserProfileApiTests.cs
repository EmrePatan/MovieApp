using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MovieApp.Contracts.Auth;
using MovieApp.Contracts.Favorites;
using MovieApp.Contracts.Movies;
using MovieApp.Contracts.Ratings;
using MovieApp.Contracts.Reviews;
using MovieApp.Contracts.TvShows;
using MovieApp.Contracts.Users;
using MovieApp.Contracts.Watchlists;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.UserProfile;

[CollectionDefinition("UserProfileApi")]
public sealed class UserProfileApiTestsFixture : ICollectionFixture<UserProfileApiFixture>;

[Collection("UserProfileApi")]
public sealed class UserProfileApiTests(UserProfileApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetCurrentProfileReturnsAuthenticatedUser()
    {
        await fixture.ResetAsync();

        var email = $"profile-{Guid.NewGuid():N}@example.com";
        var token = await RegisterAndGetTokenAsync(email, "Integration User");

        var response = await SendAuthorizedGetAsync("/api/users/me", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal(email, profile.Email);
        Assert.Equal("Integration User", profile.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(profile.UserName));
    }

    [Fact]
    public async Task UpdateProfileChangesDisplayName()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var response = await SendAuthorizedPutAsync(
            "/api/users/me/profile",
            token,
            new UpdateProfileRequest("  New Display Name  "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal("New Display Name", profile.DisplayName);
    }

    [Fact]
    public async Task UpdateProfileRejectsEmptyDisplayName()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var response = await SendAuthorizedPutAsync(
            "/api/users/me/profile",
            token,
            new UpdateProfileRequest("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeEmailReturnsNewTokenAndInvalidatesOldToken()
    {
        await fixture.ResetAsync();

        var oldEmail = $"old-{Guid.NewGuid():N}@example.com";
        var newEmail = $"new-{Guid.NewGuid():N}@example.com";
        var oldToken = await RegisterAndGetTokenAsync(oldEmail);

        var changeResponse = await SendAuthorizedPutAsync(
            "/api/users/me/email",
            oldToken,
            new ChangeEmailRequest(newEmail, "StrongPassword123"));

        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

        var payload = await changeResponse.Content.ReadFromJsonAsync<UserProfileAuthResponse>();
        Assert.NotNull(payload);
        Assert.Equal(newEmail, payload.User.Email);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));

        var oldTokenResponse = await SendAuthorizedGetAsync("/api/users/me", oldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldTokenResponse.StatusCode);

        var newTokenResponse = await SendAuthorizedGetAsync("/api/users/me", payload.AccessToken);
        Assert.Equal(HttpStatusCode.OK, newTokenResponse.StatusCode);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            newEmail,
            "StrongPassword123"));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var oldLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            oldEmail,
            "StrongPassword123"));

        Assert.Equal(HttpStatusCode.Unauthorized, oldLoginResponse.StatusCode);
    }

    [Fact]
    public async Task ChangeEmailRejectsDuplicateEmail()
    {
        await fixture.ResetAsync();

        var existingEmail = $"existing-{Guid.NewGuid():N}@example.com";
        await RegisterAndGetTokenAsync(existingEmail);

        var token = await RegisterAndGetTokenAsync();
        var response = await SendAuthorizedPutAsync(
            "/api/users/me/email",
            token,
            new ChangeEmailRequest(existingEmail, "StrongPassword123"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ChangeEmailRejectsIncorrectCurrentPassword()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var response = await SendAuthorizedPutAsync(
            "/api/users/me/email",
            token,
            new ChangeEmailRequest($"new-{Guid.NewGuid():N}@example.com", "WrongPassword123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePasswordReturnsNewTokenAndInvalidatesOldToken()
    {
        await fixture.ResetAsync();

        var email = $"password-{Guid.NewGuid():N}@example.com";
        var oldToken = await RegisterAndGetTokenAsync(email);

        var changeResponse = await SendAuthorizedPutAsync(
            "/api/users/me/password",
            oldToken,
            new ChangePasswordRequest("StrongPassword123", "AnotherPassword123"));

        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

        var payload = await changeResponse.Content.ReadFromJsonAsync<UserProfileAuthResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));

        var oldTokenResponse = await SendAuthorizedGetAsync("/api/users/me", oldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldTokenResponse.StatusCode);

        var oldLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            email,
            "StrongPassword123"));

        Assert.Equal(HttpStatusCode.Unauthorized, oldLoginResponse.StatusCode);

        var newLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            email,
            "AnotherPassword123"));

        Assert.Equal(HttpStatusCode.OK, newLoginResponse.StatusCode);
    }

    [Fact]
    public async Task ChangePasswordRejectsSamePassword()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var response = await SendAuthorizedPutAsync(
            "/api/users/me/password",
            token,
            new ChangePasswordRequest("StrongPassword123", "StrongPassword123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetStatisticsReturnsCounts()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var movieId = await SeedMovieAsync();
        var tvShowId = await SeedTvShowAsync();
        var episodeId = await SeedEpisodeAsync(tvShowId, 1, 1);

        await SendAuthorizedPostAsync($"/api/favorites/movies/{movieId}", token);
        await SendAuthorizedPostAsync($"/api/favorites/tvshows/{tvShowId}", token);

        var watchlistResponse = await SendAuthorizedPostAsync(
            "/api/watchlists",
            token,
            new CreateWatchlistRequest("My List"));

        var watchlist = await watchlistResponse.Content.ReadFromJsonAsync<WatchlistSummaryResponse>();
        Assert.NotNull(watchlist);

        await SendAuthorizedPostAsync($"/api/watchlists/{watchlist.Id}/movies/{movieId}", token);
        await SendAuthorizedPostAsync(
            $"/api/ratings/movies/{movieId}",
            token,
            new CreateRatingRequest(8));
        await SendAuthorizedPostAsync(
            $"/api/reviews/movies/{movieId}",
            token,
            new CreateReviewRequest("Great movie"));
        await SendAuthorizedPostAsync($"/api/watch-history/movies/{movieId}", token);
        await SendAuthorizedPostAsync($"/api/watch-history/episodes/{episodeId}", token);
        await SendAuthorizedGetAsync("/api/search?q=interstellar", token);

        var response = await SendAuthorizedGetAsync("/api/users/me/statistics", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var statistics = await response.Content.ReadFromJsonAsync<UserStatisticsResponse>();
        Assert.NotNull(statistics);
        Assert.Equal(1, statistics.FavoriteMovieCount);
        Assert.Equal(1, statistics.FavoriteTvShowCount);
        Assert.Equal(1, statistics.WatchlistCount);
        Assert.Equal(1, statistics.WatchlistItemCount);
        Assert.Equal(1, statistics.RatedMovieCount);
        Assert.Equal(1, statistics.ReviewedMovieCount);
        Assert.Equal(1, statistics.WatchedMovieCount);
        Assert.Equal(1, statistics.WatchedEpisodeCount);
        Assert.Equal(1, statistics.TotalRatingCount);
        Assert.Equal(1, statistics.TotalReviewCount);
        Assert.Equal(2, statistics.TotalWatchedCount);
    }

    [Fact]
    public async Task DeleteAccountRemovesUserOwnedDataAndPreservesCatalog()
    {
        await fixture.ResetAsync();

        var userAEmail = $"user-a-{Guid.NewGuid():N}@example.com";
        var userAToken = await RegisterAndGetTokenAsync(userAEmail, "User A");
        var userBToken = await RegisterAndGetTokenAsync($"user-b-{Guid.NewGuid():N}@example.com", "User B");

        var movieId = await SeedMovieAsync();
        var tvShowId = await SeedTvShowAsync();
        var episodeId = await SeedEpisodeAsync(tvShowId, 1, 1);

        var watchlistResponse = await SendAuthorizedPostAsync(
            "/api/watchlists",
            userAToken,
            new CreateWatchlistRequest("Delete Me"));

        var watchlist = await watchlistResponse.Content.ReadFromJsonAsync<WatchlistSummaryResponse>();
        Assert.NotNull(watchlist);

        await SendAuthorizedPostAsync($"/api/favorites/movies/{movieId}", userAToken);
        await SendAuthorizedPostAsync($"/api/watchlists/{watchlist.Id}/movies/{movieId}", userAToken);
        await SendAuthorizedPostAsync($"/api/ratings/movies/{movieId}", userAToken, new CreateRatingRequest(7));
        await SendAuthorizedPostAsync($"/api/reviews/movies/{movieId}", userAToken, new CreateReviewRequest("Review"));
        await SendAuthorizedPostAsync($"/api/watch-history/movies/{movieId}", userAToken);
        await SendAuthorizedPostAsync($"/api/watch-history/episodes/{episodeId}", userAToken);
        await SendAuthorizedGetAsync("/api/search?q=interstellar", userAToken);
        await SendAuthorizedPostAsync($"/api/favorites/movies/{movieId}", userBToken);

        await using (var context = CreateContext())
        {
            Assert.Equal(2, await context.Users.CountAsync());
            Assert.Equal(2, await context.Favorites.CountAsync());
            Assert.Equal(1, await context.Watchlists.CountAsync());
            Assert.Equal(1, await context.WatchlistItems.CountAsync());
            Assert.Equal(1, await context.Ratings.CountAsync());
            Assert.Equal(1, await context.Reviews.CountAsync());
            Assert.Equal(1, await context.WatchedMovies.CountAsync());
            Assert.Equal(1, await context.WatchedEpisodes.CountAsync());
            Assert.Equal(1, await context.SearchHistories.CountAsync());
            Assert.True(await context.Movies.AnyAsync(movie => movie.Id == movieId));
            Assert.True(await context.TvShows.AnyAsync(tvShow => tvShow.Id == tvShowId));
        }

        var deleteResponse = await SendAuthorizedDeleteAsync(
            "/api/users/me",
            userAToken,
            new DeleteAccountRequest("StrongPassword123"));

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using (var context = CreateContext())
        {
            Assert.Equal(1, await context.Users.CountAsync());
            Assert.Equal(1, await context.Favorites.CountAsync());
            Assert.Equal(0, await context.Watchlists.CountAsync());
            Assert.Equal(0, await context.WatchlistItems.CountAsync());
            Assert.Equal(0, await context.Ratings.CountAsync());
            Assert.Equal(0, await context.Reviews.CountAsync());
            Assert.Equal(0, await context.WatchedMovies.CountAsync());
            Assert.Equal(0, await context.WatchedEpisodes.CountAsync());
            Assert.Equal(0, await context.SearchHistories.CountAsync());
            Assert.True(await context.Movies.AnyAsync(movie => movie.Id == movieId));
            Assert.True(await context.TvShows.AnyAsync(tvShow => tvShow.Id == tvShowId));
            Assert.True(await context.Episodes.AnyAsync(episode => episode.Id == episodeId));
        }

        var deletedLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            userAEmail,
            "StrongPassword123"));

        Assert.Equal(HttpStatusCode.Unauthorized, deletedLoginResponse.StatusCode);

        var userBProfileResponse = await SendAuthorizedGetAsync("/api/users/me", userBToken);
        Assert.Equal(HttpStatusCode.OK, userBProfileResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteAccountRejectsIncorrectPassword()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var response = await SendAuthorizedDeleteAsync(
            "/api/users/me",
            token,
            new DeleteAccountRequest("WrongPassword123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProfileEndpointsRequireAuthentication()
    {
        await fixture.ResetAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/users/me")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.PutAsJsonAsync("/api/users/me/profile", new UpdateProfileRequest("Name"))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/users/me/statistics")).StatusCode);
    }

    [Fact]
    public async Task UpdatedDisplayNameAppearsInReviewAuthor()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var movieId = await SeedMovieAsync();

        await SendAuthorizedPostAsync(
            $"/api/reviews/movies/{movieId}",
            token,
            new CreateReviewRequest("Original review"));

        await SendAuthorizedPutAsync(
            "/api/users/me/profile",
            token,
            new UpdateProfileRequest("Updated Reviewer"));

        var reviewsResponse = await _client.GetAsync($"/api/reviews/movies/{movieId}?page=1&pageSize=20");
        var reviews = await reviewsResponse.Content.ReadFromJsonAsync<ReviewListResponse>();

        Assert.NotNull(reviews);
        Assert.Single(reviews.Items);
        Assert.Equal("Updated Reviewer", reviews.Items[0].User.DisplayName);
    }

    [Fact]
    public async Task ExistingAuthMeEndpointStillWorks()
    {
        await fixture.ResetAsync();

        var email = $"auth-me-{Guid.NewGuid():N}@example.com";
        var token = await RegisterAndGetTokenAsync(email);

        var response = await SendAuthorizedGetAsync("/api/auth/me", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(payload);
        Assert.Equal(email, payload.Email);
    }

    private async Task<string> RegisterAndGetTokenAsync(
        string? email = null,
        string displayName = "Integration User")
    {
        email ??= $"user-{Guid.NewGuid():N}@example.com";

        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email,
            "StrongPassword123",
            displayName));

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

    private async Task<Guid> SeedEpisodeAsync(Guid tvShowId, int seasonNumber, int episodeNumber)
    {
        var response = await _client.GetAsync(
            $"/api/tvshows/{tvShowId}/seasons/{seasonNumber}/episodes/{episodeNumber}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<EpisodeResponse>();
        Assert.NotNull(payload);
        return payload.Id;
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

    private Task<HttpResponseMessage> SendAuthorizedPutAsync(string url, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SendAuthorizedDeleteAsync(string url, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(UserProfileIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

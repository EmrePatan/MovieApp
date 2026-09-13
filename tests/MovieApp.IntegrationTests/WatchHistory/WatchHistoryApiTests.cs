using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MovieApp.Contracts.Auth;
using MovieApp.Contracts.Movies;
using MovieApp.Contracts.TvShows;
using MovieApp.Contracts.WatchHistory;

namespace MovieApp.IntegrationTests.WatchHistory;

[CollectionDefinition("WatchHistoryApi")]
public sealed class WatchHistoryApiTestsFixture : ICollectionFixture<WatchHistoryApiFixture>;

[Collection("WatchHistoryApi")]
public sealed class WatchHistoryApiTests(WatchHistoryApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task UserCanWatchAndUnwatchMovie()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("watch-movie");
        var movieId = await SeedMovieAsync();

        var createResponse = await SendAuthorizedPostAsync($"/api/watch-history/movies/{movieId}", token);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var statusResponse = await SendAuthorizedGetAsync($"/api/watch-history/movies/{movieId}/me", token);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var status = await statusResponse.Content.ReadFromJsonAsync<MovieWatchStatusResponse>();
        Assert.NotNull(status);
        Assert.True(status.IsWatched);
        Assert.NotNull(status.WatchedAt);

        var duplicateResponse = await SendAuthorizedPostAsync($"/api/watch-history/movies/{movieId}", token);
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);

        var deleteResponse = await SendAuthorizedDeleteAsync($"/api/watch-history/movies/{movieId}", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var unwatchedStatus = await SendAuthorizedGetAsync($"/api/watch-history/movies/{movieId}/me", token);
        var unwatchedPayload = await unwatchedStatus.Content.ReadFromJsonAsync<MovieWatchStatusResponse>();
        Assert.NotNull(unwatchedPayload);
        Assert.False(unwatchedPayload.IsWatched);
        Assert.Null(unwatchedPayload.WatchedAt);
    }

    [Fact]
    public async Task UserCanWatchAndUnwatchEpisode()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("watch-episode");
        var tvShowId = await SeedTvShowAsync();
        var episodeId = await SeedEpisodeAsync(tvShowId, 1, 1);

        var createResponse = await SendAuthorizedPostAsync($"/api/watch-history/episodes/{episodeId}", token);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var statusResponse = await SendAuthorizedGetAsync($"/api/watch-history/episodes/{episodeId}/me", token);
        var status = await statusResponse.Content.ReadFromJsonAsync<EpisodeWatchStatusResponse>();
        Assert.NotNull(status);
        Assert.True(status.IsWatched);

        var duplicateResponse = await SendAuthorizedPostAsync($"/api/watch-history/episodes/{episodeId}", token);
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);

        var deleteResponse = await SendAuthorizedDeleteAsync($"/api/watch-history/episodes/{episodeId}", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task WatchedMoviesListReturnsPaginatedResults()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("watched-movies");
        var movieId = await SeedMovieAsync();
        await SendAuthorizedPostAsync($"/api/watch-history/movies/{movieId}", token);

        var response = await SendAuthorizedGetAsync("/api/watch-history/movies?page=1&pageSize=20", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<WatchedMoviesListResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal("Interstellar", payload.Items[0].Title);
        Assert.Equal(1, payload.TotalCount);
    }

    [Fact]
    public async Task WatchedEpisodesListReturnsTvMetadata()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("watched-episodes");
        var tvShowId = await SeedTvShowAsync();
        var episodeId = await SeedEpisodeAsync(tvShowId, 1, 1);
        await SendAuthorizedPostAsync($"/api/watch-history/episodes/{episodeId}", token);

        var response = await SendAuthorizedGetAsync("/api/watch-history/episodes?page=1&pageSize=20", token);
        var payload = await response.Content.ReadFromJsonAsync<WatchedEpisodesListResponse>();

        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal("Breaking Bad", payload.Items[0].TvShowTitle);
        Assert.Equal(1, payload.Items[0].SeasonNumber);
        Assert.Equal(1, payload.Items[0].EpisodeNumber);
    }

    [Fact]
    public async Task RecentWatchHistoryReturnsMoviesAndEpisodesOrderedByWatchedAt()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("recent-history");
        var movieId = await SeedMovieAsync();
        var tvShowId = await SeedTvShowAsync();
        var episodeId = await SeedEpisodeAsync(tvShowId, 1, 1);

        await SendAuthorizedPostAsync($"/api/watch-history/movies/{movieId}", token);
        await SendAuthorizedPostAsync($"/api/watch-history/episodes/{episodeId}", token);

        var response = await SendAuthorizedGetAsync("/api/watch-history/recent?page=1&pageSize=20", token);
        var payload = await response.Content.ReadFromJsonAsync<RecentWatchHistoryResponse>();

        Assert.NotNull(payload);
        Assert.Equal(2, payload.TotalCount);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal("episode", payload.Items[0].Type);
        Assert.Equal("movie", payload.Items[1].Type);
    }

    [Fact]
    public async Task TvShowProgressReturnsNextEpisode()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("tv-progress");
        var tvShowId = await SeedTvShowWithAllEpisodesAsync();
        var episode1 = await SeedEpisodeAsync(tvShowId, 1, 1);
        var episode2 = await SeedEpisodeAsync(tvShowId, 1, 2);

        await SendAuthorizedPostAsync($"/api/watch-history/episodes/{episode1}", token);
        await SendAuthorizedPostAsync($"/api/watch-history/episodes/{episode2}", token);

        var response = await SendAuthorizedGetAsync($"/api/watch-history/tvshows/{tvShowId}", token);
        var payload = await response.Content.ReadFromJsonAsync<TvShowWatchProgressResponse>();

        Assert.NotNull(payload);
        Assert.Equal(7, payload.TotalEpisodes);
        Assert.Equal(2, payload.WatchedEpisodes);
        Assert.Equal(28.57m, payload.ProgressPercentage);
        Assert.NotNull(payload.NextEpisode);
        Assert.Equal(3, payload.NextEpisode!.EpisodeNumber);
        Assert.Equal(1, payload.NextEpisode.SeasonNumber);
    }

    [Fact]
    public async Task SeasonProgressReturnsNextEpisodeInSeason()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("season-progress");
        var tvShowId = await SeedTvShowAsync();
        await _client.GetAsync($"/api/tvshows/{tvShowId}/seasons/1");
        var episode1 = await SeedEpisodeAsync(tvShowId, 1, 1);
        var episode2 = await SeedEpisodeAsync(tvShowId, 1, 2);

        await SendAuthorizedPostAsync($"/api/watch-history/episodes/{episode1}", token);
        await SendAuthorizedPostAsync($"/api/watch-history/episodes/{episode2}", token);

        var response = await SendAuthorizedGetAsync($"/api/watch-history/tvshows/{tvShowId}/seasons/1", token);
        var payload = await response.Content.ReadFromJsonAsync<SeasonWatchProgressResponse>();

        Assert.NotNull(payload);
        Assert.Equal(3, payload.TotalEpisodes);
        Assert.Equal(2, payload.WatchedEpisodes);
        Assert.Equal(66.67m, payload.ProgressPercentage);
        Assert.NotNull(payload.NextEpisode);
        Assert.Equal(3, payload.NextEpisode!.EpisodeNumber);
    }

    [Fact]
    public async Task UserBCannotSeeUserAWatchStatus()
    {
        await fixture.ResetAsync();

        var userAToken = await RegisterAndGetTokenAsync("watch-owner");
        var userBToken = await RegisterAndGetTokenAsync("watch-other");
        var movieId = await SeedMovieAsync();

        await SendAuthorizedPostAsync($"/api/watch-history/movies/{movieId}", userAToken);

        var userBStatus = await SendAuthorizedGetAsync($"/api/watch-history/movies/{movieId}/me", userBToken);
        var payload = await userBStatus.Content.ReadFromJsonAsync<MovieWatchStatusResponse>();

        Assert.NotNull(payload);
        Assert.False(payload.IsWatched);
    }

    [Fact]
    public async Task AnonymousRequestsAreRejected()
    {
        await fixture.ResetAsync();

        var movieId = await SeedMovieAsync();
        var response = await _client.PostAsync($"/api/watch-history/movies/{movieId}", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WatchNonexistentMovieReturnsNotFound()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("missing-movie");
        var response = await SendAuthorizedPostAsync($"/api/watch-history/movies/{Guid.NewGuid()}", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WatchNonexistentEpisodeReturnsNotFound()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("missing-episode");
        var response = await SendAuthorizedPostAsync($"/api/watch-history/episodes/{Guid.NewGuid()}", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InvalidPaginationReturnsBadRequest()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("invalid-pagination");
        var response = await SendAuthorizedGetAsync("/api/watch-history/movies?page=0&pageSize=20", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpdateEpisodeWatchStateMarksMultipleEpisodesInOneRequest()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("bulk-watch");
        var tvShowId = await SeedTvShowAsync();
        await _client.GetAsync($"/api/tvshows/{tvShowId}/seasons/1");
        var episode1 = await SeedEpisodeAsync(tvShowId, 1, 1);
        var episode2 = await SeedEpisodeAsync(tvShowId, 1, 2);
        var episode3 = await SeedEpisodeAsync(tvShowId, 1, 3);

        var response = await SendAuthorizedPostJsonAsync(
            $"/api/watch-history/tvshows/{tvShowId}/episodes/bulk",
            token,
            new BulkUpdateEpisodeWatchStateRequest([episode1, episode2], true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<BulkUpdateEpisodeWatchStateResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.AffectedCount);

        var seasonWatched = await SendAuthorizedGetAsync(
            $"/api/watch-history/tvshows/{tvShowId}/seasons/1/episodes",
            token);
        var seasonPayload = await seasonWatched.Content.ReadFromJsonAsync<SeasonWatchedEpisodesResponse>();
        Assert.NotNull(seasonPayload);
        Assert.Equal(2, seasonPayload.WatchedEpisodeIds.Count);
        Assert.Contains(episode1, seasonPayload.WatchedEpisodeIds);
        Assert.Contains(episode2, seasonPayload.WatchedEpisodeIds);
        Assert.DoesNotContain(episode3, seasonPayload.WatchedEpisodeIds);
    }

    [Fact]
    public async Task BulkUpdateEpisodeWatchStateIsIdempotentForAlreadyWatchedEpisodes()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("bulk-idempotent");
        var tvShowId = await SeedTvShowAsync();
        var episodeId = await SeedEpisodeAsync(tvShowId, 1, 1);
        await SendAuthorizedPostAsync($"/api/watch-history/episodes/{episodeId}", token);

        var response = await SendAuthorizedPostJsonAsync(
            $"/api/watch-history/tvshows/{tvShowId}/episodes/bulk",
            token,
            new BulkUpdateEpisodeWatchStateRequest([episodeId], true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<BulkUpdateEpisodeWatchStateResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.AffectedCount);
    }

    [Fact]
    public async Task BulkUpdateEpisodeWatchStateRejectsInvalidEpisodeIds()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("bulk-invalid");
        var tvShowId = await SeedTvShowAsync();

        var response = await SendAuthorizedPostJsonAsync(
            $"/api/watch-history/tvshows/{tvShowId}/episodes/bulk",
            token,
            new BulkUpdateEpisodeWatchStateRequest([Guid.NewGuid()], true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MarkThroughEpisodeMarksAllPriorEpisodesAcrossSeasons()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("mark-through");
        var tvShowId = await SeedTvShowWithAllEpisodesAsync();
        await _client.GetAsync($"/api/tvshows/{tvShowId}/seasons/1");
        var episodeS1E1 = await SeedEpisodeAsync(tvShowId, 1, 1);
        var episodeS2E1 = await SeedEpisodeAsync(tvShowId, 2, 1);

        var response = await SendAuthorizedPostAsync(
            $"/api/watch-history/tvshows/{tvShowId}/episodes/{episodeS2E1}/mark-through",
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<MarkThroughEpisodeResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.AffectedCount >= 2);

        var progress = await SendAuthorizedGetAsync($"/api/watch-history/tvshows/{tvShowId}", token);
        var progressPayload = await progress.Content.ReadFromJsonAsync<TvShowWatchProgressResponse>();
        Assert.NotNull(progressPayload);
        Assert.True(progressPayload.WatchedEpisodes >= 2);
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

    private Task<HttpResponseMessage> SendAuthorizedDeleteAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}

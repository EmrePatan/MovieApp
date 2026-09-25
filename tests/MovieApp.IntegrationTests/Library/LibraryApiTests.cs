using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MovieApp.Contracts.Auth;
using MovieApp.Contracts.Favorites;
using MovieApp.Contracts.Library;
using MovieApp.Contracts.Movies;
using MovieApp.Contracts.TvShows;
using MovieApp.Contracts.WatchHistory;
using MovieApp.Contracts.Watchlists;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Providers;
using MovieApp.IntegrationTests.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task LibraryActionsRequiresAuthentication()
    {
        await fixture.ResetAsync();

        var movieId = await SeedMovieAsync();
        var response = await _client.GetAsync($"/api/library/actions?mediaType=movie&contentId={movieId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LibraryActionsReturnsAggregatedMovieStatus()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var movieId = await SeedMovieAsync();
        var watchlist = await CreateWatchlistAsync(token, "Actions");

        await SendAuthorizedPostAsync($"/api/favorites/movies/{movieId}", token);
        await SendAuthorizedPostAsync($"/api/watchlists/{watchlist.Id}/movies/{movieId}", token);
        await SendAuthorizedPostAsync($"/api/watch-history/movies/{movieId}", token);

        var response = await SendAuthorizedGetAsync(
            $"/api/library/actions?mediaType=movie&contentId={movieId}",
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LibraryActionStatusResponse>();
        Assert.NotNull(payload);
        Assert.Equal("movie", payload.MediaType);
        Assert.Equal(movieId, payload.ContentId);
        Assert.True(payload.IsFavorited);
        Assert.True(payload.IsInWatchlist);
        Assert.Contains(watchlist.Id, payload.WatchlistIds);
        Assert.True(payload.IsWatched);
        Assert.NotNull(payload.WatchedAt);
    }

    [Fact]
    public async Task LibraryActionsReturnsEpisodeWatchedStateForTv()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var tvShowId = await SeedTvShowWithAllEpisodesAsync();
        var episodeId = await SeedEpisodeAsync(tvShowId, 1, 1);
        await SendAuthorizedPostAsync($"/api/watch-history/episodes/{episodeId}", token);

        var response = await SendAuthorizedGetAsync(
            $"/api/library/actions?mediaType=tv&contentId={tvShowId}&episodeId={episodeId}",
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LibraryActionStatusResponse>();
        Assert.NotNull(payload);
        Assert.Equal("tv", payload.MediaType);
        Assert.Equal(tvShowId, payload.ContentId);
        Assert.True(payload.IsWatched);
        Assert.NotNull(payload.WatchedAt);
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
    public async Task EndedTvShowWithAllEpisodesWatchedReportsCompletedProgress()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var tvShowId = await SeedTvShowWithAllEpisodesAsync();
        await MarkTvShowWatchedAsync(tvShowId, token);

        var progress = await GetTvShowProgressAsync(tvShowId, token);

        Assert.True(progress.IsFullyWatched);
        Assert.True(progress.IsCompleted);
    }

    [Fact]
    public async Task WatchingOrdersPartiallyWatchedShowsBeforeCaughtUpOnes()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var partialShowId = await SeedTvShowAsync();
        await _client.GetAsync($"/api/tvshows/{partialShowId}/seasons/1");
        var partialEpisode = await SeedEpisodeAsync(partialShowId, 1, 1);
        await SendAuthorizedPostAsync($"/api/watch-history/episodes/{partialEpisode}", token);

        var caughtUpShowId = await SeedSecondTvShowWithAllEpisodesAsync();
        await MarkTvShowWatchedAsync(caughtUpShowId, token);
        await SetTvShowStatusAsync(caughtUpShowId, TvShowStatus.ReturningSeries);

        try
        {
            var caughtUpEpisode = await SeedEpisodeAsync(caughtUpShowId, 1, 1);
            await SendAuthorizedPostAsync($"/api/watch-history/episodes/{caughtUpEpisode}", token);

            var watching = await GetLibraryAsync("/api/library?category=watching&mediaType=tv", token);

            Assert.Equal(2, watching.Items.Count);
            Assert.Equal(partialShowId, watching.Items[0].Id);
            Assert.True(watching.Items[0].ProgressPercentage is > 0 and < 100m);
            Assert.NotNull(watching.Items[0].NextEpisode);
            Assert.Equal(caughtUpShowId, watching.Items[1].Id);
            Assert.Equal(100m, watching.Items[1].ProgressPercentage);
            Assert.Null(watching.Items[1].NextEpisode);
        }
        finally
        {
            await SetTvShowStatusAsync(caughtUpShowId, TvShowStatus.Ended);
        }
    }

    [Fact]
    public async Task CaughtUpReturningSeriesStaysWatching()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var tvShowId = await SeedTvShowWithAllEpisodesAsync();
        await MarkTvShowWatchedAsync(tvShowId, token);
        await SetTvShowStatusAsync(tvShowId, TvShowStatus.ReturningSeries);

        try
        {
            var watching = await GetLibraryAsync("/api/library?category=watching&mediaType=tv", token);
            var item = Assert.Single(watching.Items, entry => entry.Id == tvShowId);
            Assert.Equal("watching", item.CollectionStatus);
            Assert.Equal(100m, item.ProgressPercentage);
            Assert.Null(item.NextEpisode);

            var watched = await GetLibraryAsync("/api/library?category=watched&mediaType=all", token);
            Assert.DoesNotContain(watched.Items, entry => entry.Id == tvShowId);

            var progress = await GetTvShowProgressAsync(tvShowId, token);
            Assert.True(progress.IsFullyWatched);
            Assert.False(progress.IsCompleted);
        }
        finally
        {
            await SetTvShowStatusAsync(tvShowId, TvShowStatus.Ended);
        }
    }

    [Fact]
    public async Task EndedTvShowWithUningestedSeasonIsNotCompletedYet()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync();
        var tvShowId = await SeedTvShowWithAllEpisodesAsync();
        await MarkTvShowWatchedAsync(tvShowId, token);
        var extraSeasonId = await AddSeasonSummaryWithoutEpisodesAsync(tvShowId, seasonNumber: 4, episodeCount: 5);

        try
        {
            var watching = await GetLibraryAsync("/api/library?category=watching&mediaType=tv", token);
            var item = Assert.Single(watching.Items, entry => entry.Id == tvShowId);
            Assert.True(item.ProgressPercentage < 100m);

            var watched = await GetLibraryAsync("/api/library?category=watched&mediaType=tv", token);
            Assert.DoesNotContain(watched.Items, entry => entry.Id == tvShowId);

            var progress = await GetTvShowProgressAsync(tvShowId, token);
            Assert.Equal(12, progress.RegularTotalEpisodes);
            Assert.False(progress.IsFullyWatched);
            Assert.False(progress.IsCompleted);
        }
        finally
        {
            await RemoveSeasonAsync(extraSeasonId);
        }
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

    private Task<string> RegisterAndGetTokenAsync() =>
        AuthIntegrationHelpers.RegisterVerifyAndGetAccessTokenAsync(
            _client,
            fixture.Factory.Services,
            $"library-{Guid.NewGuid():N}@example.com");

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

    private async Task<Guid> SeedSecondTvShowAsync()
    {
        var response = await _client.GetAsync("/api/tvshows/search?q=office");
        var payload = await response.Content.ReadFromJsonAsync<TvShowSearchResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.Items.Count >= 1, "Expected a second catalog TV show for ordering coverage.");
        var breakingShowId = await SeedTvShowAsync();
        var candidate = payload.Items.FirstOrDefault(item => item.Id != breakingShowId);
        Assert.NotNull(candidate);
        return candidate.Id;
    }

    private async Task<Guid> SeedSecondTvShowWithAllEpisodesAsync()
    {
        var tvShowId = await SeedSecondTvShowAsync();
        await _client.GetAsync($"/api/tvshows/{tvShowId}/seasons/1");
        await _client.GetAsync($"/api/tvshows/{tvShowId}/seasons/2");
        await _client.GetAsync($"/api/tvshows/{tvShowId}/seasons/3");
        return tvShowId;
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

    private async Task MarkTvShowWatchedAsync(Guid tvShowId, string token)
    {
        var response = await SendAuthorizedPostJsonAsync(
            $"/api/watch-history/tvshows/{tvShowId}/watch-state",
            token,
            new MovieApp.Contracts.WatchHistory.SetWatchStateRequest(true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<LibraryListResponse> GetLibraryAsync(string url, string token)
    {
        var response = await SendAuthorizedGetAsync(url, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LibraryListResponse>();
        Assert.NotNull(payload);
        return payload;
    }

    private async Task<TvShowWatchProgressResponse> GetTvShowProgressAsync(Guid tvShowId, string token)
    {
        var response = await SendAuthorizedGetAsync($"/api/watch-history/tvshows/{tvShowId}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<TvShowWatchProgressResponse>();
        Assert.NotNull(payload);
        return payload;
    }

    private async Task SetTvShowStatusAsync(Guid tvShowId, TvShowStatus status)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.TvShows
            .Where(tvShow => tvShow.Id == tvShowId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(tvShow => tvShow.Status, status));
    }

    private async Task<Guid> AddSeasonSummaryWithoutEpisodesAsync(Guid tvShowId, int seasonNumber, int episodeCount)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var season = new Season
        {
            Id = Guid.NewGuid(),
            TvShowId = tvShowId,
            SeasonNumber = seasonNumber,
            EpisodeCount = episodeCount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        dbContext.Seasons.Add(season);
        await dbContext.SaveChangesAsync();
        return season.Id;
    }

    private async Task RemoveSeasonAsync(Guid seasonId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Seasons.Where(season => season.Id == seasonId).ExecuteDeleteAsync();
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

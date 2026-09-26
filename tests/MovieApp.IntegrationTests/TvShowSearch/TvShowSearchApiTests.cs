using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Contracts.Search;
using MovieApp.Contracts.TvShows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.IntegrationTests.TvShowSearch;

[CollectionDefinition("TvShowSearchApi")]
public sealed class TvShowSearchApiTestsFixture : ICollectionFixture<TvShowSearchApiFixture>;

[Collection("TvShowSearchApi")]
public sealed class TvShowSearchApiTests(TvShowSearchApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task SearchBreakingBadReturnsTvShowAndPersistsIt()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/tvshows/search?q=breaking");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TvShowSearchResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal(1, payload.Page);
        Assert.Equal(20, payload.PageSize);
        Assert.Equal("Breaking Bad", payload.Items[0].Title);
        Assert.Equal(FakeTvShowDataProvider.BreakingBadTmdbId, payload.Items[0].ExternalIds.TmdbId);

        await using var context = CreateContext();
        Assert.Equal(1, await context.TvShows.CountAsync());
        Assert.Equal(3, await context.Genres.CountAsync());
        Assert.Equal(3, await context.TvShowGenres.CountAsync());
        Assert.Equal(3, await context.Seasons.CountAsync());
    }

    [Fact]
    public async Task SearchWithExplicitPaginationReturnsSameResultAsDefault()
    {
        await fixture.ResetAsync();

        var defaultResponse = await _client.GetAsync("/api/tvshows/search?q=breaking");
        var explicitResponse = await _client.GetAsync("/api/tvshows/search?q=breaking&page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, explicitResponse.StatusCode);

        var defaultPayload = await defaultResponse.Content.ReadFromJsonAsync<TvShowSearchResponse>();
        var explicitPayload = await explicitResponse.Content.ReadFromJsonAsync<TvShowSearchResponse>();

        Assert.NotNull(defaultPayload);
        Assert.NotNull(explicitPayload);
        Assert.Equal(defaultPayload.Items[0].Title, explicitPayload.Items[0].Title);
    }

    [Fact]
    public async Task RepeatedSearchUsesCache()
    {
        await fixture.ResetAsync();

        var firstResponse = await _client.GetAsync("/api/tvshows/search?q=breaking");
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var tracker = scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();
            Assert.Equal(1, tracker.SearchTvShowsCallCount);
        }

        var secondResponse = await _client.GetAsync("/api/tvshows/search?q= breaking ");
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var tracker = scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();
            Assert.Equal(1, tracker.SearchTvShowsCallCount);
        }
    }

    [Fact]
    public async Task GetTvShowByIdHydratesSeasonSummariesAfterUnifiedSearchIngestion()
    {
        await fixture.ResetAsync();

        var searchResponse = await _client.GetAsync("/api/search?q=breaking&type=tv&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var searchPayload = await searchResponse.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(searchPayload);
        Assert.Single(searchPayload.Items);
        Assert.Equal("tv", searchPayload.Items[0].Type);

        var tvShowId = searchPayload.Items[0].Id;

        await using (var context = CreateContext())
        {
            Assert.Equal(1, await context.TvShows.CountAsync());
            Assert.Equal(0, await context.Seasons.CountAsync());
            Assert.Equal(0, await context.Episodes.CountAsync());
        }

        var detailsResponse = await _client.GetAsync($"/api/tvshows/{tvShowId}");
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var details = await detailsResponse.Content.ReadFromJsonAsync<TvShowDetailsResponse>();
        Assert.NotNull(details);
        Assert.Equal(3, details.Seasons.Count);
        Assert.All(details.Seasons, season => Assert.True(season.SeasonNumber >= 1));

        await using (var context = CreateContext())
        {
            Assert.Equal(3, await context.Seasons.CountAsync());
            Assert.Equal(0, await context.Episodes.CountAsync());
        }
    }

    [Fact]
    public async Task GetTvShowByIdRepairsTvShowMissingSeasonRows()
    {
        await fixture.ResetAsync();

        var tvShowId = Guid.NewGuid();
        await using (var context = CreateContext())
        {
            context.TvShows.Add(new TvShow
            {
                Id = tvShowId,
                TmdbId = FakeTvShowDataProvider.BreakingBadTmdbId,
                TvdbId = FakeTvShowDataProvider.BreakingBadTvdbId,
                ImdbId = FakeTvShowDataProvider.BreakingBadImdbId,
                Title = "Breaking Bad",
                OriginalTitle = "Breaking Bad",
                Overview = "Existing row without seasons.",
                FirstAirDate = new DateOnly(2008, 1, 20),
                PosterPath = "/fake/breaking-bad-poster.jpg",
                BackdropPath = "/fake/breaking-bad-backdrop.jpg",
                OriginalLanguage = "en",
                VoteAverage = 9.5m,
                VoteCount = 12000,
                Status = TvShowStatus.Ended,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await context.SaveChangesAsync();
            Assert.Equal(0, await context.Seasons.CountAsync());
        }

        var detailsResponse = await _client.GetAsync($"/api/tvshows/{tvShowId}");
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var details = await detailsResponse.Content.ReadFromJsonAsync<TvShowDetailsResponse>();
        Assert.NotNull(details);
        Assert.Equal(3, details.Seasons.Count);

        await using (var context = CreateContext())
        {
            Assert.Equal(3, await context.Seasons.CountAsync());
            Assert.Equal(0, await context.Episodes.CountAsync());
        }
    }

    [Fact]
    public async Task GetTvShowByIdReturnsPersistedDetails()
    {
        await fixture.ResetAsync();

        var searchResponse = await _client.GetAsync("/api/tvshows/search?q=breaking");
        var searchPayload = await searchResponse.Content.ReadFromJsonAsync<TvShowSearchResponse>();
        Assert.NotNull(searchPayload);

        var tvShowId = searchPayload.Items[0].Id;
        var detailsResponse = await _client.GetAsync($"/api/tvshows/{tvShowId}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var details = await detailsResponse.Content.ReadFromJsonAsync<TvShowDetailsResponse>();
        Assert.NotNull(details);
        Assert.Equal(tvShowId, details.Id);
        Assert.Equal("Breaking Bad", details.Title);
        Assert.Equal(3, details.Genres.Count);
        Assert.Equal(3, details.Seasons.Count);
    }

    [Fact]
    public async Task GetSeasonReturnsPersistedSeasonWithEpisodes()
    {
        await fixture.ResetAsync();

        var searchPayload = await SearchBreakingBadAsync();
        var tvShowId = searchPayload.Items[0].Id;

        var response = await _client.GetAsync($"/api/tvshows/{tvShowId}/seasons/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var season = await response.Content.ReadFromJsonAsync<SeasonResponse>();
        Assert.NotNull(season);
        Assert.Equal(1, season.SeasonNumber);
        Assert.Equal(3, season.Episodes.Count);
        Assert.Equal("Pilot", season.Episodes[0].Name);

        await using var context = CreateContext();
        Assert.Equal(3, await context.Episodes.CountAsync());
    }

    [Fact]
    public async Task GetEpisodeReturnsPersistedEpisode()
    {
        await fixture.ResetAsync();

        var searchPayload = await SearchBreakingBadAsync();
        var tvShowId = searchPayload.Items[0].Id;

        var response = await _client.GetAsync($"/api/tvshows/{tvShowId}/seasons/1/episodes/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var episode = await response.Content.ReadFromJsonAsync<EpisodeResponse>();
        Assert.NotNull(episode);
        Assert.Equal(1, episode.SeasonNumber);
        Assert.Equal(1, episode.EpisodeNumber);
        Assert.Equal("Pilot", episode.Name);
    }

    [Fact]
    public async Task GetTvShowByIdReturnsNotFoundForMissingTvShow()
    {
        var response = await _client.GetAsync($"/api/tvshows/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSeasonReturnsNotFoundForMissingSeason()
    {
        await fixture.ResetAsync();

        var searchPayload = await SearchBreakingBadAsync();
        var tvShowId = searchPayload.Items[0].Id;

        var response = await _client.GetAsync($"/api/tvshows/{tvShowId}/seasons/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEpisodeReturnsNotFoundForMissingEpisode()
    {
        await fixture.ResetAsync();

        var searchPayload = await SearchBreakingBadAsync();
        var tvShowId = searchPayload.Items[0].Id;

        var response = await _client.GetAsync($"/api/tvshows/{tvShowId}/seasons/1/episodes/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("")]
    public async Task SearchWithInvalidQueryReturnsBadRequest(string query)
    {
        var response = await _client.GetAsync($"/api/tvshows/search?q={query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task SearchWithInvalidPaginationReturnsBadRequest(string paginationQuery)
    {
        var response = await _client.GetAsync($"/api/tvshows/search?q=breaking&{paginationQuery}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<TvShowSearchResponse> SearchBreakingBadAsync()
    {
        var searchResponse = await _client.GetAsync("/api/tvshows/search?q=breaking");
        var searchPayload = await searchResponse.Content.ReadFromJsonAsync<TvShowSearchResponse>();
        Assert.NotNull(searchPayload);
        return searchPayload;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(TvShowSearchIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

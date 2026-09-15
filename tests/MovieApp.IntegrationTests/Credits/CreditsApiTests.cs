using System.Net;
using System.Net.Http.Json;
using MovieApp.Contracts.Credits;
using MovieApp.Contracts.Movies;
using MovieApp.Contracts.TvShows;
using MovieApp.Infrastructure.Providers;
using MovieApp.IntegrationTests.MovieSearch;
using MovieApp.IntegrationTests.TvShowSearch;

namespace MovieApp.IntegrationTests.Credits;

[Collection("MovieSearchApi")]
public sealed class MovieCreditsApiTests(MovieSearchApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetMovieCreditsReturnsCastAndCrewWithoutAuthentication()
    {
        await fixture.ResetAsync();

        var searchResponse = await _client.GetAsync("/api/movies/search?q=Interstellar");
        var searchPayload = await searchResponse.Content.ReadFromJsonAsync<MovieSearchResponse>();
        Assert.NotNull(searchPayload);

        var movieId = searchPayload.Items[0].Id;
        var creditsResponse = await _client.GetAsync($"/api/movies/{movieId}/credits");
        Assert.Equal(HttpStatusCode.OK, creditsResponse.StatusCode);

        var credits = await creditsResponse.Content.ReadFromJsonAsync<CreditsResponse>();
        Assert.NotNull(credits);
        Assert.Equal(3, credits.Cast.Count);
        Assert.Equal(3, credits.Crew.Count);
        Assert.Contains(credits.Crew, member => member.Name == "Christopher Nolan");

        var detailsResponse = await _client.GetAsync($"/api/movies/{movieId}");
        var details = await detailsResponse.Content.ReadFromJsonAsync<MovieDetailsResponse>();
        Assert.NotNull(details);
        Assert.Equal("Interstellar", details.Title);
    }

    [Fact]
    public async Task GetMovieCreditsReturnsNotFoundForUnknownMovie()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync($"/api/movies/{Guid.NewGuid():D}/credits");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

[Collection("TvShowSearchApi")]
public sealed class TvShowCreditsApiTests(TvShowSearchApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetTvShowCreditsReturnsAggregateCastAndCrew()
    {
        await fixture.ResetAsync();

        var searchResponse = await _client.GetAsync("/api/tvshows/search?q=breaking");
        var searchPayload = await searchResponse.Content.ReadFromJsonAsync<TvShowSearchResponse>();
        Assert.NotNull(searchPayload);

        var tvShowId = searchPayload.Items[0].Id;
        var creditsResponse = await _client.GetAsync($"/api/tvshows/{tvShowId}/credits");
        Assert.Equal(HttpStatusCode.OK, creditsResponse.StatusCode);

        var credits = await creditsResponse.Content.ReadFromJsonAsync<CreditsResponse>();
        Assert.NotNull(credits);
        Assert.Equal(2, credits.Cast.Count);
        Assert.Single(credits.Crew);
        Assert.Equal(62, credits.Cast[0].TotalEpisodeCount);
        Assert.NotNull(credits.Cast[0].Roles);

        var detailsResponse = await _client.GetAsync($"/api/tvshows/{tvShowId}");
        var details = await detailsResponse.Content.ReadFromJsonAsync<TvShowDetailsResponse>();
        Assert.NotNull(details);
        Assert.Equal(FakeTvShowDataProvider.BreakingBadTmdbId, details.ExternalIds.TmdbId);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MovieApp.Contracts.Auth;
using MovieApp.IntegrationTests.Auth;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Contracts.Search;

namespace MovieApp.IntegrationTests.AdvancedSearch;

[CollectionDefinition("AdvancedSearchApi")]
public sealed class AdvancedSearchApiTestsFixture : ICollectionFixture<AdvancedSearchApiFixture>;

[Collection("AdvancedSearchApi")]
public sealed class AdvancedSearchApiTests(AdvancedSearchApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task UnifiedSearchReturnsMoviesAndTvShows()
    {
        await fixture.ResetAsync();
        await SeedCatalogAsync();

        var response = await _client.GetAsync("/api/search?type=all&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.TotalCount);
        Assert.Contains(payload.Items, item => item.Type == "movie");
        Assert.Contains(payload.Items, item => item.Type == "tv");
    }

    [Fact]
    public async Task UnifiedSearchFiltersByTypeMovie()
    {
        await fixture.ResetAsync();
        await SeedCatalogAsync();

        var response = await _client.GetAsync("/api/search?q=inter&type=movie");
        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>();

        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal("movie", payload.Items[0].Type);
    }

    [Fact]
    public async Task AutocompleteReturnsSuggestions()
    {
        await fixture.ResetAsync();
        await SeedCatalogAsync();

        var response = await _client.GetAsync("/api/search/autocomplete?q=in");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<SearchAutocompleteResponse>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Items);
        Assert.True(payload.Items.Count <= 10);
        Assert.Contains(payload.Items, item => !string.IsNullOrWhiteSpace(item.PosterUrl));
    }

    [Fact]
    public async Task AutocompleteReturnsNullPosterUrlWhenCatalogItemHasNoPoster()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/search/autocomplete?q=posterless");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<SearchAutocompleteResponse>();
        Assert.NotNull(payload);
        Assert.Contains(payload.Items, item => item.Title == "Posterless Title" && item.PosterUrl is null);
    }

    [Fact]
    public async Task DiscoveryPopularReturnsResults()
    {
        await fixture.ResetAsync();
        await SeedCatalogAsync();

        var response = await _client.GetAsync("/api/discovery/popular?type=all&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.TotalCount);
    }

    [Fact]
    public async Task DiscoveryTrendingReturnsResults()
    {
        await fixture.ResetAsync();
        await SeedCatalogAsync();

        var response = await _client.GetAsync("/api/discovery/trending?type=all");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Items);
    }

    [Fact]
    public async Task SearchHistoryRecordsAndReturnsQueries()
    {
        await fixture.ResetAsync();
        await SeedCatalogAsync();

        var token = await RegisterAndGetTokenAsync("search-history");
        await SendAuthorizedGetAsync("/api/search?q=interstellar", token);
        await SendAuthorizedGetAsync("/api/search?q=interstellar", token);

        await using var context = CreateContext();
        Assert.Equal(1, await context.SearchHistories.CountAsync());

        var historyResponse = await SendAuthorizedGetAsync("/api/search/history?page=1&pageSize=20", token);
        var history = await historyResponse.Content.ReadFromJsonAsync<SearchHistoryResponse>();

        Assert.NotNull(history);
        Assert.Single(history.Items);
        Assert.Equal("interstellar", history.Items[0].Query);
    }

    [Fact]
    public async Task UserBCannotSeeUserASearchHistory()
    {
        await fixture.ResetAsync();
        await SeedCatalogAsync();

        var userAToken = await RegisterAndGetTokenAsync("history-owner");
        var userBToken = await RegisterAndGetTokenAsync("history-other");

        await SendAuthorizedGetAsync("/api/search?q=breaking", userAToken);

        var userBHistory = await SendAuthorizedGetAsync("/api/search/history", userBToken);
        var payload = await userBHistory.Content.ReadFromJsonAsync<SearchHistoryResponse>();

        Assert.NotNull(payload);
        Assert.Empty(payload.Items);
    }

    [Fact]
    public async Task SearchHistoryRequiresAuthentication()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/search/history");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidTypeReturnsBadRequest()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/search?q=batman&type=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidRatingRangeReturnsBadRequest()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/search?q=batman&minRating=9&maxRating=5");
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
        var movieResponse = await _client.GetAsync("/api/movies/search?q=Interstellar");
        Assert.Equal(HttpStatusCode.OK, movieResponse.StatusCode);

        var tvResponse = await _client.GetAsync("/api/tvshows/search?q=breaking");
        Assert.Equal(HttpStatusCode.OK, tvResponse.StatusCode);
    }

    private Task<string> RegisterAndGetTokenAsync(string prefix) =>
        AuthIntegrationHelpers.RegisterVerifyAndGetAccessTokenAsync(
            _client,
            fixture.Factory.Services,
            $"{prefix}-{Guid.NewGuid():N}@example.com");

    private Task<HttpResponseMessage> SendAuthorizedGetAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(AdvancedSearchIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MovieApp.Contracts.Genres;
using MovieApp.Contracts.Search;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.AdvancedSearch;

[Collection("AdvancedSearchApi")]
public sealed class DiscoverBrowseApiTests(AdvancedSearchApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GenresEndpointReturnsOrderedGenresFromDatabase()
    {
        await fixture.ResetAsync();
        await SeedGenresAsync();

        var response = await _client.GetAsync("/api/genres");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<List<GenreResponse>>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Count);
        Assert.Equal("Action", payload[0].Name);
        Assert.Equal("Drama", payload[1].Name);
    }

    [Fact]
    public async Task BrowseReturnsDefaultTrendingResultsForAllType()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/discovery/browse?mode=trending&type=all&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Items);
        Assert.Contains(payload.Items, item => item.Type == "movie");
        Assert.Contains(payload.Items, item => item.Type == "tv");
    }

    [Fact]
    public async Task BrowseSupportsMovieOnlyModeAndPaginationMetadata()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/discovery/browse?mode=top_rated&type=movie&page=1&pageSize=1");
        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>();

        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal("movie", payload.Items[0].Type);
        Assert.True(payload.TotalCount >= 1);
    }

    [Fact]
    public async Task BrowseNewReleasesExcludesFutureDatedFakeCatalogItems()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/discovery/browse?mode=new_releases&type=movie");
        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>();

        Assert.NotNull(payload);
        Assert.DoesNotContain(payload.Items, item => item.Title == "Discover Movie Future");
    }

    [Fact]
    public async Task BrowseInvalidModeReturnsBadRequest()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/discovery/browse?mode=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BrowseInvalidSortReturnsBadRequest()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/discovery/browse?mode=trending&sort=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BrowseRepeatedRequestsRemainStable()
    {
        await fixture.ResetAsync();

        var url = "/api/discovery/browse?mode=trending&type=movie&page=1&pageSize=20";
        var first = await _client.GetFromJsonAsync<SearchResponse>(url);
        var second = await _client.GetFromJsonAsync<SearchResponse>(url);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Items.Select(item => item.Id), second.Items.Select(item => item.Id));
    }

    private static async Task SeedGenresAsync()
    {
        await using var context = CreateContext();
        context.Genres.AddRange(
            new Genre { Id = Guid.NewGuid(), Name = "Drama", CreatedAt = DateTime.UtcNow },
            new Genre { Id = Guid.NewGuid(), Name = "Action", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(AdvancedSearchIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

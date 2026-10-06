using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Services.Search;
using MovieApp.Contracts.Discovery;
using MovieApp.Contracts.Search;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.AdvancedSearch;

[Collection("AdvancedSearchApi")]
public sealed class DiscoverBatchApiTests(AdvancedSearchApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GenreCoverCandidatesBatchReturnsOrderedItemsWithStatus()
    {
        await fixture.ResetAsync();
        var genres = await SeedGenresAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/discovery/genre-cover-candidates",
            new GenreCoverCandidatesBatchRequest(
                [genres[1].Id.ToString(), genres[0].Id.ToString()],
                "all",
                5));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<GenreCoverCandidatesBatchResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal(genres[1].Id.ToString(), payload.Items[0].GenreId);
        Assert.Equal(genres[0].Id.ToString(), payload.Items[1].GenreId);
        Assert.Equal("ok", payload.Items[0].Status);
    }

    [Fact]
    public async Task GenreCoverCandidatesBatchRejectsEmptyRequest()
    {
        await fixture.ResetAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/discovery/genre-cover-candidates",
            new GenreCoverCandidatesBatchRequest([], "all", 5));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenreCoverCandidatesBatchRejectsExcessiveBatchSize()
    {
        await fixture.ResetAsync();
        var ids = Enumerable.Range(0, DiscoveryBatchOrchestration.MaxGenreCoverBatchSize + 1)
            .Select(_ => Guid.NewGuid().ToString())
            .ToList();

        var response = await _client.PostAsJsonAsync(
            "/api/discovery/genre-cover-candidates",
            new GenreCoverCandidatesBatchRequest(ids, "all", 5));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProviderPreviewsBatchReturnsOrderedItemsWithStatus()
    {
        await fixture.ResetAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/discovery/provider-previews",
            new ProviderPreviewsBatchRequest([8, 119], "movie", "US", 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ProviderPreviewsBatchResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal(8, payload.Items[0].ProviderId);
        Assert.Equal(119, payload.Items[1].ProviderId);
        Assert.Equal("ok", payload.Items[0].Status);
    }

    [Fact]
    public async Task ProviderPreviewsBatchRejectsInvalidMediaType()
    {
        await fixture.ResetAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/discovery/provider-previews",
            new ProviderPreviewsBatchRequest([8], "all", "US", 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProviderPreviewsBatchRejectsExcessiveBatchSize()
    {
        await fixture.ResetAsync();
        var ids = Enumerable.Range(1, DiscoveryBatchOrchestration.MaxProviderPreviewBatchSize + 1).ToList();

        var response = await _client.PostAsJsonAsync(
            "/api/discovery/provider-previews",
            new ProviderPreviewsBatchRequest(ids, "movie", "US", 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BrowseAndAdvancedEndpointsRemainAvailable()
    {
        await fixture.ResetAsync();

        var browse = await _client.GetAsync("/api/discovery/browse?mode=trending&type=all&page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, browse.StatusCode);
        var browsePayload = await browse.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(browsePayload);

        var advanced = await _client.GetAsync(
            "/api/discovery/advanced?mediaType=movie&page=1&pageSize=1&watchRegion=US&watchProviderId=8&watchMonetizationType=stream");
        Assert.Equal(HttpStatusCode.OK, advanced.StatusCode);
        var advancedPayload = await advanced.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(advancedPayload);
    }

    private static async Task<List<Genre>> SeedGenresAsync()
    {
        await using var context = CreateContext();
        var action = new Genre { Id = Guid.NewGuid(), Name = "Action", CreatedAt = DateTime.UtcNow };
        var drama = new Genre { Id = Guid.NewGuid(), Name = "Drama", CreatedAt = DateTime.UtcNow };
        context.Genres.AddRange(action, drama);
        await context.SaveChangesAsync();
        return [action, drama];
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(AdvancedSearchIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

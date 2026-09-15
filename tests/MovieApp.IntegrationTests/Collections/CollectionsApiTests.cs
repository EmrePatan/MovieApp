using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MovieApp.Contracts.Collections;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.IntegrationTests.Collections;

[CollectionDefinition("CollectionsApi")]
public sealed class CollectionsApiTestsFixture : ICollectionFixture<CollectionsApiFixture>;

[Collection("CollectionsApi")]
public sealed class CollectionsApiTests(CollectionsApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetByTmdbIdReturnsCollectionAndMaterializesMovies()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync(
            $"/api/collections/{FakeCollectionDataProvider.SpaceOdysseyCollectionId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CollectionResponse>();
        Assert.NotNull(payload);
        Assert.Equal(FakeCollectionDataProvider.SpaceOdysseyCollectionId, payload.TmdbId);
        Assert.Equal("Space Odyssey Collection", payload.Name);
        Assert.Equal(3, payload.Parts.Count);
        Assert.All(payload.Parts, part => Assert.NotEqual(Guid.Empty, part.Id));

        await using var context = CreateContext();
        Assert.Equal(3, await context.Movies.CountAsync());
    }

    [Fact]
    public async Task GetByTmdbIdReturnsNotFoundForUnknownCollection()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync(
            $"/api/collections/{FakeCollectionDataProvider.UnknownCollectionId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(CollectionsIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

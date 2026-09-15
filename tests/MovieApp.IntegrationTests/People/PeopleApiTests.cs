using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MovieApp.Contracts.People;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.IntegrationTests.People;

[CollectionDefinition("PeopleApi")]
public sealed class PeopleApiTestsFixture : ICollectionFixture<PeopleApiFixture>;

[Collection("PeopleApi")]
public sealed class PeopleApiTests(PeopleApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetByTmdbIdReturnsPersonDetailAndPersistsPerson()
    {
        await PeopleApiFixture.ResetAsync();

        var response = await _client.GetAsync($"/api/people/tmdb/{FakePersonDataProvider.McConaugheyTmdbId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PersonDetailResponse>();
        Assert.NotNull(payload);
        Assert.Equal(FakePersonDataProvider.McConaugheyTmdbId, payload.TmdbId);
        Assert.Equal("Matthew McConaughey", payload.Name);
        Assert.Equal("Acting", payload.KnownForDepartment);
        Assert.Equal(2, payload.Filmography.Count);
        Assert.Equal("movie", payload.Filmography[0].MediaType);
        Assert.Null(payload.Filmography[0].CatalogId);
        Assert.Equal(FakeMovieDataProvider.InterstellarTmdbId, payload.Filmography[0].TmdbId);
        Assert.Equal("tv", payload.Filmography[1].MediaType);
        Assert.Null(payload.Filmography[1].CatalogId);

        await using var context = CreateContext();
        Assert.Equal(1, await context.People.CountAsync());
        Assert.Equal(0, await context.Movies.CountAsync());
        Assert.Equal(0, await context.TvShows.CountAsync());
    }

    [Fact]
    public async Task GetByTmdbIdReturnsNotFoundForUnknownPerson()
    {
        await PeopleApiFixture.ResetAsync();

        var response = await _client.GetAsync("/api/people/tmdb/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(PeopleIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

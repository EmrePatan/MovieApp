using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Contracts.Movies;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.IntegrationTests.MovieSearch;

[CollectionDefinition("MovieSearchApi")]
public sealed class MovieSearchApiTestsFixture : ICollectionFixture<MovieSearchApiFixture>;

[Collection("MovieSearchApi")]
public sealed class MovieSearchApiTests(MovieSearchApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task SearchInterstellarReturnsMovieAndPersistsIt()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/movies/search?q=Interstellar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<MovieSearchResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal(1, payload.Page);
        Assert.Equal(20, payload.PageSize);
        Assert.Equal(1, payload.TotalCount);
        Assert.Equal(1, payload.TotalPages);
        Assert.False(payload.HasNextPage);
        Assert.False(payload.HasPreviousPage);
        Assert.Equal("Interstellar", payload.Items[0].Title);
        Assert.Equal(FakeMovieDataProvider.InterstellarTmdbId, payload.Items[0].ExternalIds.TmdbId);

        await using var context = CreateContext();
        Assert.Equal(1, await context.Movies.CountAsync());
        Assert.Equal(3, await context.Genres.CountAsync());
        Assert.Equal(3, await context.MovieGenres.CountAsync());
    }

    [Fact]
    public async Task SearchWithExplicitPaginationReturnsSameResultAsDefault()
    {
        await fixture.ResetAsync();

        var defaultResponse = await _client.GetAsync("/api/movies/search?q=Interstellar");
        var explicitResponse = await _client.GetAsync("/api/movies/search?q=Interstellar&page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, explicitResponse.StatusCode);

        var defaultPayload = await defaultResponse.Content.ReadFromJsonAsync<MovieSearchResponse>();
        var explicitPayload = await explicitResponse.Content.ReadFromJsonAsync<MovieSearchResponse>();

        Assert.NotNull(defaultPayload);
        Assert.NotNull(explicitPayload);
        Assert.Equal(defaultPayload.Items[0].Title, explicitPayload.Items[0].Title);
        Assert.Equal(defaultPayload.Page, explicitPayload.Page);
        Assert.Equal(defaultPayload.PageSize, explicitPayload.PageSize);
        Assert.Equal(defaultPayload.TotalCount, explicitPayload.TotalCount);
    }

    [Fact]
    public async Task RepeatedSearchDoesNotCreateDuplicateMovieAndUsesCache()
    {
        await fixture.ResetAsync();

        var firstResponse = await _client.GetAsync("/api/movies/search?q=Interstellar");
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var tracker = scope.ServiceProvider.GetRequiredService<MovieDataProviderCallTracker>();
            Assert.Equal(1, tracker.SearchMoviesCallCount);
        }

        var secondResponse = await _client.GetAsync("/api/movies/search?q= Interstellar ");
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        await using var context = CreateContext();
        Assert.Equal(1, await context.Movies.CountAsync());

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var tracker = scope.ServiceProvider.GetRequiredService<MovieDataProviderCallTracker>();
            Assert.Equal(1, tracker.SearchMoviesCallCount);
        }
    }

    [Fact]
    public async Task DifferentPaginationParametersUseDistinctCacheEntries()
    {
        await fixture.ResetAsync();

        var pageOneResponse = await _client.GetAsync("/api/movies/search?q=Interstellar&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, pageOneResponse.StatusCode);

        var pageTwoResponse = await _client.GetAsync("/api/movies/search?q=Interstellar&page=2&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, pageTwoResponse.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var tracker = scope.ServiceProvider.GetRequiredService<MovieDataProviderCallTracker>();
            Assert.Equal(2, tracker.SearchMoviesCallCount);
        }
    }

    [Fact]
    public async Task GetMovieByIdReturnsPersistedMovieDetails()
    {
        await fixture.ResetAsync();

        var searchResponse = await _client.GetAsync("/api/movies/search?q=Interstellar");
        var searchPayload = await searchResponse.Content.ReadFromJsonAsync<MovieSearchResponse>();
        Assert.NotNull(searchPayload);

        var movieId = searchPayload.Items[0].Id;
        var detailsResponse = await _client.GetAsync($"/api/movies/{movieId}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var details = await detailsResponse.Content.ReadFromJsonAsync<MovieDetailsResponse>();
        Assert.NotNull(details);
        Assert.Equal(movieId, details.Id);
        Assert.Equal("Interstellar", details.Title);
        Assert.Equal(3, details.Genres.Count);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("")]
    public async Task SearchWithInvalidQueryReturnsBadRequest(string query)
    {
        var response = await _client.GetAsync($"/api/movies/search?q={query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task SearchWithInvalidPaginationReturnsBadRequest(string paginationQuery)
    {
        var response = await _client.GetAsync($"/api/movies/search?q=Interstellar&{paginationQuery}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchWithMultipleEmptyImdbIdsReturnsOkAndPersistsNullImdbValues()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync(
            $"/api/movies/search?q={FakeMovieDataProvider.EmptyImdbCatalogQueryToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<MovieSearchResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal(2, payload.TotalCount);
        Assert.All(payload.Items, item => Assert.Null(item.ExternalIds.ImdbId));

        await using var context = CreateContext();
        Assert.Equal(2, await context.Movies.CountAsync());
        Assert.Equal(0, await context.Movies.CountAsync(movie => movie.ImdbId == string.Empty));
        Assert.Equal(2, await context.Movies.CountAsync(movie => movie.ImdbId == null));
    }

    [Fact]
    public async Task SearchWithLegacyEmptyImdbRowAndMultipleEmptyImdbResultsReturnsOk()
    {
        await fixture.ResetAsync();

        await using (var context = CreateContext())
        {
            var utcNow = DateTime.UtcNow;
            context.Movies.Add(new Movie
            {
                Id = Guid.NewGuid(),
                TmdbId = 999001,
                ImdbId = string.Empty,
                Title = "Legacy Empty IMDb Row",
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
            await context.SaveChangesAsync();
        }

        var response = await _client.GetAsync(
            $"/api/movies/search?q={FakeMovieDataProvider.EmptyImdbCatalogQueryToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<MovieSearchResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Items.Count);
    }

    [Fact]
    public async Task SearchSkipsDuplicateImdbConflictAndReturnsRemainingResults()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync(
            $"/api/movies/search?q={FakeMovieDataProvider.DuplicateImdbCatalogQueryToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<MovieSearchResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal(FakeMovieDataProvider.DuplicateImdbMovieOneTmdbId, payload.Items[0].ExternalIds.TmdbId);
        Assert.Equal(2, payload.TotalCount);

        await using var context = CreateContext();
        Assert.Equal(1, await context.Movies.CountAsync(movie =>
            movie.ImdbId == FakeMovieDataProvider.DuplicateImdbMovieImdbId));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(MovieSearchIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

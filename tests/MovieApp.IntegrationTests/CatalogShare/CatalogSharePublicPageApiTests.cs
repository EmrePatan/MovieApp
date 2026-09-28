using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MovieApp.Contracts.Movies;
using MovieApp.Contracts.TvShows;
using MovieApp.IntegrationTests.MovieSearch;

namespace MovieApp.IntegrationTests.CatalogShare;

[Collection("MovieSearchApi")]
public sealed class CatalogSharePublicPageApiTests(MovieSearchApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetMovie_ReturnsHtml_WithTitleAndOpenGraph()
    {
        await fixture.ResetAsync();
        var movieId = await SeedMovieIdAsync();
        var response = await _client.GetAsync($"/movie/{movieId:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<!DOCTYPE html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("og:title", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("og:image", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Movie Cave", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTv_ReturnsHtml_WithTitleAndOpenGraph()
    {
        await fixture.ResetAsync();
        var tvId = await SeedTvIdAsync();
        var response = await _client.GetAsync($"/tv/{tvId:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("og:title", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("og:description", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMovie_InvalidId_ReturnsSafeNotFoundHtml()
    {
        var response = await _client.GetAsync($"/movie/{Guid.Empty:D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<!DOCTYPE html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AppleAppSiteAssociation_ReturnsJson()
    {
        var response = await _client.GetAsync("/.well-known/apple-app-site-association");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty("applinks", out _));
    }

    [Fact]
    public async Task AssetLinks_ReturnsJsonArray()
    {
        var response = await _client.GetAsync("/.well-known/assetlinks.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
    }

    private async Task<Guid> SeedMovieIdAsync()
    {
        var response = await _client.GetAsync("/api/movies/search?q=Interstellar");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<MovieSearchResponse>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Items);
        return payload.Items[0].Id;
    }

    private async Task<Guid> SeedTvIdAsync()
    {
        var response = await _client.GetAsync("/api/tvshows/search?q=breaking");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<TvShowSearchResponse>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Items);
        return payload.Items[0].Id;
    }
}

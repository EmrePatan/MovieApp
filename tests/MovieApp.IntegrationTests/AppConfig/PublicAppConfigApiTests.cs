using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MovieApp.Contracts.AppConfig;

namespace MovieApp.IntegrationTests.AppConfig;

public sealed class PublicAppConfigApiTests : IClassFixture<MovieAppWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PublicAppConfigApiTests(MovieAppWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetReturnsAnonymous200WithCamelCaseJson()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/app-config");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Authorization"));

        Assert.True(response.Headers.CacheControl?.Public);
        Assert.Equal(TimeSpan.FromSeconds(60), response.Headers.CacheControl?.MaxAge);

        await using var jsonStream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(jsonStream);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("maintenance", out var maintenance));
        Assert.True(maintenance.TryGetProperty("enabled", out var enabled));
        Assert.False(enabled.GetBoolean());

        Assert.True(root.TryGetProperty("versions", out var versions));
        Assert.True(versions.TryGetProperty("ios", out var ios));
        Assert.Equal(9, ios.GetProperty("minimumBuild").GetInt32());
        Assert.Equal(9, ios.GetProperty("latestBuild").GetInt32());

        Assert.True(root.TryGetProperty("features", out var features));
        Assert.True(features.GetProperty("aiRecommendations").GetBoolean());
        Assert.True(features.GetProperty("reviewTranslation").GetBoolean());
    }

    [Fact]
    public async Task GetDeserializesToContractTypes()
    {
        var payload = await _client.GetFromJsonAsync<AppConfigResponse>("/api/public/app-config");

        Assert.NotNull(payload);
        Assert.False(payload.Maintenance.Enabled);
        Assert.True(payload.Features.AiRecommendations);
        Assert.True(payload.Features.ReviewTranslation);
        Assert.Equal(2, payload.Versions.Android.MinimumBuild);
    }
}

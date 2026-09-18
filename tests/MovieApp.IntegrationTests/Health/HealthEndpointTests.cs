using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MovieApp.Contracts.Health;

namespace MovieApp.IntegrationTests.Health;

public sealed class HealthEndpointTests : IClassFixture<MovieAppWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(MovieAppWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealthReturnsHealthyResponse()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Healthy", payload.Status);
        Assert.False(string.IsNullOrWhiteSpace(payload.Environment));
        Assert.False(string.IsNullOrWhiteSpace(payload.SourceVersion));
    }

    [Fact]
    public async Task GetLiveReturnsHealthyResponse()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Healthy", payload.Status);
    }
}

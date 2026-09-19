using System.Net;

namespace MovieApp.IntegrationTests.Email;

public sealed class EmailAssetEndpointTests : IClassFixture<MovieAppWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EmailAssetEndpointTests(MovieAppWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task VerificationHeroAssetIsPublicAndCacheable()
    {
        using var response = await _client.GetAsync("/email-assets/verification-hero.jpg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("max-age=31536000", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.Contains("immutable", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 10_000);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }
}

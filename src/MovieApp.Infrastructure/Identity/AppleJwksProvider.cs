using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace MovieApp.Infrastructure.Identity;

public sealed class AppleJwksProvider(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
{
    private const string CacheKey = "apple-signin-jwks";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);
    private static readonly Uri AppleKeysEndpoint = new("https://appleid.apple.com/auth/keys");

    public async Task<IList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(CacheKey, out JsonWebKeySet? cachedKeys) && cachedKeys is not null)
        {
            return cachedKeys.GetSigningKeys();
        }

        var httpClient = httpClientFactory.CreateClient(nameof(AppleJwksProvider));
        using var response = await httpClient.GetAsync(AppleKeysEndpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);
        var keysJson = document.RootElement.GetRawText();
        var keySet = new JsonWebKeySet(keysJson);

        memoryCache.Set(CacheKey, keySet, CacheDuration);
        return keySet.GetSigningKeys();
    }
}

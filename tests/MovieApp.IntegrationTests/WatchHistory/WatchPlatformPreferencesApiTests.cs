using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MovieApp.Application.Abstractions.Preferences;
using MovieApp.IntegrationTests.Auth;

namespace MovieApp.IntegrationTests.WatchHistory;

[Collection("WatchHistoryApi")]
public sealed class WatchPlatformPreferencesApiTests(WatchHistoryApiFixture fixture)
{
    private const string Path = "/api/watch-platforms";
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task SelectionIsEmptyByDefaultThenSavedReplacedAndCleared()
    {
        await fixture.ResetAsync();
        var token = await RegisterAsync("platforms");

        Assert.Empty(await GetIdsAsync(token));

        Assert.Equal(HttpStatusCode.OK, (await PutAsync(token, [337, 8])).StatusCode);
        Assert.Equal([8, 337], await GetIdsAsync(token));

        Assert.Equal(HttpStatusCode.OK, (await PutAsync(token, [119])).StatusCode);
        Assert.Equal([119], await GetIdsAsync(token));

        Assert.Equal(HttpStatusCode.OK, (await PutAsync(token, [])).StatusCode);
        Assert.Empty(await GetIdsAsync(token));
    }

    [Fact]
    public async Task SelectionBelongsToOneUser()
    {
        await fixture.ResetAsync();
        var first = await RegisterAsync("platforms-first");
        var second = await RegisterAsync("platforms-second");

        await PutAsync(first, [8]);

        Assert.Empty(await GetIdsAsync(second));
    }

    [Fact]
    public async Task InvalidOrAnonymousRequestsAreRejected()
    {
        await fixture.ResetAsync();
        var token = await RegisterAsync("platforms-invalid");

        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(token, [8, 8])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(token, [0])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(token, Enumerable.Range(1, 21).ToArray())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(token, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(Path)).StatusCode);
    }

    private Task<string> RegisterAsync(string prefix) =>
        AuthIntegrationHelpers.RegisterVerifyAndGetAccessTokenAsync(
            _client,
            fixture.Factory.Services,
            $"{prefix}-{Guid.NewGuid():N}@example.com");

    private async Task<IReadOnlyList<int>> GetIdsAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<WatchPlatformPreferencesResult>();
        Assert.NotNull(payload);
        return payload.ProviderIds;
    }

    private async Task<HttpResponseMessage> PutAsync(string token, int[]? providerIds)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, Path)
        {
            Content = JsonContent.Create(new { providerIds }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}

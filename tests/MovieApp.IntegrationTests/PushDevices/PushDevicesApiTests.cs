using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MovieApp.Contracts.Auth;
using MovieApp.IntegrationTests.Auth;
using MovieApp.Contracts.PushDevices;

namespace MovieApp.IntegrationTests.PushDevices;

[CollectionDefinition("PushDevicesApi")]
public sealed class PushDevicesApiTestsDefinition : ICollectionFixture<PushDevicesFixture>;

[Collection("PushDevicesApi")]
public sealed class PushDevicesApiTests(PushDevicesFixture fixture)
{
    private const string ValidTokenA = "ExponentPushToken[aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa]";
    private const string ValidTokenB = "ExponentPushToken[bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb]";
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task AuthenticatedUserRegistersToken()
    {
        await PushDevicesFixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("push-user-a");
        var response = await SendAuthorizedPutAsync(token, ValidTokenA, "ios");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var context = PushDevicesFixture.CreateContext();
        var device = await context.PushDevices.SingleAsync();
        Assert.True(device.IsActive);
        Assert.Equal("Ios", device.Platform.ToString());
    }

    [Fact]
    public async Task RepeatedRegistrationIsIdempotent()
    {
        await PushDevicesFixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("push-user-repeat");
        await SendAuthorizedPutAsync(token, ValidTokenA, "ios");
        await SendAuthorizedPutAsync(token, ValidTokenA, "android");

        await using var context = PushDevicesFixture.CreateContext();
        Assert.Equal(1, await context.PushDevices.CountAsync());
        var device = await context.PushDevices.SingleAsync();
        Assert.Equal("Android", device.Platform.ToString());
        Assert.True(device.IsActive);
    }

    [Fact]
    public async Task SameTokenCannotRemainActiveForTwoUsers()
    {
        await PushDevicesFixture.ResetAsync();

        var tokenA = await RegisterAndGetTokenAsync("push-user-one");
        var tokenB = await RegisterAndGetTokenAsync("push-user-two");

        await SendAuthorizedPutAsync(tokenA, ValidTokenA, "ios");
        await SendAuthorizedPutAsync(tokenB, ValidTokenA, "android");

        await using var context = PushDevicesFixture.CreateContext();
        Assert.Equal(1, await context.PushDevices.CountAsync());
        var device = await context.PushDevices.SingleAsync();
        Assert.Equal("Android", device.Platform.ToString());

        var userBId = await GetUserIdForEmailAsync("push-user-two@example.com");
        Assert.Equal(userBId, device.UserId);
    }

    [Fact]
    public async Task TokenCanBeReassociatedSafely()
    {
        await PushDevicesFixture.ResetAsync();

        var tokenA = await RegisterAndGetTokenAsync("push-reassign-a");
        var tokenB = await RegisterAndGetTokenAsync("push-reassign-b");

        await SendAuthorizedPutAsync(tokenA, ValidTokenB, "ios");
        await SendAuthorizedPutAsync(tokenB, ValidTokenB, "android");

        await using var context = PushDevicesFixture.CreateContext();
        var device = await context.PushDevices.SingleAsync(d => d.ExpoPushToken == ValidTokenB);
        var userBId = await GetUserIdForEmailAsync("push-reassign-b@example.com");
        Assert.Equal(userBId, device.UserId);
        Assert.True(device.IsActive);
    }

    [Fact]
    public async Task UnregisterIsIdempotent()
    {
        await PushDevicesFixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("push-unregister");
        await SendAuthorizedPutAsync(token, ValidTokenA, "ios");

        var firstDelete = await SendAuthorizedDeleteAsync(token, ValidTokenA);
        var secondDelete = await SendAuthorizedDeleteAsync(token, ValidTokenA);

        Assert.Equal(HttpStatusCode.NoContent, firstDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, secondDelete.StatusCode);

        await using var context = PushDevicesFixture.CreateContext();
        var device = await context.PushDevices.SingleAsync();
        Assert.False(device.IsActive);
    }

    [Fact]
    public async Task UserCannotUnregisterAnotherUsersToken()
    {
        await PushDevicesFixture.ResetAsync();

        var tokenA = await RegisterAndGetTokenAsync("push-owner");
        var tokenB = await RegisterAndGetTokenAsync("push-intruder");

        await SendAuthorizedPutAsync(tokenA, ValidTokenA, "ios");
        await SendAuthorizedDeleteAsync(tokenB, ValidTokenA);

        await using var context = PushDevicesFixture.CreateContext();
        var device = await context.PushDevices.SingleAsync();
        Assert.True(device.IsActive);
    }

    [Fact]
    public async Task InvalidTokenIsRejected()
    {
        await PushDevicesFixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("push-invalid");
        var response = await SendAuthorizedPutAsync(token, "bad-token", "ios");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InactiveTokenCanBeReactivated()
    {
        await PushDevicesFixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("push-reactivate");
        await SendAuthorizedPutAsync(token, ValidTokenA, "ios");
        await SendAuthorizedDeleteAsync(token, ValidTokenA);
        await SendAuthorizedPutAsync(token, ValidTokenA, "android");

        await using var context = PushDevicesFixture.CreateContext();
        var device = await context.PushDevices.SingleAsync();
        Assert.True(device.IsActive);
        Assert.Equal("Android", device.Platform.ToString());
    }

    private Task<string> RegisterAndGetTokenAsync(string username) =>
        AuthIntegrationHelpers.RegisterVerifyAndGetAccessTokenAsync(
            _client,
            fixture.Factory.Services,
            $"{username}@example.com");

    private async Task<HttpResponseMessage> SendAuthorizedPutAsync(
        string accessToken,
        string expoPushToken,
        string platform)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/push-devices")
        {
            Content = JsonContent.Create(new RegisterPushDeviceRequest(expoPushToken, platform))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedDeleteAsync(
        string accessToken,
        string expoPushToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/push-devices")
        {
            Content = JsonContent.Create(new UnregisterPushDeviceRequest(expoPushToken))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private static async Task<Guid> GetUserIdForEmailAsync(string email)
    {
        await using var context = PushDevicesFixture.CreateContext();
        return await context.Users
            .Where(user => user.Email == email)
            .Select(user => user.Id)
            .SingleAsync();
    }
}

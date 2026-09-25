using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Contracts.AiRecommendations;
using MovieApp.Contracts.Auth;
using MovieApp.Infrastructure.Email;
using MovieApp.Infrastructure.Persistence;
using MovieApp.IntegrationTests.Auth;

namespace MovieApp.IntegrationTests.AiRecommendations;

[CollectionDefinition("AiRecommendationsApi")]
public sealed class AiRecommendationsApiTestsFixture : ICollectionFixture<AiRecommendationsApiFixture>;

[Collection("AiRecommendationsApi")]
public sealed class AiRecommendationsApiTests(AiRecommendationsApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task PostReturnsUnauthorizedWithoutToken()
    {
        fixture.Factory.ResetTestState();

        var response = await _client.PostAsJsonAsync(
            "/api/ai/recommendations",
            new AiRecommendationRequest("mystery movie please", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostReturnsSuccessfulAiResponseForAuthenticatedUser()
    {
        fixture.Factory.ResetTestState();

        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedAsync(
            new AiRecommendationRequest("mystery movie with a twist", null),
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AiRecommendationResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.IsAiGenerated);
        Assert.Equal(1, payload.ReturnedCount);
        Assert.Equal(2, payload.QuotaRemaining);
        Assert.Equal("Arrival", payload.Recommendations[0].Title);
    }

    [Fact]
    public async Task PostReturns422WhenValidationProducesZeroResults()
    {
        fixture.Factory.ResetTestState();
        fixture.Factory.ProviderResult = new AiProviderGenerationResult(
            [new AiProviderSuggestion("Unknown", 2099, "movie", null, "Reason")],
            null);
        fixture.Factory.ValidatorResult = new AiValidationResult([], 1, 0, 1, false);

        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedAsync(
            new AiRecommendationRequest("impossible match request", null),
            token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("isAiGenerated").GetBoolean());
        Assert.Equal(0, json.GetProperty("returnedCount").GetInt32());
        Assert.Equal(2, json.GetProperty("quotaRemaining").GetInt32());
    }

    [Fact]
    public async Task PostReturns429WhenDailyQuotaIsExhausted()
    {
        fixture.Factory.ResetTestState();

        var token = await RegisterAndGetTokenAsync();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var success = await SendAuthorizedAsync(
                new AiRecommendationRequest($"mystery movie attempt {attempt}", null),
                token);
            success.EnsureSuccessStatusCode();
        }

        var response = await SendAuthorizedAsync(
            new AiRecommendationRequest("one more mystery movie", null),
            token);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(AiRecommendationQuotaMessages.DailyLimitTitle, problem.GetProperty("title").GetString());
        Assert.Contains(
            "3 AI recommendation requests",
            problem.GetProperty("detail").GetString(),
            StringComparison.Ordinal);
        Assert.Contains("tomorrow", problem.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostReturns500WithoutConsumingQuotaWhenValidationThrows()
    {
        fixture.Factory.ResetTestState();
        fixture.Factory.ValidatorShouldThrow = true;

        var token = await RegisterAndGetTokenAsync();

        var failed = await SendAuthorizedAsync(
            new AiRecommendationRequest("mystery movie please", null),
            token);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);

        fixture.Factory.ValidatorShouldThrow = false;

        var retry = await SendAuthorizedAsync(
            new AiRecommendationRequest("mystery movie retry", null),
            token);
        retry.EnsureSuccessStatusCode();

        var payload = await retry.Content.ReadFromJsonAsync<AiRecommendationResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.QuotaRemaining);
    }

    [Fact]
    public async Task PostReturns503WithoutConsumingQuotaWhenProviderUnavailable()
    {
        fixture.Factory.ResetTestState();
        fixture.Factory.ProviderShouldFail = true;

        var token = await RegisterAndGetTokenAsync();

        var failed = await SendAuthorizedAsync(
            new AiRecommendationRequest("mystery movie please", null),
            token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);

        fixture.Factory.ProviderShouldFail = false;

        var retry = await SendAuthorizedAsync(
            new AiRecommendationRequest("mystery movie retry", null),
            token);
        retry.EnsureSuccessStatusCode();

        var payload = await retry.Content.ReadFromJsonAsync<AiRecommendationResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.QuotaRemaining);
    }

    private Task<string> RegisterAndGetTokenAsync() =>
        AuthIntegrationHelpers.RegisterVerifyAndGetAccessTokenAsync(
            _client,
            fixture.Factory.Services,
            $"ai-user-{Guid.NewGuid():N}@example.com");

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        AiRecommendationRequest request,
        string token)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/ai/recommendations")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(message);
    }
}

public sealed class AiRecommendationsApiFixture : IAsyncLifetime
{
    public AiRecommendationsWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        Factory.Dispose();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(AiRecommendationsIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

public sealed class AiRecommendationsWebApplicationFactory : WebApplicationFactory<Program>
{
    public AiProviderGenerationResult ProviderResult { get; set; } = CreateDefaultProviderResult();

    public AiValidationResult ValidatorResult { get; set; } = CreateDefaultValidatorResult();

    public bool ProviderShouldFail { get; set; }

    public bool ValidatorShouldThrow { get; set; }

    public void ResetTestState()
    {
        ProviderResult = CreateDefaultProviderResult();
        ValidatorResult = CreateDefaultValidatorResult();
        ProviderShouldFail = false;
        ValidatorShouldThrow = false;
    }

    private static AiProviderGenerationResult CreateDefaultProviderResult() => new(
        [new AiProviderSuggestion("Arrival", 2016, "movie", 329996, "Mind-bending sci-fi")],
        null);

    private static AiValidationResult CreateDefaultValidatorResult() => new(
        [
            new AiValidatedRecommendation(
                new ResolvedMovieIdentity(
                    "movie",
                    Guid.NewGuid(),
                    329996,
                    "Arrival",
                    2016,
                    116,
                    "Arrival",
                    "Overview",
                    "/poster.jpg",
                    null,
                    new DateOnly(2016, 1, 1),
                    7.8m,
                    1000,
                    ["Science Fiction"]),
                "Mind-bending sci-fi")
        ],
        1,
        1,
        0,
        false);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            AiRecommendationsIntegrationDatabase.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] =
                AiRecommendationsIntegrationDatabase.GetConnectionString();
            configuration["Redis:ConnectionString"] = string.Empty;
            configuration["MovieProviders:Provider"] = "Fake";
            configuration["AiRecommendations:UserDailyMessageLimit"] = "3";
            configuration["AiRecommendations:Gemini:Enabled"] = "true";
            configurationBuilder.AddInMemoryCollection(configuration);
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(this);
            services.AddSingleton<CapturingEmailSender>();
            services.AddSingleton<IEmailSender>(provider => provider.GetRequiredService<CapturingEmailSender>());

            services.AddSingleton<IAiMovieRecommendationProvider, TestAiProvider>();
            services.AddSingleton<IAiMovieRecommendationValidator, TestAiValidator>();
        });
    }

    private sealed class TestAiProvider(AiRecommendationsWebApplicationFactory factory) : IAiMovieRecommendationProvider
    {
        public Task<AiMovieRecommendationProviderOutcome> GenerateAsync(
            AiProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            if (factory.ProviderShouldFail)
            {
                throw new MovieApp.Application.Exceptions.AiRecommendationProviderException("Provider failed.");
            }

            return Task.FromResult(new AiMovieRecommendationProviderOutcome(
                factory.ProviderResult,
                true,
                "test"));
        }
    }

    private sealed class TestAiValidator(AiRecommendationsWebApplicationFactory factory) : IAiMovieRecommendationValidator
    {
        public Task<AiValidationResult> ValidateAsync(
            Guid userId,
            IReadOnlyList<AiProviderSuggestion> suggestions,
            AiRecommendationSessionState session,
            int maxReturnedCount,
            CancellationToken cancellationToken = default)
        {
            if (factory.ValidatorShouldThrow)
            {
                throw new InvalidOperationException("Validation infrastructure failure.");
            }

            return Task.FromResult(factory.ValidatorResult);
        }
    }
}

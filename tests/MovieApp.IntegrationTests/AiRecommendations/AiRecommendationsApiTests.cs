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
    public async Task PostReturnsForbiddenWhenNotEntitled()
    {
        fixture.Factory.ResetTestState();

        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedAsync(
            new AiRecommendationRequest("mystery movie please", null),
            token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostReturnsSuccessfulAiResponseForEntitledUser()
    {
        fixture.Factory.ResetTestState();
        fixture.Factory.AllowEntitlement = true;

        var token = await RegisterAndGetTokenAsync();

        var response = await SendAuthorizedAsync(
            new AiRecommendationRequest("mystery movie with a twist", null),
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AiRecommendationResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.IsAiGenerated);
        Assert.Equal(1, payload.ReturnedCount);
        Assert.Equal("Arrival", payload.Recommendations[0].Title);
    }

    [Fact]
    public async Task PostReturns422WhenValidationProducesZeroResults()
    {
        fixture.Factory.ResetTestState();
        fixture.Factory.AllowEntitlement = true;
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
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"ai-user-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", "AI User"));

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return payload!.AccessToken;
    }

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
            .UseNpgsql(AuthIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

public sealed class AiRecommendationsWebApplicationFactory : WebApplicationFactory<Program>
{
    public bool AllowEntitlement { get; set; }

    public AiProviderGenerationResult ProviderResult { get; set; } = CreateDefaultProviderResult();

    public AiValidationResult ValidatorResult { get; set; } = CreateDefaultValidatorResult();

    public void ResetTestState()
    {
        AllowEntitlement = false;
        ProviderResult = CreateDefaultProviderResult();
        ValidatorResult = CreateDefaultValidatorResult();
    }

    private static AiProviderGenerationResult CreateDefaultProviderResult() => new(
        [new AiProviderSuggestion("Arrival", 2016, "movie", 329996, "Mind-bending sci-fi")],
        null);

    private static AiValidationResult CreateDefaultValidatorResult() => new(
        [
            new AiValidatedRecommendation(
                new ResolvedMovieIdentity(
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
            AuthIntegrationDatabase.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] = AuthIntegrationDatabase.GetConnectionString();
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
            services.AddSingleton<IAiRecommendationEntitlementService, TestEntitlementService>();
        });
    }

    private sealed class TestAiProvider(AiRecommendationsWebApplicationFactory factory) : IAiMovieRecommendationProvider
    {
        public Task<AiProviderGenerationResult> GenerateAsync(
            AiProviderRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(factory.ProviderResult);
    }

    private sealed class TestAiValidator(AiRecommendationsWebApplicationFactory factory) : IAiMovieRecommendationValidator
    {
        public Task<AiValidationResult> ValidateAsync(
            Guid userId,
            IReadOnlyList<AiProviderSuggestion> suggestions,
            AiRecommendationSessionState session,
            int maxReturnedCount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(factory.ValidatorResult);
    }

    private sealed class TestEntitlementService(AiRecommendationsWebApplicationFactory factory)
        : IAiRecommendationEntitlementService
    {
        public Task EnsurePremiumEntitledAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (factory.AllowEntitlement)
            {
                return Task.CompletedTask;
            }

            throw new MovieApp.Application.Exceptions.AiRecommendationEntitlementException("Premium required.");
        }
    }
}

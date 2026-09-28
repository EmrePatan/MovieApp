using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MovieApp.Infrastructure.Persistence;
using MovieApp.IntegrationTests.Auth;

namespace MovieApp.IntegrationTests.AdvancedSearch;

public sealed class SearchAutocompleteRateLimitWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            AdvancedSearchIntegrationDatabase.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] = AdvancedSearchIntegrationDatabase.GetConnectionString();
            configuration["Redis:ConnectionString"] = string.Empty;
            configuration["Redis:InstanceName"] = "MovieApp:";
            configuration["MovieProviders:Provider"] = "Fake";
            configuration["Search:RateLimit:AutocompletePermitLimit"] = "2";
            configuration["Search:RateLimit:AutocompleteWindowMinutes"] = "1";
            configurationBuilder.AddInMemoryCollection(configuration);
        });
    }
}

[CollectionDefinition("SearchAutocompleteRateLimitApi")]
public sealed class SearchAutocompleteRateLimitApiTestsFixture
    : ICollectionFixture<SearchAutocompleteRateLimitApiFixture>;

public sealed class SearchAutocompleteRateLimitApiFixture : IAsyncLifetime
{
    public SearchAutocompleteRateLimitWebApplicationFactory Factory { get; } = new();

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
            .UseNpgsql(AdvancedSearchIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

[Collection("SearchAutocompleteRateLimitApi")]
public sealed class SearchAutocompleteRateLimitApiTests(SearchAutocompleteRateLimitApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task AutocompleteReturns429AfterConfiguredLimit()
    {
        var first = await _client.GetAsync("/api/search/autocomplete?q=in");
        var second = await _client.GetAsync("/api/search/autocomplete?q=inter");
        var third = await _client.GetAsync("/api/search/autocomplete?q=int");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.True(third.Headers.Contains("Retry-After"));
    }
}

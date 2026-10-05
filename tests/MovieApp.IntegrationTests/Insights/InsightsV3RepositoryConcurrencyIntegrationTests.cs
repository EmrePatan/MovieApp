using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Insights;

public sealed class InsightsV3RepositoryConcurrencyIntegrationTests
{
    [Theory]
    [InlineData(11)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task GetV3RawDataAsyncPreservesSemanticsAndCommandCountsForConfiguredConcurrency(
        int maxRepositoryConcurrency)
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        context.Users.Add(new User
        {
            Id = userId,
            Email = $"v3-concurrency-{userId:N}@example.com",
            NormalizedEmail = $"v3-concurrency-{userId:N}@example.com".ToUpperInvariant(),
            UserName = $"v3-concurrency-{userId:N}",
            DisplayName = "Concurrency User",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        var repository = CreateInsightsRepository(context, maxRepositoryConcurrency);
        var timeZone = InsightsTimeZoneGuard.RequireValidTimeZone("Europe/Istanbul");
        var (raw, metrics) = await repository.GetV3RawDataAsync(userId, timeZone, 2026);

        Assert.Equal(11, metrics.DbRoundTrips);
        Assert.Equal(11, metrics.PgCommandRoundTrips);
        Assert.NotNull(raw);
    }

    [Fact]
    public async Task GetV3RawDataAsyncReturnsIdenticalPayloadAcrossConcurrencySettings()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        context.Users.Add(new User
        {
            Id = userId,
            Email = $"v3-parity-{userId:N}@example.com",
            NormalizedEmail = $"v3-parity-{userId:N}@example.com".ToUpperInvariant(),
            UserName = $"v3-parity-{userId:N}",
            DisplayName = "Parity User",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        var timeZone = InsightsTimeZoneGuard.RequireValidTimeZone("Europe/Istanbul");
        var baseline = await repositoryRaw(context, 11, userId, timeZone);
        var sequential = await repositoryRaw(context, 1, userId, timeZone);
        var boundedTwo = await repositoryRaw(context, 2, userId, timeZone);

        Assert.Equal(Fingerprint(baseline), Fingerprint(sequential));
        Assert.Equal(Fingerprint(baseline), Fingerprint(boundedTwo));
    }

    [Fact]
    public void OptionsBindingDefaultsToElevenWhenSectionMissing()
    {
        var options = new InsightsV3Options();
        Assert.Equal(InsightsV3Options.DefaultMaxRepositoryConcurrency, options.MaxRepositoryConcurrency);
    }

    private static async Task<InsightsV3RawData> repositoryRaw(
        ApplicationDbContext context,
        int maxRepositoryConcurrency,
        Guid userId,
        TimeZoneInfo timeZone)
    {
        var repository = CreateInsightsRepository(context, maxRepositoryConcurrency);
        var (raw, _) = await repository.GetV3RawDataAsync(userId, timeZone, 2026);
        return raw;
    }

    private static string Fingerprint(InsightsV3RawData raw)
    {
        var json = JsonSerializer.Serialize(raw);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(IntegrationTestDatabase.GetConnectionString("movieapp_insights_v3_concurrency_tests"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static InsightsRepository CreateInsightsRepository(
        ApplicationDbContext context,
        int maxRepositoryConcurrency)
    {
        var connectionString = IntegrationTestDatabase.GetConnectionString("movieapp_insights_v3_concurrency_tests");
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new InsightsRepository(
            context,
            scopeFactory,
            Options.Create(new InsightsV3Options { MaxRepositoryConcurrency = maxRepositoryConcurrency }));
    }
}

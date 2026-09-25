using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Contracts.Auth;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;
using MovieApp.Infrastructure.Identity;
using MovieApp.Infrastructure.Persistence;
using MovieApp.IntegrationTests.Support;

namespace MovieApp.IntegrationTests.Auth;

public sealed class DevelopmentLoginSchemaMigrationApiTests
{
    private const string DevelopmentDatabaseName = "movieapp_dev_login_migration_tests";
    private const string TestingDatabaseName = "movieapp_testing_login_migration_tests";

    [Fact]
    public async Task DevelopmentStartupAppliesPendingRefreshTokenMigrationAndIssuesPasswordAndSocialSessions()
    {
        var connectionString = IntegrationTestDatabase.GetConnectionString(DevelopmentDatabaseName);
        WebApplicationFactory<Program>? factory = null;

        try
        {
            await using (var setup = CreateContext(connectionString))
            {
                await PrepareDatabaseBeforeRefreshTokensAsync(setup);
            }

            factory = CreateFactory(connectionString, "Development", includeSocialVerifiers: true);
            var client = factory.CreateClient();

            var readyResponse = await client.GetAsync("/health/ready");
            var readyBody = await readyResponse.Content.ReadAsStringAsync();
            Assert.Contains("database-migrations", readyBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Database schema is current.", readyBody, StringComparison.Ordinal);

            await using var migrated = CreateContext(connectionString);
            var pending = await migrated.Database.GetPendingMigrationsAsync();
            Assert.DoesNotContain(
                pending,
                id => id.EndsWith("_AddRefreshTokens", StringComparison.Ordinal));
            Assert.True(await RefreshTokensTableExistsAsync(migrated));

            var email = $"dev-login-{Guid.NewGuid():N}@example.com";
            var user = User.Create(
                Guid.NewGuid(),
                email,
                new Pbkdf2PasswordHasher().HashPassword("StrongPassword123"),
                "Dev User",
                DateTime.UtcNow);
            user.MarkEmailVerified(DateTime.UtcNow);
            migrated.Users.Add(user);
            await migrated.SaveChangesAsync();

            var loginResponse = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(email, "StrongPassword123"));
            var loginBody = await loginResponse.Content.ReadAsStringAsync();
            Assert.True(
                loginResponse.StatusCode == HttpStatusCode.OK,
                $"Password login failed with {(int)loginResponse.StatusCode}: {loginBody}");
            var login = JsonSerializer.Deserialize<AuthResponse>(loginBody, JsonSerializerOptions.Web);
            Assert.NotNull(login);
            Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));

            var socialResponse = await client.PostAsJsonAsync(
                "/api/auth/social",
                new SocialAuthRequest(
                    ExternalLoginProviders.Google,
                    IntegrationTestGoogleIdentityTokenVerifier.ValidToken));
            var socialBody = await socialResponse.Content.ReadAsStringAsync();
            Assert.True(
                socialResponse.StatusCode == HttpStatusCode.OK,
                $"Social login failed with {(int)socialResponse.StatusCode}: {socialBody}");
            var social = JsonSerializer.Deserialize<AuthResponse>(socialBody, JsonSerializerOptions.Web);
            Assert.NotNull(social);
            Assert.False(string.IsNullOrWhiteSpace(social.RefreshToken));
            Assert.NotEqual(login.RefreshToken, social.RefreshToken);

            Assert.Equal(2, await migrated.RefreshTokens.CountAsync());
        }
        finally
        {
            factory?.Dispose();
            await DeleteDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task TestingStartupLeavesPendingRefreshTokenMigrationUnapplied()
    {
        var connectionString = IntegrationTestDatabase.GetConnectionString(TestingDatabaseName);
        WebApplicationFactory<Program>? factory = null;

        try
        {
            await using (var setup = CreateContext(connectionString))
            {
                await PrepareDatabaseBeforeRefreshTokensAsync(setup);
            }

            factory = CreateFactory(connectionString, "Testing");
            _ = factory.CreateClient();

            await using var context = CreateContext(connectionString);
            var pending = await context.Database.GetPendingMigrationsAsync();
            Assert.Contains(
                pending,
                id => id.EndsWith("_AddRefreshTokens", StringComparison.Ordinal));
            Assert.False(await RefreshTokensTableExistsAsync(context));
        }
        finally
        {
            factory?.Dispose();
            await DeleteDatabaseAsync(connectionString);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString,
        string environmentName,
        bool includeSocialVerifiers = false,
        string? signingKey = null)
    {
        Environment.SetEnvironmentVariable("PostgreSql__ConnectionString", connectionString);
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            signingKey ?? IntegrationTestJwtSettings.SigningKey);

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environmentName);
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                var configuration = IntegrationTestJwtSettings.CreateConfiguration();
                configuration["Authentication:Jwt:SigningKey"] = signingKey ?? IntegrationTestJwtSettings.SigningKey;
                configuration["PostgreSql:ConnectionString"] = connectionString;
                configuration["Redis:ConnectionString"] = string.Empty;
                configuration["Redis:InstanceName"] = "MovieApp:";
                configuration["MovieProviders:Provider"] = "Fake";
                configuration["BackgroundJobs:Enabled"] = "false";
                configuration["Authentication:RateLimit:SocialPermitLimit"] = "100";
                configuration["Authentication:RateLimit:SocialWindowMinutes"] = "1";
                configurationBuilder.AddInMemoryCollection(configuration);
            });

            if (includeSocialVerifiers)
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ISocialIdentityTokenVerifier>();
                    services.AddSingleton<ISocialIdentityTokenVerifier, IntegrationTestGoogleIdentityTokenVerifier>();
                    services.AddSingleton<ISocialIdentityTokenVerifier, IntegrationTestAppleIdentityTokenVerifier>();
                });
            }
        });
    }

    private static async Task PrepareDatabaseBeforeRefreshTokensAsync(ApplicationDbContext context)
    {
        await context.Database.EnsureDeletedAsync();
        var migrations = context.Database.GetMigrations().ToList();
        var refreshIndex = migrations.FindIndex(id => id.EndsWith("_AddRefreshTokens", StringComparison.Ordinal));
        Assert.True(refreshIndex > 0, "AddRefreshTokens must not be the first migration.");

        await context.Database.MigrateAsync(migrations[refreshIndex - 1]);

        var pending = await context.Database.GetPendingMigrationsAsync();
        Assert.Contains(pending, id => id.EndsWith("_AddRefreshTokens", StringComparison.Ordinal));
        Assert.False(await RefreshTokensTableExistsAsync(context));
    }

    private static async Task<bool> RefreshTokensTableExistsAsync(ApplicationDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = 'refresh_tokens')
                """;
            var result = await command.ExecuteScalarAsync();
            return result is bool exists && exists;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static ApplicationDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task DeleteDatabaseAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        await context.Database.EnsureDeletedAsync();
    }
}

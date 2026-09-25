using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Configuration;

public sealed class ProductionStartupValidationTests
{
    [Fact]
    public void MovieProvidersValidatorFailsProductionWithFakeProvider()
    {
        var validator = new MovieProvidersOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            MovieProvidersOptions.SectionName,
            new MovieProvidersOptions { Provider = MovieDataProviderNames.Fake });

        Assert.False(result.Succeeded);
        Assert.Contains("Tmdb", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Fake", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void MovieProvidersValidatorFailsProductionWithTmdbAndMissingCredential()
    {
        var validator = new MovieProvidersOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            MovieProvidersOptions.SectionName,
            new MovieProvidersOptions
            {
                Provider = MovieDataProviderNames.Tmdb,
                Tmdb = new TmdbOptions()
            });

        Assert.False(result.Succeeded);
        Assert.Contains("ReadAccessToken", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void MovieProvidersValidatorSucceedsProductionWithTmdbAndCredential()
    {
        var validator = new MovieProvidersOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            MovieProvidersOptions.SectionName,
            new MovieProvidersOptions
            {
                Provider = MovieDataProviderNames.Tmdb,
                Tmdb = new TmdbOptions { ReadAccessToken = "test-production-tmdb-token-value" }
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void MovieProvidersValidatorSucceedsProductionWithTmdbApiKey()
    {
        var validator = new MovieProvidersOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            MovieProvidersOptions.SectionName,
            new MovieProvidersOptions
            {
                Provider = "TMDB",
                Tmdb = new TmdbOptions { ApiKey = "test-production-tmdb-api-key" }
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void JwtValidatorFailsProductionWithMissingSigningKey()
    {
        var validator = new JwtOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            JwtOptions.SectionName,
            new JwtOptions { SigningKey = string.Empty });

        Assert.False(result.Succeeded);
        Assert.Contains("SigningKey", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void JwtValidatorFailsProductionWithKnownTestSigningKey()
    {
        var validator = new JwtOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            JwtOptions.SectionName,
            new JwtOptions
            {
                SigningKey = "integration-test-signing-key-must-be-at-least-32-bytes"
            });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void JwtValidatorSucceedsProductionWithSecureSigningKey()
    {
        var validator = new JwtOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            JwtOptions.SectionName,
            new JwtOptions
            {
                SigningKey = "production-test-signing-key-with-32-bytes-minimum"
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void PostgreSqlValidatorFailsProductionWithMissingConfiguration()
    {
        var validator = new PostgreSqlOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            PostgreSqlOptions.SectionName,
            new PostgreSqlOptions());

        Assert.False(result.Succeeded);
        Assert.Contains("PostgreSql", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void PostgreSqlValidatorFailsProductionWithLocalhostComponentConfiguration()
    {
        var validator = new PostgreSqlOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            PostgreSqlOptions.SectionName,
            new PostgreSqlOptions
            {
                Host = "localhost",
                Port = 5432,
                Database = "movieapp",
                Username = "movieapp",
                Password = "test-production-postgres-password"
            });

        Assert.False(result.Succeeded);
        Assert.Contains("localhost", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PostgreSqlValidatorSucceedsProductionWithManagedConnectionString()
    {
        var validator = new PostgreSqlOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            PostgreSqlOptions.SectionName,
            new PostgreSqlOptions
            {
                ConnectionString =
                    "Host=prod-db.example.com;Port=5432;Database=movieapp;Username=movieapp;Password=test-production-postgres-password;SSL Mode=Require"
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void RedisValidatorFailsProductionWithMissingConnectionString()
    {
        var validator = new RedisOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            RedisOptions.SectionName,
            new RedisOptions());

        Assert.False(result.Succeeded);
        Assert.Contains("Redis:ConnectionString", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordResetValidatorFailsProductionWithoutResendProvider()
    {
        var validator = new PasswordResetOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            PasswordResetOptions.SectionName,
            new PasswordResetOptions
            {
                EmailProvider = "Development",
                BaseUrl = "movieapp://reset-password"
            });

        Assert.False(result.Succeeded);
        Assert.Contains("Resend", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void MovieProvidersValidatorAllowsDevelopmentWithFakeProvider()
    {
        var validator = new MovieProvidersOptionsValidator(new FakeHostEnvironment("Development"));

        var result = validator.Validate(
            MovieProvidersOptions.SectionName,
            new MovieProvidersOptions { Provider = MovieDataProviderNames.Fake });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void RedisValidatorAllowsDevelopmentWithOptionalRedis()
    {
        var validator = new RedisOptionsValidator(new FakeHostEnvironment("Development"));

        var result = validator.Validate(
            RedisOptions.SectionName,
            new RedisOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void JwtValidatorFailsDevelopmentWithMissingSigningKey()
    {
        var validator = new JwtOptionsValidator(new FakeHostEnvironment("Development"));

        var result = validator.Validate(
            JwtOptions.SectionName,
            new JwtOptions { SigningKey = string.Empty });

        Assert.False(result.Succeeded);
        Assert.Contains("Authentication:Jwt:SigningKey", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Development", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void JwtValidatorSucceedsDevelopmentWithLocalSigningKey()
    {
        var validator = new JwtOptionsValidator(new FakeHostEnvironment("Development"));

        var result = validator.Validate(
            JwtOptions.SectionName,
            new JwtOptions
            {
                SigningKey = "YOUR_LOCAL_DEVELOPMENT_SIGNING_KEY_AT_LEAST_32_CHARS"
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void JwtValidatorFailsDevelopmentWithShortSigningKey()
    {
        var validator = new JwtOptionsValidator(new FakeHostEnvironment("Development"));

        var result = validator.Validate(
            JwtOptions.SectionName,
            new JwtOptions { SigningKey = "1234567890123456789012345678901" });

        Assert.False(result.Succeeded);
        Assert.Contains("32 characters", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void JwtValidatorAllowsTestingWithMissingSigningKey()
    {
        var validator = new JwtOptionsValidator(new FakeHostEnvironment("Testing"));

        var result = validator.Validate(
            JwtOptions.SectionName,
            new JwtOptions { SigningKey = string.Empty });

        Assert.True(result.Succeeded);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MovieApp.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

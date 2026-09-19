using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.UnitTests.Configuration;

public sealed class MovieAppDataProtectionOptionsValidatorTests
{
    [Fact]
    public void ValidateAllowsDevelopmentWithoutRedisOrCertificate()
    {
        var validator = CreateValidator(
            new FakeHostEnvironment("Development"),
            new RedisOptions());

        var result = validator.Validate(
            MovieAppDataProtectionOptions.SectionName,
            new MovieAppDataProtectionOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ValidateFailsProductionWithoutRedis()
    {
        var validator = CreateValidator(
            new FakeHostEnvironment("Production"),
            new RedisOptions());

        var result = validator.Validate(
            MovieAppDataProtectionOptions.SectionName,
            new MovieAppDataProtectionOptions { CertificatePath = "certs/dp.pfx" });

        Assert.False(result.Succeeded);
        Assert.Contains("Redis", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateFailsProductionWithoutCertificate()
    {
        var validator = CreateValidator(
            new FakeHostEnvironment("Production"),
            new RedisOptions { ConnectionString = "localhost:6379" });

        var result = validator.Validate(
            MovieAppDataProtectionOptions.SectionName,
            new MovieAppDataProtectionOptions());

        Assert.False(result.Succeeded);
        Assert.Contains("CertificatePath", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateSucceedsProductionWithRedisAndCertificate()
    {
        var validator = CreateValidator(
            new FakeHostEnvironment("Production"),
            new RedisOptions { ConnectionString = "redis.example.com:6379" });

        var result = validator.Validate(
            MovieAppDataProtectionOptions.SectionName,
            new MovieAppDataProtectionOptions { CertificatePath = "certs/dp.pfx" });

        Assert.True(result.Succeeded);
    }

    private static MovieAppDataProtectionOptionsValidator CreateValidator(
        IHostEnvironment hostEnvironment,
        RedisOptions redisOptions) =>
        new(hostEnvironment, Options.Create(redisOptions));

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MovieApp.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

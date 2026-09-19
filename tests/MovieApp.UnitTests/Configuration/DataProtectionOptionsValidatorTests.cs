using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.UnitTests.Configuration;

public sealed class MovieAppDataProtectionOptionsValidatorTests
{
    [Fact]
    public void ValidateAllowsDevelopmentWithoutPostgreSqlOrEncryptionKey()
    {
        var validator = CreateValidator(
            new FakeHostEnvironment("Development"),
            new PostgreSqlOptions());

        var result = validator.Validate(
            MovieAppDataProtectionOptions.SectionName,
            new MovieAppDataProtectionOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ValidateFailsProductionWithoutPostgreSql()
    {
        var validator = CreateValidator(
            new FakeHostEnvironment("Production"),
            new PostgreSqlOptions());

        var result = validator.Validate(
            MovieAppDataProtectionOptions.SectionName,
            new MovieAppDataProtectionOptions
            {
                KeyEncryptionKeyBase64 = Convert.ToBase64String(new byte[32])
            });

        Assert.False(result.Succeeded);
        Assert.Contains("PostgreSql", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateFailsProductionWithoutKeyEncryptionKey()
    {
        var validator = CreateValidator(
            new FakeHostEnvironment("Production"),
            new PostgreSqlOptions { ConnectionString = "Host=db.example.com;Database=movieapp" });

        var result = validator.Validate(
            MovieAppDataProtectionOptions.SectionName,
            new MovieAppDataProtectionOptions());

        Assert.False(result.Succeeded);
        Assert.Contains("KeyEncryptionKeyBase64", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateSucceedsProductionWithPostgreSqlAndEncryptionKey()
    {
        var validator = CreateValidator(
            new FakeHostEnvironment("Production"),
            new PostgreSqlOptions { ConnectionString = "Host=db.example.com;Database=movieapp" });

        var result = validator.Validate(
            MovieAppDataProtectionOptions.SectionName,
            new MovieAppDataProtectionOptions
            {
                KeyEncryptionKeyBase64 = Convert.ToBase64String(new byte[32])
            });

        Assert.True(result.Succeeded);
    }

    private static MovieAppDataProtectionOptionsValidator CreateValidator(
        IHostEnvironment hostEnvironment,
        PostgreSqlOptions postgreSqlOptions) =>
        new(hostEnvironment, Options.Create(postgreSqlOptions));

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MovieApp.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

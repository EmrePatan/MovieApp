using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.UnitTests.Configuration;

public sealed class PasswordResetOptionsValidatorTests
{
    [Fact]
    public void ValidateAllowsDevelopmentAndTestingEnvironments()
    {
        var validator = CreateValidator(new FakeHostEnvironment("Development"));

        var result = validator.Validate(
            PasswordResetOptions.SectionName,
            new PasswordResetOptions { EmailProvider = "Development" });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ValidateFailsProductionWithoutResendProvider()
    {
        var validator = CreateValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            PasswordResetOptions.SectionName,
            new PasswordResetOptions { EmailProvider = "Development", BaseUrl = "movieapp://reset-password" });

        Assert.False(result.Succeeded);
        Assert.Contains("EmailProvider", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Resend", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateSucceedsProductionWithResendProviderAndHttpsBaseUrl()
    {
        var validator = CreateValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            PasswordResetOptions.SectionName,
            new PasswordResetOptions
            {
                EmailProvider = "Resend",
                BaseUrl = "https://moviecaveapp.com/auth/reset-password"
            });

        Assert.True(result.Succeeded);
    }

    private static PasswordResetOptionsValidator CreateValidator(IHostEnvironment hostEnvironment) =>
        new(hostEnvironment);

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MovieApp.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

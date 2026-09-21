using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.UnitTests.Configuration;

public sealed class EmailAuthLinkOptionsValidatorTests
{
    [Fact]
    public void EmailVerificationProductionValidator_RejectsCustomSchemeBaseUrl()
    {
        var validator = new EmailVerificationOptionsValidator(new FakeHostEnvironment(Environments.Production));
        var result = validator.Validate(
            null,
            new EmailVerificationOptions
            {
                BaseUrl = "movieapp://verify-email"
            });

        Assert.False(result.Succeeded);
        Assert.Contains("HTTPS URL", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void EmailVerificationProductionValidator_AcceptsHttpsBridgeBaseUrl()
    {
        var validator = new EmailVerificationOptionsValidator(new FakeHostEnvironment(Environments.Production));
        var result = validator.Validate(
            null,
            new EmailVerificationOptions
            {
                BaseUrl = "https://moviecaveapp.com/auth/verify-email"
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void PasswordResetProductionValidator_RejectsCustomSchemeBaseUrl()
    {
        var validator = new PasswordResetOptionsValidator(new FakeHostEnvironment(Environments.Production));
        var result = validator.Validate(
            null,
            new PasswordResetOptions
            {
                EmailProvider = "Resend",
                BaseUrl = "movieapp://reset-password"
            });

        Assert.False(result.Succeeded);
        Assert.Contains("HTTPS URL", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordResetProductionValidator_AcceptsHttpsBridgeBaseUrl()
    {
        var validator = new PasswordResetOptionsValidator(new FakeHostEnvironment(Environments.Production));
        var result = validator.Validate(
            null,
            new PasswordResetOptions
            {
                EmailProvider = "Resend",
                BaseUrl = "https://moviecaveapp.com/auth/reset-password"
            });

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

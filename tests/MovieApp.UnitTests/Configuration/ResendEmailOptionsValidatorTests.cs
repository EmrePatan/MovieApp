using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.UnitTests.Configuration;

public sealed class ResendEmailOptionsValidatorTests
{
    [Fact]
    public void PasswordResetValidatorSucceedsProductionWithSharedResendDefaults()
    {
        var validator = new ResendPasswordResetEmailOptionsValidator(
            new FakeHostEnvironment("Production"),
            Options.Create(new PasswordResetOptions { EmailProvider = "Resend" }),
            Options.Create(new SharedResendEmailOptions
            {
                ApiKey = "re_prod_key",
                FromAddress = "noreply@moviecave.example"
            }));

        var result = validator.Validate(
            ResendPasswordResetEmailOptions.SectionName,
            new ResendPasswordResetEmailOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void PasswordResetValidatorFailsProductionForOnboardingSender()
    {
        var validator = new ResendPasswordResetEmailOptionsValidator(
            new FakeHostEnvironment("Production"),
            Options.Create(new PasswordResetOptions { EmailProvider = "Resend" }),
            Options.Create(new SharedResendEmailOptions
            {
                ApiKey = "re_prod_key",
                FromAddress = "onboarding@resend.dev"
            }));

        var result = validator.Validate(
            ResendPasswordResetEmailOptions.SectionName,
            new ResendPasswordResetEmailOptions());

        Assert.False(result.Succeeded);
        Assert.Contains("custom-domain", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerificationValidatorSucceedsProductionWithSharedResendDefaults()
    {
        var validator = new ResendVerificationEmailOptionsValidator(
            new FakeHostEnvironment("Production"),
            Options.Create(new SharedResendEmailOptions
            {
                ApiKey = "re_prod_key",
                FromAddress = "noreply@moviecave.example"
            }));

        var result = validator.Validate(
            ResendVerificationEmailOptions.SectionName,
            new ResendVerificationEmailOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void VerificationValidatorFailsProductionForOnboardingSender()
    {
        var validator = new ResendVerificationEmailOptionsValidator(
            new FakeHostEnvironment("Production"),
            Options.Create(new SharedResendEmailOptions
            {
                ApiKey = "re_prod_key",
                FromAddress = "onboarding@resend.dev"
            }));

        var result = validator.Validate(
            ResendVerificationEmailOptions.SectionName,
            new ResendVerificationEmailOptions());

        Assert.False(result.Succeeded);
        Assert.Contains("custom-domain", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MovieApp.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

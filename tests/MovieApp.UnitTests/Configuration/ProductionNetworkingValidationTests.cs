using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.UnitTests.Configuration;

public sealed class ProductionNetworkingValidationTests
{
    [Fact]
    public void CorsValidatorAllowsProductionWhenDisabledForNativeMobileClients()
    {
        var validator = new CorsOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            CorsOptions.SectionName,
            new CorsOptions { Enabled = false });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void CorsValidatorFailsProductionWhenEnabledWithoutValidOrigins()
    {
        var validator = new CorsOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            CorsOptions.SectionName,
            new CorsOptions
            {
                Enabled = true,
                AllowedOrigins = ["http://localhost:5027"]
            });

        Assert.False(result.Succeeded);
        Assert.Contains("AllowedOrigins", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void CorsValidatorSucceedsProductionWithConfiguredHttpsOrigin()
    {
        var validator = new CorsOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            CorsOptions.SectionName,
            new CorsOptions
            {
                Enabled = true,
                AllowedOrigins = ["https://app.example.com"]
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void CorsOptionsGetValidOriginsRejectsArbitraryOriginsInProductionMode()
    {
        var options = new CorsOptions
        {
            Enabled = true,
            AllowedOrigins =
            [
                "https://app.example.com",
                "https://evil.example.net",
                "not-a-uri",
                "http://localhost:5027"
            ]
        };

        var validOrigins = options.GetValidOrigins(requireHttps: true);

        Assert.Equal(["https://app.example.com", "https://evil.example.net"], validOrigins);
        Assert.DoesNotContain("http://localhost:5027", validOrigins, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void CorsOptionsGetValidOriginsAcceptsLocalhostInDevelopmentMode()
    {
        var options = new CorsOptions
        {
            Enabled = true,
            AllowedOrigins = ["http://localhost:5027", "https://app.example.com"]
        };

        var validOrigins = options.GetValidOrigins(requireHttps: false);

        Assert.Equal(2, validOrigins.Count);
        Assert.Contains("http://localhost:5027", validOrigins, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppValidatorFailsProductionWithoutPublicBaseUrl()
    {
        var validator = new AppOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(AppOptions.SectionName, new AppOptions());

        Assert.False(result.Succeeded);
        Assert.Contains("PublicBaseUrl", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void AppValidatorFailsProductionWithNonHttpsPublicBaseUrl()
    {
        var validator = new AppOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            AppOptions.SectionName,
            new AppOptions { PublicBaseUrl = "http://api.example.com" });

        Assert.False(result.Succeeded);
        Assert.Contains("HTTPS", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void AppValidatorSucceedsProductionWithHttpsPublicBaseUrl()
    {
        var validator = new AppOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            AppOptions.SectionName,
            new AppOptions { PublicBaseUrl = "https://api.example.com" });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ForwardedHeadersValidatorFailsProductionWhenEnabledWithoutTrustedProxies()
    {
        var validator = new ForwardedHeadersOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            ForwardedHeadersOptionsConfig.SectionName,
            new ForwardedHeadersOptionsConfig { Enabled = true });

        Assert.False(result.Succeeded);
        Assert.Contains("UseRenderProxyTrustDefaults", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ForwardedHeadersValidatorSucceedsProductionWhenEnabledWithRenderDefaults()
    {
        var validator = new ForwardedHeadersOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            ForwardedHeadersOptionsConfig.SectionName,
            new ForwardedHeadersOptionsConfig
            {
                Enabled = true,
                UseRenderProxyTrustDefaults = true
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ForwardedHeadersValidatorSucceedsProductionWhenEnabledWithKnownProxy()
    {
        var validator = new ForwardedHeadersOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            ForwardedHeadersOptionsConfig.SectionName,
            new ForwardedHeadersOptionsConfig
            {
                Enabled = true,
                KnownProxies = ["203.0.113.10"]
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ForwardedHeadersValidatorSucceedsProductionWhenDisabled()
    {
        var validator = new ForwardedHeadersOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            ForwardedHeadersOptionsConfig.SectionName,
            new ForwardedHeadersOptionsConfig { Enabled = false });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void CorsValidatorAllowsDevelopmentWithLocalhostOrigins()
    {
        var validator = new CorsOptionsValidator(new FakeHostEnvironment("Development"));

        var result = validator.Validate(
            CorsOptions.SectionName,
            new CorsOptions
            {
                Enabled = true,
                AllowedOrigins = ["http://localhost:5027"]
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void AppValidatorAllowsDevelopmentWithoutPublicBaseUrl()
    {
        var validator = new AppOptionsValidator(new FakeHostEnvironment("Development"));

        var result = validator.Validate(AppOptions.SectionName, new AppOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void SocialAuthValidatorFailsProductionWhenNoClientIdsConfigured()
    {
        var validator = new SocialAuthOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            SocialAuthOptions.SectionName,
            new SocialAuthOptions());

        Assert.False(result.Succeeded);
        Assert.Contains("ClientIds", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialAuthValidatorSucceedsProductionWithGoogleClientId()
    {
        var validator = new SocialAuthOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            SocialAuthOptions.SectionName,
            new SocialAuthOptions
            {
                Google = new GoogleSocialAuthOptions
                {
                    ClientIds = ["123456789.apps.googleusercontent.com"]
                }
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void SocialAuthValidatorSucceedsProductionWithAppleClientId()
    {
        var validator = new SocialAuthOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(
            SocialAuthOptions.SectionName,
            new SocialAuthOptions
            {
                Apple = new AppleSocialAuthOptions
                {
                    ClientIds = ["com.movieapp.mobile"]
                }
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void SocialAuthValidatorAllowsDevelopmentWithoutClientIds()
    {
        var validator = new SocialAuthOptionsValidator(new FakeHostEnvironment("Development"));

        var result = validator.Validate(
            SocialAuthOptions.SectionName,
            new SocialAuthOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void SocialAuthValidatorFailsWhenClientIdArrayContainsEmptyValue()
    {
        var validator = new SocialAuthOptionsValidator(new FakeHostEnvironment("Development"));

        var result = validator.Validate(
            SocialAuthOptions.SectionName,
            new SocialAuthOptions
            {
                Google = new GoogleSocialAuthOptions
                {
                    ClientIds = ["valid-client-id", ""]
                }
            });

        Assert.False(result.Succeeded);
        Assert.Contains("Google", result.FailureMessage, StringComparison.Ordinal);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MovieApp.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MovieApp.Api.Security;

namespace MovieApp.UnitTests.Configuration;

public sealed class SecurityPipelineExtensionsTests
{
    [Theory]
    [InlineData("Development", false)]
    [InlineData("Testing", false)]
    [InlineData("Production", true)]
    public void ShouldApplyProductionTransportSecurityOnlyInProduction(
        string environmentName,
        bool expected)
    {
        var environment = new FakeHostEnvironment(environmentName);

        Assert.Equal(expected, SecurityPipelineExtensions.ShouldApplyProductionTransportSecurity(environment));
    }

    [Fact]
    public void ShouldEnableInProcessHttpsRedirectionWhenHttpsPortsAreConfigured()
    {
        var originalHttpsPorts = Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORTS");
        var originalUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");

        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_HTTPS_PORTS", "443");
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", null);

            Assert.True(SecurityPipelineExtensions.ShouldEnableInProcessHttpsRedirection());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_HTTPS_PORTS", originalHttpsPorts);
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", originalUrls);
        }
    }

    [Fact]
    public void ShouldDisableInProcessHttpsRedirectionForHttpOnlyContainerBinding()
    {
        var originalHttpsPorts = Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORTS");
        var originalUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");

        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_HTTPS_PORTS", null);
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://+:8080");

            Assert.False(SecurityPipelineExtensions.ShouldEnableInProcessHttpsRedirection());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_HTTPS_PORTS", originalHttpsPorts);
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", originalUrls);
        }
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MovieApp.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

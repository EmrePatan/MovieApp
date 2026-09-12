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

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MovieApp.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

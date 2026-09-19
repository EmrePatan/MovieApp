using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MovieApp.Infrastructure.Identity;

namespace MovieApp.UnitTests.Identity;

public sealed class EmailVerificationDeliveryConfigurationTests
{
    [Fact]
    public void EnsureSupportedDeliveryBackendAllowsTestingWithoutHangfire()
    {
        var exception = Record.Exception(() =>
            EmailVerificationDeliveryConfiguration.EnsureSupportedDeliveryBackend(
                new FakeHostEnvironment("Testing"),
                backgroundJobsEnabled: false));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureSupportedDeliveryBackendAllowsDevelopmentWithoutHangfire()
    {
        var exception = Record.Exception(() =>
            EmailVerificationDeliveryConfiguration.EnsureSupportedDeliveryBackend(
                new FakeHostEnvironment("Development"),
                backgroundJobsEnabled: false));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureSupportedDeliveryBackendFailsProductionWithoutHangfire()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            EmailVerificationDeliveryConfiguration.EnsureSupportedDeliveryBackend(
                new FakeHostEnvironment("Production"),
                backgroundJobsEnabled: false));

        Assert.Contains("BackgroundJobs.Enabled", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureSupportedDeliveryBackendAllowsProductionWithHangfire()
    {
        var exception = Record.Exception(() =>
            EmailVerificationDeliveryConfiguration.EnsureSupportedDeliveryBackend(
                new FakeHostEnvironment("Production"),
                backgroundJobsEnabled: true));

        Assert.Null(exception);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MovieApp.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

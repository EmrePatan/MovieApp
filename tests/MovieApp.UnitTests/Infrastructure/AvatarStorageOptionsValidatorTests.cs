using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.UnitTests.Infrastructure;

public sealed class AvatarStorageOptionsValidatorTests
{
    [Fact]
    public void ProductionRequiresFullyConfiguredAvatarStorage()
    {
        var validator = new AvatarStorageOptionsValidator(new FakeHostEnvironment(Environments.Production));

        var result = validator.Validate(null, new AvatarStorageOptions());

        Assert.True(result.Failed);
        Assert.Contains("AvatarStorage", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void DevelopmentAllowsIncompleteAvatarStorage()
    {
        var validator = new AvatarStorageOptionsValidator(new FakeHostEnvironment(Environments.Development));

        var result = validator.Validate(null, new AvatarStorageOptions());

        Assert.False(result.Failed);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "MovieApp.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

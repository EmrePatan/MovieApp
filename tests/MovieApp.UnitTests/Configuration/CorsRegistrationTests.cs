using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MovieApp.Api.Cors;

namespace MovieApp.UnitTests.Configuration;

public sealed class CorsRegistrationTests
{
    [Fact]
    public async Task AddConfiguredCorsRegistersPolicyWithConfiguredOriginOnly()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment("Production"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["Cors:Enabled"] = "true",
                ["Cors:AllowedOrigins:0"] = "https://app.example.com",
                ["Cors:AllowedOrigins:1"] = "https://*.example.com"
            })
            .Build();

        services.AddConfiguredCors(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = false
        });

        var corsPolicyProvider = provider.GetRequiredService<ICorsPolicyProvider>();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };

        var policy = await corsPolicyProvider.GetPolicyAsync(httpContext, CorsPolicyNames.Default);

        Assert.NotNull(policy);
        Assert.Contains("https://app.example.com", policy!.Origins, StringComparer.OrdinalIgnoreCase);
        Assert.Single(policy.Origins);
    }

    [Fact]
    public void AddConfiguredCorsDoesNotRegisterPolicyWhenDisabled()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment("Production"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:Enabled"] = "false"
            })
            .Build();

        services.AddConfiguredCors(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = false
        });

        Assert.False(provider.GetServices<ICorsService>().Any());
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MovieApp.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

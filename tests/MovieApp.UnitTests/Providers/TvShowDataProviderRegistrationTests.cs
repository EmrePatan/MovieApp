using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Infrastructure.Providers;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Providers;

public sealed class TvShowDataProviderRegistrationTests
{
    [Fact]
    public void FakeProviderRegistrationResolvesFakeTvShowDataProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment("Development"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MovieProviders:Provider"] = "Fake"
            })
            .Build();

        services.AddMovieDataProviders(configuration);
        services.AddTvShowDataProviders(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = false
        });

        var tvShowDataProvider = provider.GetRequiredService<ITvShowDataProvider>();
        var externalIdResolver = provider.GetRequiredService<ITvShowExternalIdResolver>();

        Assert.IsType<FakeTvShowDataProvider>(tvShowDataProvider);
        Assert.IsType<FakeTvExternalIdResolver>(externalIdResolver);
    }

    [Fact]
    public void TmdbProviderRegistrationResolvesTmdbTvShowDataProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment("Development"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MovieProviders:Provider"] = "Tmdb",
                ["MovieProviders:Tmdb:BaseUrl"] = "https://api.themoviedb.org/3/",
                ["MovieProviders:Tmdb:ApiKey"] = "test-api-key"
            })
            .Build();

        services.AddMovieDataProviders(configuration);
        services.AddTvShowDataProviders(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = false
        });

        var tvShowDataProvider = provider.GetRequiredService<ITvShowDataProvider>();
        var externalIdResolver = provider.GetRequiredService<ITvShowExternalIdResolver>();

        Assert.IsType<TmdbTvShowDataProvider>(tvShowDataProvider);
        Assert.IsType<TmdbTvExternalIdResolver>(externalIdResolver);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MovieApp.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

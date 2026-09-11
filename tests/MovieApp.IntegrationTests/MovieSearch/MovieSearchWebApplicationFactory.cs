using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MovieApp.IntegrationTests.MovieSearch;

public sealed class MovieSearchWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            MovieSearchIntegrationDatabase.GetConnectionString());

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] = MovieSearchIntegrationDatabase.GetConnectionString();
            configuration["Redis:ConnectionString"] = string.Empty;
            configuration["Redis:InstanceName"] = "MovieApp:";
            configuration["MovieProviders:Provider"] = "Fake";
            configuration["MovieProviders:Tmdb:ApiKey"] = string.Empty;
            configuration["MovieProviders:Tmdb:ReadAccessToken"] = string.Empty;
            configuration["MovieProviders:Tvdb:ApiKey"] = string.Empty;
            configurationBuilder.AddInMemoryCollection(configuration);
        });
    }

}

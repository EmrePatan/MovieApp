using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.IntegrationTests.Support;

namespace MovieApp.IntegrationTests.UserProfile;

public sealed class UserProfileWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            UserProfileIntegrationDatabase.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] = UserProfileIntegrationDatabase.GetConnectionString();
            configuration["Redis:ConnectionString"] = string.Empty;
            configuration["Redis:InstanceName"] = "MovieApp:";
            configuration["MovieProviders:Provider"] = "Fake";
            configurationBuilder.AddInMemoryCollection(configuration);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISocialIdentityTokenVerifier>();
            services.AddSingleton<ISocialIdentityTokenVerifier, IntegrationTestGoogleIdentityTokenVerifier>();
            services.AddSingleton<ISocialIdentityTokenVerifier, IntegrationTestAppleIdentityTokenVerifier>();
        });
    }
}

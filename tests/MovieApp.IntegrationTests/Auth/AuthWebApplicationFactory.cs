using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Infrastructure.Email;

namespace MovieApp.IntegrationTests.Auth;

public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    public CapturingEmailSender EmailSender =>
        Services.GetRequiredService<CapturingEmailSender>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            AuthIntegrationDatabase.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] = AuthIntegrationDatabase.GetConnectionString();
            configuration["Redis:ConnectionString"] = string.Empty;
            configuration["Redis:InstanceName"] = "MovieApp:";
            configuration["MovieProviders:Provider"] = "Fake";
            configuration["Authentication:PasswordReset:TokenLifetimeMinutes"] = "60";
            configuration["Authentication:PasswordReset:BaseUrl"] = "movieapp://reset-password";
            configurationBuilder.AddInMemoryCollection(configuration);
        });
    }
}

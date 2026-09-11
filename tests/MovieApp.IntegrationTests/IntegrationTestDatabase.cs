using Microsoft.Extensions.Configuration;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.IntegrationTests;

internal static class IntegrationTestDatabase
{
    internal const string DatabaseName = "movieapp_integration_tests";

    internal static string GetConnectionString() =>
        GetConnectionString(DatabaseName);

    internal static string GetConnectionString(string databaseName)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.IntegrationTests.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var postgreSqlOptions = configuration
            .GetSection(PostgreSqlOptions.SectionName)
            .Get<PostgreSqlOptions>() ?? new PostgreSqlOptions();

        var password = postgreSqlOptions.Password;

        if (string.IsNullOrWhiteSpace(password))
        {
            password = Environment.GetEnvironmentVariable("PostgreSql__Password")
                ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Set PostgreSql__Password or POSTGRES_PASSWORD for integration database tests.");
        }

        if (postgreSqlOptions.IsConfigured())
        {
            return
                $"Host={postgreSqlOptions.Host};Port={postgreSqlOptions.Port};Database={databaseName};Username={postgreSqlOptions.Username};Password={password}";
        }

        return
            $"Host=localhost;Port=5432;Database={databaseName};Username=movieapp;Password={password}";
    }
}

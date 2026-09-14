using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Persistence;

internal sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static string ResolveConnectionString()
    {
        var fromConnectionFile = TryReadConnectionFile();
        if (!string.IsNullOrWhiteSpace(fromConnectionFile))
        {
            return PostgreSqlConnectionStringFactory.Normalize(fromConnectionFile);
        }

        var fromStagingSecret = Environment.GetEnvironmentVariable("MOVIEAPP_STAGING_POSTGRES_CONNECTION");
        if (!string.IsNullOrWhiteSpace(fromStagingSecret))
        {
            return PostgreSqlConnectionStringFactory.Normalize(fromStagingSecret);
        }

        var fromEnvironment = Environment.GetEnvironmentVariable("PostgreSql__ConnectionString");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return PostgreSqlConnectionStringFactory.Normalize(fromEnvironment);
        }

        var configuration = BuildConfiguration();
        var postgreSqlOptions = configuration
            .GetSection(PostgreSqlOptions.SectionName)
            .Get<PostgreSqlOptions>() ?? new PostgreSqlOptions();

        if (postgreSqlOptions.IsConfigured())
        {
            if (string.IsNullOrWhiteSpace(postgreSqlOptions.Password))
            {
                postgreSqlOptions.Password =
                    configuration["PostgreSql:Password"]
                    ?? Environment.GetEnvironmentVariable("PostgreSql__Password")
                    ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")
                    ?? string.Empty;
            }

            return postgreSqlOptions.ResolveConnectionString();
        }

        throw new InvalidOperationException(
            "Design-time migrations require a PostgreSQL connection. " +
            "Set NPgsql_CONNECTION_FILE, MOVIEAPP_STAGING_POSTGRES_CONNECTION, " +
            "PostgreSql__ConnectionString, or PostgreSql settings in appsettings.");
    }

    private static string? TryReadConnectionFile()
    {
        var connectionFile = Environment.GetEnvironmentVariable("NPgsql_CONNECTION_FILE");
        if (string.IsNullOrWhiteSpace(connectionFile) || !File.Exists(connectionFile))
        {
            return null;
        }

        return File.ReadAllText(connectionFile).Trim();
    }

    private static IConfiguration BuildConfiguration()
    {
        var apiSettingsPath = ResolveApiSettingsPath();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        return new ConfigurationBuilder()
            .SetBasePath(apiSettingsPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string ResolveApiSettingsPath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "src", "MovieApp.Api"),
            Path.Combine(Directory.GetCurrentDirectory(), "MovieApp.Api"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "MovieApp.Api"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MovieApp.Api")),
        };

        foreach (var candidate in candidates)
        {
            var settingsPath = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(settingsPath, "appsettings.json")))
            {
                return settingsPath;
            }
        }

        throw new InvalidOperationException(
            "Could not locate MovieApp.Api appsettings for design-time migrations.");
    }
}

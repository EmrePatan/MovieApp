using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MovieApp.Infrastructure.Configuration;

public sealed class PostgreSqlOptionsValidator(IHostEnvironment hostEnvironment) : IValidateOptions<PostgreSqlOptions>
{
    public ValidateOptionsResult Validate(string? name, PostgreSqlOptions options)
    {
        if (!hostEnvironment.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        if (!options.IsConfigured())
        {
            return ValidateOptionsResult.Fail(
                "Production requires PostgreSQL to be configured via PostgreSql:ConnectionString " +
                "or PostgreSql:Host, PostgreSql:Port, PostgreSql:Database, PostgreSql:Username, and PostgreSql:Password.");
        }

        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateConnectionString(options.ConnectionString);
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            return ValidateOptionsResult.Fail(
                "Production PostgreSQL requires PostgreSql:Password when using component-based configuration.");
        }

        if (IsLocalDevelopmentHost(options.Host))
        {
            return ValidateOptionsResult.Fail(
                "Production PostgreSQL must not use localhost. Configure PostgreSql:Host to your managed database endpoint.");
        }

        return ValidateOptionsResult.Success;
    }

    private static ValidateOptionsResult ValidateConnectionString(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);

            if (IsLocalDevelopmentHost(builder.Host ?? string.Empty))
            {
                return ValidateOptionsResult.Fail(
                    "Production PostgreSQL must not use localhost. Configure a managed PostgreSQL endpoint via PostgreSql:ConnectionString.");
            }

            if (string.IsNullOrWhiteSpace(builder.Password))
            {
                return ValidateOptionsResult.Fail(
                    "Production PostgreSQL connection string must include a password.");
            }

            return ValidateOptionsResult.Success;
        }
        catch (ArgumentException)
        {
            return ValidateOptionsResult.Fail(
                "Production PostgreSQL ConnectionString is invalid. Configure PostgreSql:ConnectionString with a valid managed database connection.");
        }
    }

    private static bool IsLocalDevelopmentHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
}

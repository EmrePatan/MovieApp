using MovieApp.Infrastructure.Configuration;
using Npgsql;

namespace MovieApp.UnitTests.Configuration;

public sealed class PostgreSqlConnectionStringFactoryTests
{
    private const string TestPassword = "test-postgres-password";

    [Fact]
    public void NormalizeAddsGssEncryptionModeDisableToExplicitConnectionString()
    {
        const string connectionString =
            "Host=prod-db.example.com;Port=5432;Database=movieapp;Username=movieapp;Password=" + TestPassword
            + ";SSL Mode=Require";

        var normalized = PostgreSqlConnectionStringFactory.Normalize(connectionString);
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal(GssEncryptionMode.Disable, builder.GssEncryptionMode);
        Assert.Equal("prod-db.example.com", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("movieapp", builder.Database);
        Assert.Equal("movieapp", builder.Username);
        Assert.Equal(SslMode.Require, builder.SslMode);
        Assert.Equal(TestPassword, builder.Password);
    }

    [Fact]
    public void ResolveConnectionStringAddsGssEncryptionModeDisableForComponentConfiguration()
    {
        var options = new PostgreSqlOptions
        {
            Host = "prod-db.example.com",
            Port = 5432,
            Database = "movieapp",
            Username = "movieapp",
            Password = TestPassword,
        };

        var resolved = options.ResolveConnectionString();
        var builder = new NpgsqlConnectionStringBuilder(resolved);

        Assert.Equal(GssEncryptionMode.Disable, builder.GssEncryptionMode);
        Assert.Equal("prod-db.example.com", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("movieapp", builder.Database);
        Assert.Equal("movieapp", builder.Username);
        Assert.Equal(TestPassword, builder.Password);
    }

    [Fact]
    public void NormalizePreservesExistingConnectionStringSettings()
    {
        const string connectionString =
            "Host=prod-db.example.com;Port=5433;Database=movieapp;Username=movieapp;Password=" + TestPassword
            + ";SSL Mode=VerifyFull;Timeout=30;Command Timeout=60;Pooling=true";

        var normalized = PostgreSqlConnectionStringFactory.Normalize(connectionString);
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal(GssEncryptionMode.Disable, builder.GssEncryptionMode);
        Assert.Equal("prod-db.example.com", builder.Host);
        Assert.Equal(5433, builder.Port);
        Assert.Equal(SslMode.VerifyFull, builder.SslMode);
        Assert.Equal(30, builder.Timeout);
        Assert.Equal(60, builder.CommandTimeout);
        Assert.True(builder.Pooling);
    }

    [Fact]
    public void NormalizeOverridesPreferGssEncryptionModeWithoutDuplicatingSetting()
    {
        const string connectionString =
            "Host=prod-db.example.com;Port=5432;Database=movieapp;Username=movieapp;Password=" + TestPassword
            + ";GSS Encryption Mode=Prefer;SSL Mode=Require";

        var normalized = PostgreSqlConnectionStringFactory.Normalize(connectionString);
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal(GssEncryptionMode.Disable, builder.GssEncryptionMode);
        Assert.Equal(SslMode.Require, builder.SslMode);
        Assert.Equal(1, CountSettingOccurrences(normalized, "GSS Encryption Mode"));
    }

    [Fact]
    public void NormalizeDoesNotDuplicateGssEncryptionModeWhenAlreadyDisabled()
    {
        const string connectionString =
            "Host=prod-db.example.com;Port=5432;Database=movieapp;Username=movieapp;Password=" + TestPassword
            + ";GSS Encryption Mode=Disable;SSL Mode=Require";

        var normalized = PostgreSqlConnectionStringFactory.Normalize(connectionString);
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal(GssEncryptionMode.Disable, builder.GssEncryptionMode);
        Assert.Equal(1, CountSettingOccurrences(normalized, "GSS Encryption Mode"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeThrowsForMissingConnectionString(string connectionString)
    {
        Assert.Throws<ArgumentException>(() => PostgreSqlConnectionStringFactory.Normalize(connectionString));
    }

    private static int CountSettingOccurrences(string connectionString, string settingName) =>
        connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(part => part.StartsWith(settingName, StringComparison.OrdinalIgnoreCase));
}

using MovieApp.Infrastructure.Configuration;
using Npgsql;

namespace MovieApp.UnitTests.Infrastructure;

public sealed class PostgreSqlOptionsTests
{
    [Fact]
    public void ResolveConnectionStringUsesExplicitConnectionStringWhenProvided()
    {
        var options = new PostgreSqlOptions
        {
            ConnectionString = "Host=db;Port=5432;Database=movieapp;Username=movieapp;Password=secret"
        };

        var resolved = options.ResolveConnectionString();
        var builder = new NpgsqlConnectionStringBuilder(resolved);

        Assert.Equal("db", builder.Host);
        Assert.Equal(GssEncryptionMode.Disable, builder.GssEncryptionMode);
    }

    [Fact]
    public void ResolveConnectionStringBuildsFromIndividualSettings()
    {
        var options = new PostgreSqlOptions
        {
            Host = "localhost",
            Port = 5432,
            Database = "movieapp",
            Username = "movieapp",
            Password = "secret"
        };

        var resolved = options.ResolveConnectionString();
        var builder = new NpgsqlConnectionStringBuilder(resolved);

        Assert.Equal("localhost", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("movieapp", builder.Database);
        Assert.Equal("movieapp", builder.Username);
        Assert.Equal(GssEncryptionMode.Disable, builder.GssEncryptionMode);
    }

    [Fact]
    public void IsConfiguredReturnsFalseWhenNoSettingsProvided()
    {
        var options = new PostgreSqlOptions();

        Assert.False(options.IsConfigured());
    }
}

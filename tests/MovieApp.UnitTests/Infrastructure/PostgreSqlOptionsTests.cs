using MovieApp.Infrastructure.Configuration;

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

        Assert.Equal(options.ConnectionString, options.ResolveConnectionString());
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

        Assert.Equal(
            "Host=localhost;Port=5432;Database=movieapp;Username=movieapp;Password=secret",
            options.ResolveConnectionString());
    }

    [Fact]
    public void IsConfiguredReturnsFalseWhenNoSettingsProvided()
    {
        var options = new PostgreSqlOptions();

        Assert.False(options.IsConfigured());
    }
}

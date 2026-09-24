using MovieApp.LoadTestIdentityProvisioner;
using Npgsql;

namespace MovieApp.LoadTestIdentityProvisioner.Tests;

public sealed class PostgreSqlConnectionStringNormalizerTests
{
    [Fact]
    public void NormalizesPostgresUriWithDefaultPort()
    {
        var normalized = PostgreSqlConnectionStringNormalizer.Normalize(
            "postgresql://loaduser:p%40ss%2Fword@dpg-example.render.com/movieapp_db");

        var builder = new NpgsqlConnectionStringBuilder(normalized);
        Assert.Equal("dpg-example.render.com", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("movieapp_db", builder.Database);
        Assert.Equal("loaduser", builder.Username);
        Assert.Equal("p@ss/word", builder.Password);
        Assert.Equal(SslMode.Require, builder.SslMode);
    }

    [Fact]
    public void NormalizesPostgresUriWithExplicitPort()
    {
        var normalized = PostgreSqlConnectionStringNormalizer.Normalize(
            "postgres://user:secret@db.example.com:6543/mydb");

        var builder = new NpgsqlConnectionStringBuilder(normalized);
        Assert.Equal(6543, builder.Port);
        Assert.Equal("mydb", builder.Database);
    }

    [Fact]
    public void PassesThroughValidKeyValueString()
    {
        const string cs = "Host=localhost;Port=5432;Database=test;Username=u;Password=p";
        var normalized = PostgreSqlConnectionStringNormalizer.Normalize(cs);
        Assert.Equal(cs, normalized);
    }
}

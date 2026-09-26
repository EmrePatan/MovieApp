using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class UnicodeSearchProbeIntegrationTests
{
    [Fact]
    public async Task PostgreSqlReportsDatabaseCollationAndEncoding()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var encoding = connection.CreateCommand();
        encoding.CommandText = "SHOW server_encoding";
        var serverEncoding = (string?)await encoding.ExecuteScalarAsync();
        Assert.Equal("UTF8", serverEncoding);

        await using var collate = connection.CreateCommand();
        collate.CommandText =
            "SELECT datcollate, datctype FROM pg_database WHERE datname = current_database()";
        await using var reader = await collate.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        _ = reader.GetString(0);
        _ = reader.GetString(1);
    }

    [Theory]
    [InlineData("I", "ı", false)]
    [InlineData("I", "i", true)]
    [InlineData("İ", "i", true)]
    [InlineData("İ", "ı", false)]
    [InlineData("ıslık", "I", false)]
    [InlineData("ıslık", "i", false)]
    [InlineData("ıslık", "ı", true)]
    [InlineData("İstanbul", "istanbul", true)]
    public async Task PostgreSqlILikeMatrixDocumentsBehavior(
        string title,
        string needle,
        bool expectedMatch)
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT @title ILIKE '%' || @needle || '%'";
        var titleParam = command.CreateParameter();
        titleParam.ParameterName = "title";
        titleParam.Value = title;
        command.Parameters.Add(titleParam);
        var needleParam = command.CreateParameter();
        needleParam.ParameterName = "needle";
        needleParam.Value = needle;
        command.Parameters.Add(needleParam);

        var result = (bool?)await command.ExecuteScalarAsync();
        Assert.Equal(expectedMatch, result);
    }
}

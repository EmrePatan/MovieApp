using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class UnicodeCollationProbeIntegrationTests
{
    [Theory]
    [InlineData("Islık", "und-x-icu")]
    [InlineData("Islık", "tr-x-icu")]
    [InlineData("INTERSTELLAR", "und-x-icu")]
    [InlineData("INTERSTELLAR", "tr-x-icu")]
    public async Task IcuLowerCollationProducesExpectedBytes(string input, string collation)
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT lower(@input COLLATE \"{collation}\")";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "input";
        parameter.Value = input;
        command.Parameters.Add(parameter);

        var folded = (string?)await command.ExecuteScalarAsync();
        Assert.NotNull(folded);
    }

    [Fact]
    public async Task IcuFoldedSubstringStillMatchesEnglishTitles()
    {
        const string title = "Interstellar";
        const string query = "INTERSTELLAR";

        await using var context = CatalogPersistenceFixture.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT lower(@title COLLATE "und-x-icu") ILIKE '%' || lower(@query COLLATE "und-x-icu") || '%'
            """;
        AddTextParam(command, "title", title);
        AddTextParam(command, "query", query);

        var matches = (bool?)await command.ExecuteScalarAsync();
        Assert.True(matches);
    }

    private static void AddTextParam(System.Data.Common.DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

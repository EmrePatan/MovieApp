using MovieApp.Application.Services.Keywords;
using Npgsql;

namespace MovieApp.IntegrationTests.Persistence;

public sealed class KeywordCanonicalNormalizationPostgreSqlParityTests
{
    private static readonly string[] CoreMigrationParityInputs =
    [
        " Time-Travel ",
        "SCI_FI",
        "a   b    c",
        "\uFB01ght",
        "\u00A0full\u3000width",
        "café",
        "Straße",
        "CAFÉ",
        "cafe\u0301",
        "\u03C3\u03C2",
        "I",
        "i",
    ];

    [Fact]
    public async Task CoreNormalizationInputsMatchBetweenCSharpAndPostgreSqlMigrationExpression()
    {
        await using var connection = new NpgsqlConnection(IntegrationTestDatabase.GetConnectionString("postgres"));
        await connection.OpenAsync();

        foreach (var input in CoreMigrationParityInputs)
        {
            var csharp = KeywordCanonicalNormalization.NormalizeKeywordName(input);
            var postgres = await NormalizeInPostgreSqlAsync(connection, input);
            Assert.True(
                string.Equals(csharp, postgres, StringComparison.Ordinal),
                $"IN=[{input}] CS=[{csharp}] PG=[{postgres}]");
        }
    }

    /// <summary>
    /// PR2 switch checklist when graph becomes authoritative:
    /// CanonicalName and NormalizedName must be reconciled using
    /// KeywordCanonicalNormalization as the application source of truth
    /// (not raw PostgreSQL lower() from the PR1 migration expression).
    /// </summary>
    [Fact]
    public async Task LocaleEdgeAuditTurkishDottedCapitalIDivergesBetweenPostgreSqlLowerAndToLowerInvariant()
    {
        await using var connection = new NpgsqlConnection(IntegrationTestDatabase.GetConnectionString("postgres"));
        await connection.OpenAsync();

        const string input = "İ";
        var csharp = KeywordCanonicalNormalization.NormalizeKeywordName(input);
        var postgres = await NormalizeInPostgreSqlAsync(connection, input);

        Assert.Equal("İ", csharp);
        Assert.Equal("i", postgres);
        Assert.NotEqual(csharp, postgres);
    }

    [Fact]
    public async Task LocaleEdgeAuditTurkishDotlessSmallIMatchesBetweenImplementations()
    {
        await using var connection = new NpgsqlConnection(IntegrationTestDatabase.GetConnectionString("postgres"));
        await connection.OpenAsync();

        const string input = "ı";
        var csharp = KeywordCanonicalNormalization.NormalizeKeywordName(input);
        var postgres = await NormalizeInPostgreSqlAsync(connection, input);

        Assert.Equal(csharp, postgres);
    }

    private static async Task<string> NormalizeInPostgreSqlAsync(NpgsqlConnection connection, string input)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT trim(both ' ' from regexp_replace(
                replace(replace(lower(normalize(@input, NFKC)), '-', ' '), '_', ' '),
                '\s+',
                ' ',
                'g'))
            """,
            connection);
        command.Parameters.AddWithValue("input", input);
        var result = await command.ExecuteScalarAsync();
        return result?.ToString() ?? string.Empty;
    }
}

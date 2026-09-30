namespace MovieApp.UnitTests.Migrations;

public sealed class MakeKeywordTmdbKeywordIdNullableMigrationTests
{
    [Fact]
    public void MigrationMakesTmdbKeywordIdNullableWithoutDataRewrite()
    {
        var migrationPath = LocateMigrationFile("20260930120011_MakeKeywordTmdbKeywordIdNullable.cs");
        var migrationSource = File.ReadAllText(migrationPath);

        Assert.Contains("nullable: true", migrationSource);
        Assert.Contains("name: \"TmdbKeywordId\"", migrationSource);
        Assert.DoesNotContain("UPDATE keywords", migrationSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP INDEX", migrationSource, StringComparison.OrdinalIgnoreCase);
    }

    private static string LocateMigrationFile(string fileName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "MovieApp.Infrastructure",
                "Persistence",
                "Migrations",
                fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not locate migration file {fileName}.");
    }
}

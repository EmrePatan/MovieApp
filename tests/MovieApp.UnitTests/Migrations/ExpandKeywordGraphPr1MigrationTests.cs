namespace MovieApp.UnitTests.Migrations;

public sealed class ExpandKeywordGraphPr1MigrationTests
{
    [Fact]
    public void MigrationExpandsKeywordGraphWithoutDestructiveDataOperations()
    {
        var migrationPath = LocateMigrationFile("20260930100106_ExpandKeywordGraphPr1.cs");
        var migrationSource = File.ReadAllText(migrationPath);

        Assert.Contains("name: \"keyword_external_references\"", migrationSource);
        Assert.Contains("name: \"movie_keyword_sources\"", migrationSource);
        Assert.Contains("name: \"tv_show_keyword_sources\"", migrationSource);
        Assert.Contains("MdbListKeywordsSyncedAtUtc", migrationSource);
        Assert.Contains("IX_keywords_NormalizedName", migrationSource);
        Assert.Contains("CanonicalName", migrationSource);
        Assert.Contains("INSERT INTO keyword_external_references", migrationSource);
        Assert.Contains("UPDATE keywords", migrationSource);

        Assert.DoesNotContain("DropTable(\n                name: \"keywords\"", migrationSource);
        Assert.DoesNotContain("DropTable(\n                name: \"movie_keywords\"", migrationSource);
        Assert.DoesNotContain("DropTable(\n                name: \"tv_show_keywords\"", migrationSource);
        Assert.DoesNotContain("DROP INDEX \"IX_keywords_TmdbKeywordId\"", migrationSource);
        Assert.DoesNotContain("DELETE FROM", migrationSource, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MigrationDoesNotBackfillKeywordSourceTables()
    {
        var migrationSource = File.ReadAllText(LocateMigrationFile("20260930100106_ExpandKeywordGraphPr1.cs"));
        Assert.DoesNotContain("INSERT INTO movie_keyword_sources", migrationSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO tv_show_keyword_sources", migrationSource, StringComparison.OrdinalIgnoreCase);
    }

    private static string LocateMigrationFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "MovieApp.Infrastructure",
                "Persistence",
                "Migrations",
                fileName);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate migration file '{fileName}'.");
    }
}

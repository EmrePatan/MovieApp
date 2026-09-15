namespace MovieApp.UnitTests.Migrations;

public sealed class AddCatalogKeywordsMigrationTests
{
    [Fact]
    public void MigrationAddsKeywordTablesAndSyncColumns()
    {
        var migrationPath = LocateMigrationFile("20260915095315_AddCatalogKeywords.cs");
        var migrationSource = File.ReadAllText(migrationPath);

        Assert.Contains("name: \"keywords\"", migrationSource);
        Assert.Contains("name: \"movie_keywords\"", migrationSource);
        Assert.Contains("name: \"tv_show_keywords\"", migrationSource);
        Assert.Contains("KeywordsSyncedAtUtc", migrationSource);
        Assert.Contains("IX_keywords_TmdbKeywordId", migrationSource);
        Assert.Contains("migrationBuilder.DropTable", migrationSource);
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

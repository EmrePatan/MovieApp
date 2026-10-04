namespace MovieApp.UnitTests.Migrations;

public sealed class ExtendKeywordClassificationStatusMigrationTests
{
    [Fact]
    public void KeywordClassificationStatusRemainsStoredOnKeywordsTableWithoutDestructiveChanges()
    {
        var migrationSource = File.ReadAllText(LocateMigrationFile());

        Assert.DoesNotContain("DROP TABLE", migrationSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM keywords", migrationSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP COLUMN", migrationSource, StringComparison.OrdinalIgnoreCase);
    }

    private static string LocateMigrationFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var migrationsDirectory = Path.Combine(
                directory.FullName,
                "src",
                "MovieApp.Infrastructure",
                "Persistence",
                "Migrations");
            if (!Directory.Exists(migrationsDirectory))
            {
                directory = directory.Parent;
                continue;
            }

            var candidates = Directory.GetFiles(
                migrationsDirectory,
                "*ExtendKeywordClassificationStatus*.cs");

            var migration = candidates.FirstOrDefault(path => !path.EndsWith("Designer.cs", StringComparison.Ordinal));
            if (migration is not null)
            {
                return migration;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate ExtendKeywordClassificationStatus migration.");
    }
}

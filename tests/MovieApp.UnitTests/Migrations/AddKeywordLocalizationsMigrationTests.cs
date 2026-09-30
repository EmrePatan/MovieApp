namespace MovieApp.UnitTests.Migrations;

public sealed class AddKeywordLocalizationsMigrationTests
{
    [Fact]
    public void MigrationCreatesKeywordLocalizationsWithoutTouchingCanonicalKeywords()
    {
        var migrationSource = File.ReadAllText(LocateMigrationFile());

        Assert.Contains("name: \"keyword_localizations\"", migrationSource);
        Assert.Contains("IX_keyword_localizations_Locale_NormalizedName", migrationSource);
        Assert.DoesNotContain("DROP TABLE \"keywords\"", migrationSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO keyword_localizations", migrationSource, StringComparison.OrdinalIgnoreCase);
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
                "*AddKeywordLocalizations*.cs");

            var migration = candidates.FirstOrDefault(path => !path.EndsWith("Designer.cs", StringComparison.Ordinal));
            if (migration is not null)
            {
                return migration;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate AddKeywordLocalizations migration.");
    }
}

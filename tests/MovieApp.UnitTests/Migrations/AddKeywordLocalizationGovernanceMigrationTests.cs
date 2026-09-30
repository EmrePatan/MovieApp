namespace MovieApp.UnitTests.Migrations;

public sealed class AddKeywordLocalizationGovernanceMigrationTests
{
    [Fact]
    public void MigrationAddsGovernanceColumnsAndProtectsPreExistingRows()
    {
        var migrationSource = File.ReadAllText(LocateMigrationFile());

        Assert.Contains("TranslationSource", migrationSource);
        Assert.Contains("ReviewStatus", migrationSource);
        Assert.Contains("SourceTextHash", migrationSource);
        Assert.Contains("UPDATE keyword_localizations", migrationSource, StringComparison.Ordinal);
        Assert.Contains("\"ReviewStatus\" = 1", migrationSource, StringComparison.Ordinal);
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
                "*AddKeywordLocalizationGovernance*.cs");

            var migration = candidates.FirstOrDefault(path => !path.EndsWith("Designer.cs", StringComparison.Ordinal));
            if (migration is not null)
            {
                return migration;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate AddKeywordLocalizationGovernance migration.");
    }
}

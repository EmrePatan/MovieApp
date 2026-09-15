namespace MovieApp.UnitTests.Migrations;

public sealed class AddMovieRegionalReleasesMigrationTests
{
    [Fact]
    public void MigrationAddsMovieRegionalReleasesTableAndIndex()
    {
        var migrationPath = LocateMigrationFile("20260915110524_AddMovieRegionalReleases.cs");
        var migrationSource = File.ReadAllText(migrationPath);

        Assert.Contains("name: \"movie_regional_releases\"", migrationSource);
        Assert.Contains("EffectiveReleaseDate", migrationSource);
        Assert.Contains("EffectiveReleaseType", migrationSource);
        Assert.Contains("Certification", migrationSource);
        Assert.Contains("IsFallbackGlobal", migrationSource);
        Assert.Contains("SyncedAtUtc", migrationSource);
        Assert.Contains("IX_movie_regional_releases_Region_EffectiveReleaseDate", migrationSource);
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

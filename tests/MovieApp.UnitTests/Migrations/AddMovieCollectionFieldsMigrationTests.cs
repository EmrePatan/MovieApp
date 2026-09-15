namespace MovieApp.UnitTests.Migrations;

public sealed class AddMovieCollectionFieldsMigrationTests
{
    [Fact]
    public void Migration_AddsMovieCollectionSummaryColumns()
    {
        var migrationSource = FindMigrationSource();

        Assert.Contains("CollectionBackdropPath", migrationSource, StringComparison.Ordinal);
        Assert.Contains("CollectionName", migrationSource, StringComparison.Ordinal);
        Assert.Contains("CollectionPosterPath", migrationSource, StringComparison.Ordinal);
        Assert.Contains("TmdbCollectionId", migrationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateTable", migrationSource, StringComparison.Ordinal);
    }

    private static string FindMigrationSource()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var migrationsDirectory = Path.Combine(
                current.FullName,
                "src",
                "MovieApp.Infrastructure",
                "Persistence",
                "Migrations");

            if (Directory.Exists(migrationsDirectory))
            {
                var migrationFile = Directory
                    .GetFiles(migrationsDirectory, "*AddMovieCollectionFields.cs")
                    .SingleOrDefault();

                if (migrationFile is not null)
                {
                    return File.ReadAllText(migrationFile);
                }
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("AddMovieCollectionFields migration file was not found.");
    }
}

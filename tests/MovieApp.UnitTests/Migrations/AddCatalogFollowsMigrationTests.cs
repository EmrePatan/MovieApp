namespace MovieApp.UnitTests.Migrations;

public sealed class AddCatalogFollowsMigrationTests
{
    [Fact]
    public void Migration_CopiesTvShowFollowRowsBeforeDrop()
    {
        var migrationSource = FindMigrationSource();

        Assert.Contains("INSERT INTO catalog_follows", migrationSource, StringComparison.Ordinal);
        Assert.Contains("FROM tv_show_follows", migrationSource, StringComparison.Ordinal);
        Assert.Contains("'Tv'", migrationSource, StringComparison.Ordinal);
        Assert.Contains("\"NotifyFromUtc\"", migrationSource, StringComparison.Ordinal);
        Assert.Contains("\"BaselineEstablishedAtUtc\"", migrationSource, StringComparison.Ordinal);
        Assert.Contains("\"NotifyNewSeasons\"", migrationSource, StringComparison.Ordinal);
        Assert.Contains("\"NotifyNewEpisodes\"", migrationSource, StringComparison.Ordinal);
        Assert.Contains("FALSE", migrationSource, StringComparison.Ordinal);

        var insertIndex = migrationSource.IndexOf("INSERT INTO catalog_follows", StringComparison.Ordinal);
        var dropIndex = migrationSource.IndexOf("name: \"tv_show_follows\"", StringComparison.Ordinal);
        Assert.True(insertIndex >= 0);
        Assert.True(dropIndex > insertIndex, "TV follow rows must be copied before tv_show_follows is dropped.");
    }

    private static string FindMigrationSource()
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
                "20260914215350_AddCatalogFollows.cs");

            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("AddCatalogFollows migration file was not found.");
    }
}

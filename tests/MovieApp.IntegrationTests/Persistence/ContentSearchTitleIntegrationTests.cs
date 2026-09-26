using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Common;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence.Repositories;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class ContentSearchTitleIntegrationTests
{
    [Fact]
    public async Task SyncCatalogTitlesCreatesCanonicalAndOriginalRowsForMovie()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var synchronizer = new ContentSearchTitleSynchronizer(context);
        var movieId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Whistle If You Come Back",
            OriginalTitle = "Dönersen Islık Çal",
            ReleaseDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 7,
            VoteCount = 10,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        await synchronizer.SyncCatalogTitlesAsync(
            CatalogContentType.Movie,
            movieId,
            "Whistle If You Come Back",
            "Dönersen Islık Çal");

        var rows = await context.ContentSearchTitles
            .Where(row => row.ContentId == movieId)
            .OrderBy(row => row.TitleKind)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row =>
            row.TitleKind == ContentSearchTitleKind.Canonical
            && row.Source == ContentSearchTitleSource.CatalogCanonical
            && row.NormalizedTitle == SearchTitleFolder.Fold("Whistle If You Come Back"));
        Assert.Contains(rows, row =>
            row.TitleKind == ContentSearchTitleKind.Original
            && row.Source == ContentSearchTitleSource.CatalogOriginal
            && row.NormalizedTitle == SearchTitleFolder.Fold("Dönersen Islık Çal"));
    }

    [Fact]
    public async Task SyncFromProviderDetailPersistsAlternativeAndTranslationRows()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var synchronizer = new ContentSearchTitleSynchronizer(context);
        var movieId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Whistle If You Come Back",
            OriginalTitle = "Original",
            ReleaseDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 7,
            VoteCount = 10,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        var providerTitles = new[]
        {
            new ProviderSearchTitleEntry(
                "Dönersen Islık Çal",
                ContentSearchTitleKind.Alternative,
                ContentSearchTitleSource.TmdbAlternative,
                null,
                "TR",
                "working"),
            new ProviderSearchTitleEntry(
                "Dönersen Islık Çal",
                ContentSearchTitleKind.Translation,
                ContentSearchTitleSource.TmdbTranslation,
                "tr",
                "TR",
                null),
        };

        await synchronizer.SyncFromProviderDetailAsync(
            CatalogContentType.Movie,
            movieId,
            "Whistle If You Come Back",
            "Original",
            providerTitles,
            utcNow);

        var rows = await context.ContentSearchTitles
            .Where(row => row.ContentId == movieId)
            .ToListAsync();

        Assert.Equal(3, rows.Count);
        Assert.Single(rows, row => row.Source == ContentSearchTitleSource.TmdbAlternative);
        Assert.DoesNotContain(rows, row => row.Source == ContentSearchTitleSource.TmdbTranslation);
    }

    [Fact]
    public async Task SyncFromProviderDetailRemovesStaleProviderAliases()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var synchronizer = new ContentSearchTitleSynchronizer(context);
        var movieId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Whistle If You Come Back",
            ReleaseDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 7,
            VoteCount = 10,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        await synchronizer.SyncFromProviderDetailAsync(
            CatalogContentType.Movie,
            movieId,
            "Whistle If You Come Back",
            null,
            [new ProviderSearchTitleEntry("Old Alias", ContentSearchTitleKind.Alternative, ContentSearchTitleSource.TmdbAlternative, null, "US", null)],
            utcNow);

        await synchronizer.SyncFromProviderDetailAsync(
            CatalogContentType.Movie,
            movieId,
            "Whistle If You Come Back",
            null,
            [new ProviderSearchTitleEntry("New Alias", ContentSearchTitleKind.Alternative, ContentSearchTitleSource.TmdbAlternative, null, "US", null)],
            utcNow);

        var providerRows = await context.ContentSearchTitles
            .Where(row => row.ContentId == movieId && row.Source == ContentSearchTitleSource.TmdbAlternative)
            .ToListAsync();

        Assert.Single(providerRows);
        Assert.Equal("New Alias", providerRows[0].Title);
    }

    [Fact]
    public async Task DeleteAllForContentRemovesSearchTitleRows()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var synchronizer = new ContentSearchTitleSynchronizer(context);
        var movieId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Whistle If You Come Back",
            ReleaseDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 7,
            VoteCount = 10,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        await synchronizer.SyncCatalogTitlesAsync(
            CatalogContentType.Movie,
            movieId,
            "Whistle If You Come Back",
            null);

        await synchronizer.DeleteAllForContentAsync(CatalogContentType.Movie, movieId);

        Assert.Empty(await context.ContentSearchTitles.Where(row => row.ContentId == movieId).ToListAsync());
    }

    [Fact]
    public async Task CatalogBackfillPopulatesCanonicalRowsFromExistingMovies()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var utcNow = DateTime.UtcNow;
        var movieId = Guid.NewGuid();
        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Existing Catalog Title",
            OriginalTitle = "Original Catalog",
            ReleaseDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 1,
            VoteCount = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        var backfill = new ContentSearchTitleCatalogBackfillService(
            context,
            new ContentSearchTitleSynchronizer(context));
        await backfill.BackfillCanonicalAndOriginalAsync();

        var rows = await context.ContentSearchTitles
            .Where(row => row.ContentId == movieId)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task TmdbMovieMapperMapsAlternativeTitlesAndTranslations()
    {
        var details = new TmdbMovieDetailsResponseJson
        {
            Id = 1,
            Title = "Whistle If You Come Back",
            AlternativeTitles = new TmdbMovieAlternativeTitlesAppendJson
            {
                Titles =
                [
                    new TmdbAlternativeTitleJson { Iso31661 = "TR", Title = "Dönersen Islık Çal", Type = "working" }
                ]
            },
            Translations = new TmdbTranslationsAppendJson
            {
                Translations =
                [
                    new TmdbTranslationJson
                    {
                        Iso31661 = "TR",
                        Iso6391 = "tr",
                        Data = new TmdbMovieTranslationDataJson { Title = "Dönersen Islık Çal" }
                    }
                ]
            }
        };

        var mapped = TmdbContentSearchTitleMapper.MapMovie(details);

        Assert.Equal(2, mapped.Count);
        Assert.Contains(mapped, entry => entry.Source == ContentSearchTitleSource.TmdbAlternative);
        Assert.Contains(mapped, entry => entry.Source == ContentSearchTitleSource.TmdbTranslation);
    }

    [Fact]
    public async Task NormalizedTitleTrigramIndexSupportsIlikeProbe()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var seed = connection.CreateCommand();
        seed.CommandText =
            """
            INSERT INTO content_search_titles
                ("Id", "ContentType", "ContentId", "Title", "NormalizedTitle", "TitleKind", "Source", "CreatedAtUtc", "UpdatedAtUtc")
            SELECT
                gen_random_uuid(),
                0,
                gen_random_uuid(),
                'probe-' || gs,
                'donersen islik probe ' || gs,
                2,
                2,
                NOW(),
                NOW()
            FROM generate_series(1, 2500) AS gs;
            """;
        await seed.ExecuteNonQueryAsync();

        await using var explain = connection.CreateCommand();
        explain.CommandText =
            """
            EXPLAIN (FORMAT TEXT)
            SELECT "Id"
            FROM content_search_titles
            WHERE "NormalizedTitle" ILIKE '%islik%'
            LIMIT 20;
            """;
        string plan;
        await using (var reader = await explain.ExecuteReaderAsync())
        {
            var planLines = new List<string>();
            while (await reader.ReadAsync())
            {
                planLines.Add(reader.GetString(0));
            }

            plan = string.Join(Environment.NewLine, planLines);
        }

        Assert.Contains("content_search_titles", plan, StringComparison.OrdinalIgnoreCase);

        await using var cleanup = connection.CreateCommand();
        cleanup.CommandText = "DELETE FROM content_search_titles WHERE \"NormalizedTitle\" LIKE 'donersen islik probe %';";
        await cleanup.ExecuteNonQueryAsync();
    }
}

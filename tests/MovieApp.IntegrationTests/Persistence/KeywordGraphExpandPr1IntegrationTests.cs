using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Keywords;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class KeywordGraphExpandPr1IntegrationTests
{
    [Fact]
    public async Task LegacyTmdbSyncMirrorsTmdbSourcesWhenProviderAwareFlagIsFalse()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = Random.Shared.Next(70_000_000, 79_999_999),
            Title = "Legacy keyword sync",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Movies.Add(movie);
        await context.SaveChangesAsync();

        var repository = new KeywordCatalogRepository(context, Options.Create(new KeywordGraphOptions()));
        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [new ProviderKeywordSummary(12345, "probe")],
            DateTime.UtcNow);

        var movieRelationships = await context.MovieKeywords
            .AsNoTracking()
            .Where(join => join.MovieId == movie.Id)
            .ToListAsync();

        Assert.Single(movieRelationships);
        Assert.True(KeywordProviderSources.Contains(movieRelationships[0].Sources, KeywordProvider.Tmdb));
        Assert.Equal(0, await context.TvShowKeywords.CountAsync());
    }

    [Fact]
    public async Task MdbListKeywordsSyncedAtUtcDefaultsToNullOnNewMovie()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = Random.Shared.Next(10_000_000, 99_999_999),
            Title = "Keyword Graph PR1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Movies.Add(movie);
        await context.SaveChangesAsync();

        var stored = await context.Movies.AsNoTracking().SingleAsync(m => m.Id == movie.Id);
        Assert.Null(stored.MdbListKeywordsSyncedAtUtc);
    }

    [Fact]
    public async Task NormalizedNameAllowsDuplicatesAcrossKeywords()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var now = DateTime.UtcNow;
        var normalized = "shared normalized phrase";
        context.Keywords.AddRange(
            new Keyword
            {
                Id = Guid.NewGuid(),
                TmdbKeywordId = Random.Shared.Next(1_000_000, 2_000_000),
                Name = "Shared Normalized Phrase",
                CanonicalName = "Shared Normalized Phrase",
                NormalizedName = normalized,
                SemanticCategory = KeywordSemanticCategory.Unknown,
                ClassificationStatus = KeywordClassificationStatus.Auto,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new Keyword
            {
                Id = Guid.NewGuid(),
                TmdbKeywordId = Random.Shared.Next(2_000_000, 3_000_000),
                Name = "shared-normalized-phrase",
                CanonicalName = "shared-normalized-phrase",
                NormalizedName = normalized,
                SemanticCategory = KeywordSemanticCategory.Unknown,
                ClassificationStatus = KeywordClassificationStatus.Auto,
                CreatedAt = now,
                UpdatedAt = now,
            });
        await context.SaveChangesAsync();

        var count = await context.Keywords.CountAsync(keyword => keyword.NormalizedName == normalized);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ExternalReferenceAllowsSameExternalIdAcrossProviders()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var now = DateTime.UtcNow;
        var keywordA = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = Random.Shared.Next(3_000_000, 4_000_000),
            Name = "alpha",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var keywordB = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = Random.Shared.Next(4_000_000, 5_000_000),
            Name = "beta",
            CreatedAt = now,
            UpdatedAt = now,
        };
        context.Keywords.AddRange(keywordA, keywordB);
        context.KeywordExternalReferences.AddRange(
            new KeywordExternalReference
            {
                KeywordId = keywordA.Id,
                Provider = KeywordProvider.Tmdb,
                ExternalId = "42",
                ExternalName = "alpha",
                CreatedAt = now,
            },
            new KeywordExternalReference
            {
                KeywordId = keywordB.Id,
                Provider = KeywordProvider.MdbList,
                ExternalId = "42",
                ExternalName = "beta",
                CreatedAt = now,
            });
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.KeywordExternalReferences.CountAsync(reference => reference.ExternalId == "42"));
    }

    [Fact]
    public async Task MigrationBackfillSqlAlignsTmdbExternalReferenceWithKeywordIdentity()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = Random.Shared.Next(60_000_000, 69_999_999),
            Title = "Keyword backfill probe",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Movies.Add(movie);
        await context.SaveChangesAsync();

        var repository = new KeywordCatalogRepository(context, Options.Create(new KeywordGraphOptions()));
        var syncedAtUtc = DateTime.UtcNow;
        var tmdbKeywordId = Random.Shared.Next(6_000_000, 6_999_999);
        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [new ProviderKeywordSummary(tmdbKeywordId, " Time-Travel ")],
            syncedAtUtc);

        var keyword = await context.Keywords.AsNoTracking().SingleAsync(item => item.TmdbKeywordId == tmdbKeywordId);
        var keywordId = keyword.Id;
        var originalName = keyword.Name;
        var reference = await context.KeywordExternalReferences
            .AsNoTracking()
            .SingleAsync(item => item.KeywordId == keywordId && item.Provider == KeywordProvider.Tmdb);

        Assert.Equal(originalName, keyword.Name);
        Assert.Equal(keywordId, keyword.Id);
        Assert.Equal(tmdbKeywordId, keyword.TmdbKeywordId);
        Assert.Equal(keyword.Name, keyword.CanonicalName);
        Assert.Equal("time travel", keyword.NormalizedName);
        Assert.Equal(tmdbKeywordId.ToString(CultureInfo.InvariantCulture), reference.ExternalId);
        Assert.Equal(keyword.Name, reference.ExternalName);
    }
}

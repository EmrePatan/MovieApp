using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Keywords;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Keywords;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Keywords;

public sealed class KeywordGraphProviderNeutralReadinessTests
{
    [Fact]
    public async Task TmdbBackedKeywordWithExternalRefIsReady()
    {
        await using var context = CreateContext();
        var keyword = await SeedTmdbKeywordAsync(context, 42, "time travel");
        context.KeywordExternalReferences.Add(new KeywordExternalReference
        {
            KeywordId = keyword.Id,
            Provider = KeywordProvider.Tmdb,
            ExternalId = "42",
            ExternalName = keyword.Name,
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.True(result.IsReadyForProviderAwareSync);
        Assert.Equal(0, result.MissingTmdbExternalRefCount);
    }

    [Fact]
    public async Task ProviderNeutralKeywordWithoutTmdbIdDoesNotFailReadiness()
    {
        await using var context = CreateContext();
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = null,
            Name = "mdb-only",
            CanonicalName = "mdb-only",
            NormalizedName = KeywordCanonicalNormalization.NormalizeKeywordName("mdb-only"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.Add(keyword);
        context.KeywordExternalReferences.Add(new KeywordExternalReference
        {
            KeywordId = keyword.Id,
            Provider = KeywordProvider.MdbList,
            ExternalId = "mdb-42",
            ExternalName = "mdb-only",
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.True(result.IsReadyForProviderAwareSync);
        Assert.Equal(0, result.MissingTmdbExternalRefCount);
    }

    [Fact]
    public async Task NullTmdbKeywordIdWithTmdbExternalReferenceFailsReadiness()
    {
        await using var context = CreateContext();
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = null,
            Name = "broken",
            CanonicalName = "broken",
            NormalizedName = "broken",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.Add(keyword);
        context.KeywordExternalReferences.Add(new KeywordExternalReference
        {
            KeywordId = keyword.Id,
            Provider = KeywordProvider.Tmdb,
            ExternalId = "1",
            ExternalName = "broken",
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.False(result.IsReadyForProviderAwareSync);
        Assert.NotEmpty(result.Conflicts);
    }

    [Fact]
    public async Task TmdbKeywordIdWithoutExternalRefIncreasesMissingTmdbRefCount()
    {
        await using var context = CreateContext();
        await SeedTmdbKeywordAsync(context, 77, "missing ref");
        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.Equal(1, result.MissingTmdbExternalRefCount);
        Assert.False(result.IsReadyForProviderAwareSync);
    }

    [Fact]
    public async Task MovieRelationshipWithoutSourcesFailsReadiness()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = await SeedTmdbKeywordAsync(context, 5, "orphan");
        context.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = keyword.Id, Sources = "[]" });
        await context.SaveChangesAsync();

        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.Equal(1, result.MissingMovieKeywordSourceCount);
        Assert.False(result.IsReadyForProviderAwareSync);
    }

    [Fact]
    public async Task MovieRelationshipWithOnlyMdbListSourceIsReady()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            Name = "mdb",
            CanonicalName = "mdb",
            NormalizedName = "mdb",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.Add(keyword);
        context.MovieKeywords.Add(new MovieKeyword
        {
            MovieId = movie.Id,
            KeywordId = keyword.Id,
            Sources = KeywordProviderSources.Create(KeywordProvider.MdbList),
        });
        await context.SaveChangesAsync();

        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.Equal(0, result.MissingMovieKeywordSourceCount);
        Assert.True(result.IsReadyForProviderAwareSync);
    }

    [Fact]
    public async Task TvRelationshipWithoutSourcesFailsReadiness()
    {
        await using var context = CreateContext();
        var tvShow = await SeedTvShowAsync(context);
        var keyword = await SeedTmdbKeywordAsync(context, 8, "tv orphan");
        context.TvShowKeywords.Add(new TvShowKeyword { TvShowId = tvShow.Id, KeywordId = keyword.Id, Sources = "[]" });
        await context.SaveChangesAsync();

        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.Equal(1, result.MissingTvShowKeywordSourceCount);
        Assert.False(result.IsReadyForProviderAwareSync);
    }

    [Fact]
    public async Task ReconcileDoesNotInventTmdbOwnershipForMdbListOnlyRelationship()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            Name = "mdb join",
            CanonicalName = "mdb join",
            NormalizedName = "mdb join",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.Add(keyword);
        context.MovieKeywords.Add(new MovieKeyword
        {
            MovieId = movie.Id,
            KeywordId = keyword.Id,
            Sources = KeywordProviderSources.Create(KeywordProvider.MdbList),
        });
        await context.SaveChangesAsync();

        await new KeywordGraphReconciliationService(context).ReconcileAsync();

        var relationship = await context.MovieKeywords.SingleAsync();
        Assert.False(KeywordProviderSources.Contains(relationship.Sources, KeywordProvider.Tmdb));
        Assert.True(KeywordProviderSources.Contains(relationship.Sources, KeywordProvider.MdbList));
    }

    [Fact]
    public async Task EmbeddedSourcesCannotExistWithoutMaterializedJoin()
    {
        await using var context = CreateContext();
        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.Equal(0, result.MissingMovieMaterializedJoinCount);
        Assert.Equal(0, result.MissingTvShowMaterializedJoinCount);
    }

    [Fact]
    public async Task FullyHealthyEmbeddedGraphIsReady()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var tvShow = await SeedTvShowAsync(context);
        var movieKeyword = await SeedTmdbKeywordAsync(context, 20, "healthy movie");
        var tvKeyword = await SeedTmdbKeywordAsync(context, 21, "healthy tv");
        foreach (var keyword in new[] { movieKeyword, tvKeyword })
        {
            context.KeywordExternalReferences.Add(new KeywordExternalReference
            {
                KeywordId = keyword.Id,
                Provider = KeywordProvider.Tmdb,
                ExternalId = keyword.TmdbKeywordId!.Value.ToString(CultureInfo.InvariantCulture),
                ExternalName = keyword.Name,
                CreatedAt = DateTime.UtcNow,
            });
        }

        context.MovieKeywords.Add(new MovieKeyword
        {
            MovieId = movie.Id,
            KeywordId = movieKeyword.Id,
            Sources = KeywordProviderSources.Create(KeywordProvider.Tmdb),
        });
        context.TvShowKeywords.Add(new TvShowKeyword
        {
            TvShowId = tvShow.Id,
            KeywordId = tvKeyword.Id,
            Sources = KeywordProviderSources.Create(KeywordProvider.Tmdb),
        });
        await context.SaveChangesAsync();

        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.Equal(0, result.MissingMovieKeywordSourceCount);
        Assert.Equal(0, result.MissingTvShowKeywordSourceCount);
        Assert.Equal(0, result.MissingMovieMaterializedJoinCount);
        Assert.Equal(0, result.MissingTvShowMaterializedJoinCount);
        Assert.True(result.IsReadyForProviderAwareSync);
    }

    [Fact]
    public async Task ProviderAwareSyncPreservesMdbListSourceOnTmdbRefresh()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var mdbKeyword = await SeedTmdbKeywordAsync(context, 99, "mdb preserved");
        _ = await SeedTmdbKeywordAsync(context, 1, "tmdb");
        context.MovieKeywords.Add(new MovieKeyword
        {
            MovieId = movie.Id,
            KeywordId = mdbKeyword.Id,
            Sources = KeywordProviderSources.Create(KeywordProvider.MdbList),
        });
        await context.SaveChangesAsync();

        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context, providerAwareSyncEnabled: true);
        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [new ProviderKeywordSummary(1, "tmdb")],
            DateTime.UtcNow);

        var relationships = await context.MovieKeywords.Where(join => join.MovieId == movie.Id).ToListAsync();
        Assert.Contains(relationships, relationship =>
            relationship.KeywordId == mdbKeyword.Id &&
            KeywordProviderSources.Contains(relationship.Sources, KeywordProvider.MdbList));
        Assert.Equal(2, relationships.Count);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"keyword-neutral-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Movie> SeedMovieAsync(ApplicationDbContext context)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = Random.Shared.Next(1_000_000, 9_999_999),
            Title = "Test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Movies.Add(movie);
        await context.SaveChangesAsync();
        return movie;
    }

    private static async Task<TvShow> SeedTvShowAsync(ApplicationDbContext context)
    {
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = Random.Shared.Next(1_000_000, 9_999_999),
            Title = "Show",
            Status = TvShowStatus.Ended,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.TvShows.Add(tvShow);
        await context.SaveChangesAsync();
        return tvShow;
    }

    private static async Task<Keyword> SeedTmdbKeywordAsync(ApplicationDbContext context, int tmdbId, string name)
    {
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = tmdbId,
            Name = name,
            CanonicalName = name,
            NormalizedName = KeywordCanonicalNormalization.NormalizeKeywordName(name),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.Add(keyword);
        await context.SaveChangesAsync();
        return keyword;
    }
}

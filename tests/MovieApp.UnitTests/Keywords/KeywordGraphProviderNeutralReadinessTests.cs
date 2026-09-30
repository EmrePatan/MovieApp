using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
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
    public async Task OrphanMovieJoinIncreasesMissingMovieSourceCount()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = await SeedTmdbKeywordAsync(context, 5, "orphan");
        context.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = keyword.Id });
        await context.SaveChangesAsync();

        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.Equal(1, result.MissingMovieKeywordSourceCount);
        Assert.False(result.IsReadyForProviderAwareSync);
    }

    [Fact]
    public async Task MovieJoinWithOnlyMdbListSourceIsNotMissingSource()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = null,
            Name = "mdb",
            CanonicalName = "mdb",
            NormalizedName = "mdb",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.Add(keyword);
        context.MovieKeywordSources.Add(new MovieKeywordSource
        {
            MovieId = movie.Id,
            KeywordId = keyword.Id,
            Provider = KeywordProvider.MdbList,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        context.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = keyword.Id });
        await context.SaveChangesAsync();

        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.Equal(0, result.MissingMovieKeywordSourceCount);
        Assert.True(result.IsReadyForProviderAwareSync);
    }

    [Fact]
    public async Task OrphanTvJoinIncreasesMissingTvSourceCount()
    {
        await using var context = CreateContext();
        var tvShow = await SeedTvShowAsync(context);
        var keyword = await SeedTmdbKeywordAsync(context, 8, "tv orphan");
        context.TvShowKeywords.Add(new TvShowKeyword { TvShowId = tvShow.Id, KeywordId = keyword.Id });
        await context.SaveChangesAsync();

        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.Equal(1, result.MissingTvShowKeywordSourceCount);
    }

    [Fact]
    public async Task ReconcileDoesNotCreateTmdbSourceFromMdbListOnlyJoin()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = null,
            Name = "mdb join",
            CanonicalName = "mdb join",
            NormalizedName = "mdb join",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.Add(keyword);
        context.MovieKeywordSources.Add(new MovieKeywordSource
        {
            MovieId = movie.Id,
            KeywordId = keyword.Id,
            Provider = KeywordProvider.MdbList,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        context.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = keyword.Id });
        await context.SaveChangesAsync();

        await new KeywordGraphReconciliationService(context).ReconcileAsync();

        Assert.DoesNotContain(
            await context.MovieKeywordSources.ToListAsync(),
            source => source.Provider == KeywordProvider.Tmdb);
        Assert.Single(await context.MovieKeywordSources.Where(source => source.Provider == KeywordProvider.MdbList).ToListAsync());
    }

    [Fact]
    public async Task MovieSourceWithoutMaterializedJoinFailsReadiness()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = await SeedTmdbKeywordAsync(context, 11, "source only");
        context.MovieKeywordSources.Add(new MovieKeywordSource
        {
            MovieId = movie.Id,
            KeywordId = keyword.Id,
            Provider = KeywordProvider.Tmdb,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.Equal(1, result.MissingMovieMaterializedJoinCount);
        Assert.False(result.IsReadyForProviderAwareSync);
    }

    [Fact]
    public async Task MovieDualProviderSourcesWithOneMaterializedJoinIsNotMissingMaterializedJoin()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = await SeedTmdbKeywordAsync(context, 12, "dual source");
        context.MovieKeywordSources.AddRange(
            new MovieKeywordSource
            {
                MovieId = movie.Id,
                KeywordId = keyword.Id,
                Provider = KeywordProvider.Tmdb,
                FirstSeenAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            },
            new MovieKeywordSource
            {
                MovieId = movie.Id,
                KeywordId = keyword.Id,
                Provider = KeywordProvider.MdbList,
                FirstSeenAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            });
        context.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = keyword.Id });
        await context.SaveChangesAsync();

        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.Equal(0, result.MissingMovieMaterializedJoinCount);
    }

    [Fact]
    public async Task TvSourceWithoutMaterializedJoinFailsReadiness()
    {
        await using var context = CreateContext();
        var tvShow = await SeedTvShowAsync(context);
        var keyword = await SeedTmdbKeywordAsync(context, 13, "tv source only");
        context.TvShowKeywordSources.Add(new TvShowKeywordSource
        {
            TvShowId = tvShow.Id,
            KeywordId = keyword.Id,
            Provider = KeywordProvider.Tmdb,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.Equal(1, result.MissingTvShowMaterializedJoinCount);
        Assert.False(result.IsReadyForProviderAwareSync);
    }

    [Fact]
    public async Task TvDualProviderSourcesWithOneMaterializedJoinIsNotMissingMaterializedJoin()
    {
        await using var context = CreateContext();
        var tvShow = await SeedTvShowAsync(context);
        var keyword = await SeedTmdbKeywordAsync(context, 14, "tv dual");
        context.TvShowKeywordSources.AddRange(
            new TvShowKeywordSource
            {
                TvShowId = tvShow.Id,
                KeywordId = keyword.Id,
                Provider = KeywordProvider.Tmdb,
                FirstSeenAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            },
            new TvShowKeywordSource
            {
                TvShowId = tvShow.Id,
                KeywordId = keyword.Id,
                Provider = KeywordProvider.MdbList,
                FirstSeenAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            });
        context.TvShowKeywords.Add(new TvShowKeyword { TvShowId = tvShow.Id, KeywordId = keyword.Id });
        await context.SaveChangesAsync();

        var result = await new KeywordGraphReconciliationService(context).VerifyReadinessAsync();
        Assert.Equal(0, result.MissingTvShowMaterializedJoinCount);
    }

    [Fact]
    public async Task FullyHealthyBidirectionalGraphIsReady()
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

        context.MovieKeywordSources.Add(new MovieKeywordSource
        {
            MovieId = movie.Id,
            KeywordId = movieKeyword.Id,
            Provider = KeywordProvider.Tmdb,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        context.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = movieKeyword.Id });
        context.TvShowKeywordSources.Add(new TvShowKeywordSource
        {
            TvShowId = tvShow.Id,
            KeywordId = tvKeyword.Id,
            Provider = KeywordProvider.Tmdb,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        context.TvShowKeywords.Add(new TvShowKeyword { TvShowId = tvShow.Id, KeywordId = tvKeyword.Id });
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
        var tmdbKeyword = await SeedTmdbKeywordAsync(context, 1, "tmdb");
        context.MovieKeywordSources.Add(new MovieKeywordSource
        {
            MovieId = movie.Id,
            KeywordId = mdbKeyword.Id,
            Provider = KeywordProvider.MdbList,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        context.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = mdbKeyword.Id });
        await context.SaveChangesAsync();

        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context, providerAwareSyncEnabled: true);
        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [new ProviderKeywordSummary(1, "tmdb")],
            DateTime.UtcNow);

        Assert.Contains(
            await context.MovieKeywordSources.ToListAsync(),
            source => source.Provider == KeywordProvider.MdbList && source.KeywordId == mdbKeyword.Id);
        Assert.Equal(2, await context.MovieKeywords.CountAsync(join => join.MovieId == movie.Id));
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

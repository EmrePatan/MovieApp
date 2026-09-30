using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Keywords;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Application.Services.Keywords;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class KeywordCatalogRepositoryConcurrencyIntegrationTests
{
    [Fact]
    public async Task ConcurrentMovieKeywordSyncConvergesOnSharedTmdbKeywordIdAgainstPostgreSql()
    {
        await using var setupContext = CatalogPersistenceFixture.CreateContext();
        var movieA = await SeedMovieAsync(setupContext, 438631);
        var movieB = await SeedMovieAsync(setupContext, 438632);
        var sharedKeyword = new ProviderKeywordSummary(9715, "drug dealer");
        var syncedAtUtc = DateTime.UtcNow;
        using var barrier = new Barrier(2);

        await Task.WhenAll(
            RunConcurrentMovieSyncAsync(movieA.Id, sharedKeyword, syncedAtUtc, barrier),
            RunConcurrentMovieSyncAsync(movieB.Id, sharedKeyword, syncedAtUtc, barrier));

        await using var verifyContext = CatalogPersistenceFixture.CreateContext();
        var canonicalKeywords = await verifyContext.Keywords
            .Where(keyword => keyword.TmdbKeywordId == sharedKeyword.TmdbKeywordId)
            .ToListAsync();

        Assert.Single(canonicalKeywords);
        var canonicalKeywordId = canonicalKeywords[0].Id;

        var linkedMovieIds = await verifyContext.MovieKeywords
            .Where(movieKeyword => movieKeyword.KeywordId == canonicalKeywordId)
            .Select(movieKeyword => movieKeyword.MovieId)
            .ToListAsync();

        Assert.Equal(2, linkedMovieIds.Count);
        Assert.Contains(movieA.Id, linkedMovieIds);
        Assert.Contains(movieB.Id, linkedMovieIds);
        Assert.NotNull((await verifyContext.Movies.FindAsync(movieA.Id))!.KeywordsSyncedAtUtc);
        Assert.NotNull((await verifyContext.Movies.FindAsync(movieB.Id))!.KeywordsSyncedAtUtc);
    }

    [Fact]
    public async Task ConcurrentMdbListKeywordIngestionDoesNotDuplicateExternalReferenceAgainstPostgreSql()
    {
        await using var setupContext = CatalogPersistenceFixture.CreateContext();
        var movieA = await SeedMovieAsync(setupContext, 660001);
        var movieB = await SeedMovieAsync(setupContext, 660002);
        var canonical = await SeedCanonicalKeywordAsync(setupContext, 660010, "mdb-race-tag");
        var providerKeyword = new MdbListKeywordTransportItem(KeywordProvider.MdbList, 991122, "mdb-race-tag");
        var syncedAtUtc = DateTime.UtcNow;
        using var barrier = new Barrier(2);

        await Task.WhenAll(
            RunConcurrentMdbListMovieIngestAsync(movieA.Id, providerKeyword, syncedAtUtc, barrier),
            RunConcurrentMdbListMovieIngestAsync(movieB.Id, providerKeyword, syncedAtUtc, barrier));

        await using var verifyContext = CatalogPersistenceFixture.CreateContext();
        var references = await verifyContext.KeywordExternalReferences
            .Where(reference =>
                reference.Provider == KeywordProvider.MdbList &&
                reference.ExternalId == "991122")
            .ToListAsync();

        Assert.Single(references);
        Assert.Equal(canonical.Id, references[0].KeywordId);
        Assert.NotNull((await verifyContext.Movies.FindAsync(movieA.Id))!.MdbListKeywordsSyncedAtUtc);
        Assert.NotNull((await verifyContext.Movies.FindAsync(movieB.Id))!.MdbListKeywordsSyncedAtUtc);
    }

    [Fact]
    public async Task ConcurrentMdbListIngestionPreservesExistingExternalReferenceOwnershipAgainstPostgreSql()
    {
        await using var setupContext = CatalogPersistenceFixture.CreateContext();
        var movie = await SeedMovieAsync(setupContext, 660003);
        var owner = await SeedCanonicalKeywordAsync(setupContext, 660011, "owner-tag");
        var challenger = await SeedCanonicalKeywordAsync(setupContext, 660012, "challenger-tag");
        setupContext.KeywordExternalReferences.Add(new KeywordExternalReference
        {
            KeywordId = owner.Id,
            Provider = KeywordProvider.MdbList,
            ExternalId = "445566",
            ExternalName = "owner-tag",
            CreatedAt = DateTime.UtcNow,
        });
        await setupContext.SaveChangesAsync();

        var providerKeyword = new MdbListKeywordTransportItem(
            KeywordProvider.MdbList,
            445566,
            challenger.CanonicalName!);
        var syncedAtUtc = DateTime.UtcNow;
        using var barrier = new Barrier(2);

        await Task.WhenAll(
            RunConcurrentMdbListMovieIngestAsync(movie.Id, providerKeyword, syncedAtUtc, barrier),
            RunConcurrentMdbListMovieIngestAsync(movie.Id, providerKeyword, syncedAtUtc, barrier));

        await using var verifyContext = CatalogPersistenceFixture.CreateContext();
        var reference = await verifyContext.KeywordExternalReferences.SingleAsync(
            existing => existing.Provider == KeywordProvider.MdbList && existing.ExternalId == "445566");
        Assert.Equal(owner.Id, reference.KeywordId);
    }

    [Fact]
    public async Task ConcurrentMovieAndTvKeywordSyncConvergesOnSharedTmdbKeywordIdAgainstPostgreSql()
    {
        await using var setupContext = CatalogPersistenceFixture.CreateContext();
        var movie = await SeedMovieAsync(setupContext, 550001);
        var tvShow = await SeedTvShowAsync(setupContext, 550002);
        var sharedKeyword = new ProviderKeywordSummary(180547, "sequel");
        var syncedAtUtc = DateTime.UtcNow;
        using var barrier = new Barrier(2);

        await Task.WhenAll(
            RunConcurrentMovieSyncAsync(movie.Id, sharedKeyword, syncedAtUtc, barrier),
            RunConcurrentTvShowSyncAsync(tvShow.Id, sharedKeyword, syncedAtUtc, barrier));

        await using var verifyContext = CatalogPersistenceFixture.CreateContext();
        var canonicalKeywords = await verifyContext.Keywords
            .Where(keyword => keyword.TmdbKeywordId == sharedKeyword.TmdbKeywordId)
            .ToListAsync();

        Assert.Single(canonicalKeywords);
        var canonicalKeywordId = canonicalKeywords[0].Id;

        Assert.True(await verifyContext.MovieKeywords.AnyAsync(
            movieKeyword => movieKeyword.MovieId == movie.Id &&
                            movieKeyword.KeywordId == canonicalKeywordId));
        Assert.True(await verifyContext.TvShowKeywords.AnyAsync(
            tvShowKeyword => tvShowKeyword.TvShowId == tvShow.Id &&
                             tvShowKeyword.KeywordId == canonicalKeywordId));
        Assert.NotNull((await verifyContext.Movies.FindAsync(movie.Id))!.KeywordsSyncedAtUtc);
        Assert.NotNull((await verifyContext.TvShows.FindAsync(tvShow.Id))!.KeywordsSyncedAtUtc);
    }

    private static Task RunConcurrentMdbListMovieIngestAsync(
        Guid movieId,
        MdbListKeywordTransportItem keyword,
        DateTime syncedAtUtc,
        Barrier barrier) =>
        Task.Run(async () =>
        {
            await using var context = CatalogPersistenceFixture.CreateContext();
            var repository = new KeywordCatalogRepository(context, Options.Create(new KeywordGraphOptions()));
            barrier.SignalAndWait();
            await repository.ApplyMovieMdbListKeywordIngestionAsync(movieId, [keyword], syncedAtUtc, CancellationToken.None);
        });

    private static Task RunConcurrentMovieSyncAsync(
        Guid movieId,
        ProviderKeywordSummary keyword,
        DateTime syncedAtUtc,
        Barrier barrier) =>
        Task.Run(async () =>
        {
            await using var context = CatalogPersistenceFixture.CreateContext();
            var repository = new KeywordCatalogRepository(context, Options.Create(new KeywordGraphOptions()));
            barrier.SignalAndWait();
            await repository.SyncMovieKeywordsAsync(movieId, [keyword], syncedAtUtc);
        });

    private static Task RunConcurrentTvShowSyncAsync(
        Guid tvShowId,
        ProviderKeywordSummary keyword,
        DateTime syncedAtUtc,
        Barrier barrier) =>
        Task.Run(async () =>
        {
            await using var context = CatalogPersistenceFixture.CreateContext();
            var repository = new KeywordCatalogRepository(context, Options.Create(new KeywordGraphOptions()));
            barrier.SignalAndWait();
            await repository.SyncTvShowKeywordsAsync(tvShowId, [keyword], syncedAtUtc);
        });

    private static async Task<Keyword> SeedCanonicalKeywordAsync(ApplicationDbContext context, int tmdbKeywordId, string name)
    {
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = tmdbKeywordId,
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

    private static async Task<Movie> SeedMovieAsync(ApplicationDbContext context, int tmdbId)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = $"Keyword Concurrency Movie {tmdbId}",
            VoteAverage = 7,
            VoteCount = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Movies.Add(movie);
        await context.SaveChangesAsync();
        return movie;
    }

    private static async Task<TvShow> SeedTvShowAsync(ApplicationDbContext context, int tmdbId)
    {
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = $"Keyword Concurrency Show {tmdbId}",
            Status = TvShowStatus.Ended,
            VoteAverage = 7,
            VoteCount = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.TvShows.Add(tvShow);
        await context.SaveChangesAsync();
        return tvShow;
    }
}

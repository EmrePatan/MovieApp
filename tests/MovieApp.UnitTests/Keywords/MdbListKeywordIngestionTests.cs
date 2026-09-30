using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Keywords;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Keywords;

public sealed class MdbListKeywordIngestionTests
{
    [Fact]
    public async Task ExistingMdbListExternalIdResolvesToOwnerEvenWhenIncomingNameDiffers()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = await SeedKeywordAsync(context, 42, "time travel");
        context.KeywordExternalReferences.Add(new KeywordExternalReference
        {
            KeywordId = keyword.Id,
            Provider = KeywordProvider.MdbList,
            ExternalId = "404",
            ExternalName = "time-travel",
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context);
        var result = await repository.ApplyMovieMdbListKeywordIngestionAsync(
            movie.Id,
            [new MdbListKeywordTransportItem(KeywordProvider.MdbList, 404, "renamed-label")],
            DateTime.UtcNow,
            CancellationToken.None);

        Assert.Equal(MdbListKeywordIngestionStatus.Succeeded, result.Status);
        Assert.Equal(1, result.Stats!.PromotedCanonicalCount);
        Assert.Equal(1, result.Stats.ExternalReferencesReused);
        Assert.Equal(0, result.Stats.ExternalReferencesCreated);
        Assert.Single(await context.MovieKeywordSources.Where(source => source.Provider == KeywordProvider.MdbList).ToListAsync());
    }

    [Fact]
    public async Task NewExternalIdWithSingleNormalizedMatchCreatesReferenceAndSource()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = await SeedKeywordAsync(context, 99, "time travel");

        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context);
        var result = await repository.ApplyMovieMdbListKeywordIngestionAsync(
            movie.Id,
            [new MdbListKeywordTransportItem(KeywordProvider.MdbList, 297503, "time-travel")],
            DateTime.UtcNow,
            CancellationToken.None);

        Assert.Equal(1, result.Stats!.PromotedCanonicalCount);
        Assert.Equal(1, result.Stats.ExternalReferencesCreated);
        var reference = await context.KeywordExternalReferences.SingleAsync();
        Assert.Equal(keyword.Id, reference.KeywordId);
        Assert.Equal("297503", reference.ExternalId);
        Assert.Single(await context.MovieKeywords.ToListAsync());
    }

    [Fact]
    public async Task TwoMdbListIdsForSameConceptShareCanonicalKeywordAndOneSource()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        _ = await SeedKeywordAsync(context, 99, "time travel");

        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context);
        await repository.ApplyMovieMdbListKeywordIngestionAsync(
            movie.Id,
            [
                new MdbListKeywordTransportItem(KeywordProvider.MdbList, 404, "time-travel"),
                new MdbListKeywordTransportItem(KeywordProvider.MdbList, 297503, "time-travel"),
            ],
            DateTime.UtcNow,
            CancellationToken.None);

        Assert.Equal(2, await context.KeywordExternalReferences.CountAsync());
        Assert.Single(await context.MovieKeywordSources.Where(source => source.Provider == KeywordProvider.MdbList).ToListAsync());
        Assert.Single(await context.MovieKeywords.ToListAsync());
    }

    [Fact]
    public async Task ZeroCanonicalMatchesAreSkippedWithoutCreatingGraphRows()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context);

        var result = await repository.ApplyMovieMdbListKeywordIngestionAsync(
            movie.Id,
            [new MdbListKeywordTransportItem(KeywordProvider.MdbList, 43291, "has-trailer")],
            DateTime.UtcNow,
            CancellationToken.None);

        Assert.Equal(1, result.Stats!.SkippedNoMatchCount);
        Assert.Empty(await context.Keywords.Where(keyword => keyword.TmdbKeywordId == null).ToListAsync());
        Assert.Empty(await context.KeywordExternalReferences.ToListAsync());
        Assert.Empty(await context.MovieKeywordSources.ToListAsync());
    }

    [Fact]
    public async Task AmbiguousNormalizedMatchesAreSkipped()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var normalized = KeywordCanonicalNormalization.NormalizeKeywordName("welfare");
        context.Keywords.AddRange(
            new Keyword
            {
                Id = Guid.NewGuid(),
                TmdbKeywordId = 1,
                Name = "welfare",
                CanonicalName = "welfare",
                NormalizedName = normalized,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
            new Keyword
            {
                Id = Guid.NewGuid(),
                TmdbKeywordId = 2,
                Name = "welfare duplicate",
                CanonicalName = "welfare duplicate",
                NormalizedName = normalized,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        await context.SaveChangesAsync();

        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context);
        var result = await repository.ApplyMovieMdbListKeywordIngestionAsync(
            movie.Id,
            [new MdbListKeywordTransportItem(KeywordProvider.MdbList, 304495, "welfare")],
            DateTime.UtcNow,
            CancellationToken.None);

        Assert.Equal(1, result.Stats!.SkippedAmbiguousCount);
        Assert.Empty(await context.KeywordExternalReferences.ToListAsync());
    }

    [Fact]
    public async Task MdbListRefreshRemovesStaleMdbListSourcesAndPreservesTmdbSources()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keywordA = await SeedKeywordAsync(context, 1, "alpha");
        var keywordB = await SeedKeywordAsync(context, 2, "beta");
        context.MovieKeywordSources.AddRange(
            new MovieKeywordSource
            {
                MovieId = movie.Id,
                KeywordId = keywordA.Id,
                Provider = KeywordProvider.Tmdb,
                FirstSeenAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            },
            new MovieKeywordSource
            {
                MovieId = movie.Id,
                KeywordId = keywordA.Id,
                Provider = KeywordProvider.MdbList,
                FirstSeenAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            },
            new MovieKeywordSource
            {
                MovieId = movie.Id,
                KeywordId = keywordB.Id,
                Provider = KeywordProvider.MdbList,
                FirstSeenAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            });
        context.KeywordExternalReferences.Add(new KeywordExternalReference
        {
            KeywordId = keywordA.Id,
            Provider = KeywordProvider.MdbList,
            ExternalId = "100",
            ExternalName = "alpha",
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context);
        await repository.ApplyMovieMdbListKeywordIngestionAsync(
            movie.Id,
            [new MdbListKeywordTransportItem(KeywordProvider.MdbList, 100, "alpha")],
            DateTime.UtcNow,
            CancellationToken.None);

        Assert.Single(await context.MovieKeywordSources.Where(source => source.Provider == KeywordProvider.Tmdb).ToListAsync());
        Assert.Single(await context.MovieKeywordSources.Where(source => source.Provider == KeywordProvider.MdbList).ToListAsync());
        Assert.Single(await context.MovieKeywords.ToListAsync());
    }

    [Fact]
    public async Task TvShowMdbListRefreshPreservesTmdbSources()
    {
        await using var context = CreateContext();
        var tvShow = await SeedTvShowAsync(context);
        var keyword = await SeedKeywordAsync(context, 5, "drama");
        context.TvShowKeywordSources.Add(new TvShowKeywordSource
        {
            TvShowId = tvShow.Id,
            KeywordId = keyword.Id,
            Provider = KeywordProvider.Tmdb,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        context.KeywordExternalReferences.Add(new KeywordExternalReference
        {
            KeywordId = keyword.Id,
            Provider = KeywordProvider.MdbList,
            ExternalId = "55",
            ExternalName = "drama",
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context);
        await repository.ApplyTvShowMdbListKeywordIngestionAsync(
            tvShow.Id,
            [new MdbListKeywordTransportItem(KeywordProvider.MdbList, 55, "drama")],
            DateTime.UtcNow,
            CancellationToken.None);

        Assert.Single(await context.TvShowKeywordSources.Where(source => source.Provider == KeywordProvider.Tmdb).ToListAsync());
        Assert.Single(await context.TvShowKeywordSources.Where(source => source.Provider == KeywordProvider.MdbList).ToListAsync());
    }

    [Fact]
    public async Task SuccessfulEmptyMdbListResultClearsMdbListSourcesPreservesTmdbAndAdvancesMarker()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = await SeedKeywordAsync(context, 3, "solo");
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

        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context);
        var syncedAt = DateTime.UtcNow;
        var result = await repository.ApplyMovieMdbListKeywordIngestionAsync(movie.Id, [], syncedAt, CancellationToken.None);

        Assert.Equal(MdbListKeywordIngestionStatus.Succeeded, result.Status);
        Assert.NotNull(result.MdbListKeywordsSyncedAtUtc);
        Assert.Empty(await context.MovieKeywordSources.Where(source => source.Provider == KeywordProvider.MdbList).ToListAsync());
        Assert.Single(await context.MovieKeywordSources.Where(source => source.Provider == KeywordProvider.Tmdb).ToListAsync());
        Assert.Single(await context.MovieKeywords.ToListAsync());
    }

    [Fact]
    public async Task TransportFailureDoesNotAdvanceMarkerOrMutateMdbListSources()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = await SeedKeywordAsync(context, 8, "kept");
        context.MovieKeywordSources.Add(new MovieKeywordSource
        {
            MovieId = movie.Id,
            KeywordId = keyword.Id,
            Provider = KeywordProvider.MdbList,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var ingestion = new MdbListKeywordIngestionService(
            new NullMdbListKeywordTransportProvider(),
            KeywordCatalogRepositoryTestHelper.CreateRepository(context),
            NullLogger<MdbListKeywordIngestionService>.Instance);

        var result = await ingestion.IngestMovieAsync(movie.Id);
        var stored = await context.Movies.SingleAsync();

        Assert.Equal(MdbListKeywordIngestionStatus.TransportUnavailable, result.Status);
        Assert.Null(stored.MdbListKeywordsSyncedAtUtc);
        Assert.Single(await context.MovieKeywordSources.Where(source => source.Provider == KeywordProvider.MdbList).ToListAsync());
    }

    [Fact]
    public async Task ExternalReferenceOwnershipIsNotReassignedByName()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var owner = await SeedKeywordAsync(context, 10, "owner");
        var other = await SeedKeywordAsync(context, 11, "other");
        context.KeywordExternalReferences.Add(new KeywordExternalReference
        {
            KeywordId = owner.Id,
            Provider = KeywordProvider.MdbList,
            ExternalId = "777",
            ExternalName = "owner-tag",
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context);
        await repository.ApplyMovieMdbListKeywordIngestionAsync(
            movie.Id,
            [new MdbListKeywordTransportItem(KeywordProvider.MdbList, 777, other.NormalizedName!)],
            DateTime.UtcNow,
            CancellationToken.None);

        var reference = await context.KeywordExternalReferences.SingleAsync();
        Assert.Equal(owner.Id, reference.KeywordId);
    }

    [Fact]
    public async Task BackfillSelectsOnlyPendingEligibleMoviesAndTvShows()
    {
        await using var context = CreateContext();
        var pendingMovie = await SeedMovieAsync(context);
        var syncedMovie = await SeedMovieAsync(context);
        syncedMovie.MdbListKeywordsSyncedAtUtc = DateTime.UtcNow;
        var pendingTv = await SeedTvShowAsync(context);
        var noTmdbMovie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "No tmdb",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Movies.Add(noTmdbMovie);
        await context.SaveChangesAsync();

        var repository = new MdbListKeywordBackfillRepository(context);
        var movies = await repository.SelectMovieCandidatesAsync(10, []);
        var tvShows = await repository.SelectTvShowCandidatesAsync(10, []);

        Assert.Contains(movies, candidate => candidate.CatalogId == pendingMovie.Id);
        Assert.DoesNotContain(movies, candidate => candidate.CatalogId == syncedMovie.Id);
        Assert.DoesNotContain(movies, candidate => candidate.CatalogId == noTmdbMovie.Id);
        Assert.Contains(tvShows, candidate => candidate.CatalogId == pendingTv.Id);
    }

    [Fact]
    public void MdbListKeywordBackfillOptionsValidatorRejectsInvalidBounds()
    {
        var validator = new MdbListKeywordBackfillOptionsValidator();
        var result = validator.Validate(
            null,
            new MdbListKeywordBackfillOptions
            {
                BatchSize = 0,
                MaxConcurrency = 0,
                DelayBetweenItemsMs = -1,
                RecurringCron = "",
            });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task MdbListKeywordBackfillServiceReturnsEmptyBatchWhenNoCandidates()
    {
        var service = new MdbListKeywordBackfillService(
            new MdbListKeywordBackfillRepository(CreateContext()),
            new TestScopeFactory(),
            Options.Create(new MdbListKeywordBackfillOptions { Enabled = false, BatchSize = 100 }));

        var candidates = await service.SelectCandidatesAsync(10, []);
        var result = await service.ProcessBatchAsync(candidates);
        Assert.Equal(0, result.Selected);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"mdblist-keyword-ingest-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Movie> SeedMovieAsync(ApplicationDbContext context)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = Random.Shared.Next(1_000_000, 9_999_999),
            Title = "Test Movie",
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
            Title = "Test Show",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.TvShows.Add(tvShow);
        await context.SaveChangesAsync();
        return tvShow;
    }

    private static async Task<Keyword> SeedKeywordAsync(ApplicationDbContext context, int tmdbId, string name)
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

    private sealed class NullMdbListKeywordTransportProvider : IMdbListKeywordTransportProvider
    {
        public Task<MdbListKeywordsTransportResult?> FetchKeywordsAsync(
            CatalogContentType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MdbListKeywordsTransportResult?>(null);
    }

    private sealed class TestScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new TestScope();

        private sealed class TestScope : IServiceScope
        {
            public IServiceProvider ServiceProvider => new TestServiceProvider();

            public void Dispose()
            {
            }
        }

        private sealed class TestServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) =>
                serviceType == typeof(IMdbListKeywordBackfillItemProcessor)
                    ? new MdbListKeywordBackfillItemProcessor(new NullMdbListKeywordIngestionService())
                    : null;
        }

        private sealed class NullMdbListKeywordIngestionService : IMdbListKeywordIngestionService
        {
            public Task<MdbListKeywordIngestionResult> IngestMovieAsync(Guid movieId, CancellationToken cancellationToken = default) =>
                Task.FromResult(new MdbListKeywordIngestionResult(MdbListKeywordIngestionStatus.Succeeded));

            public Task<MdbListKeywordIngestionResult> IngestTvShowAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
                Task.FromResult(new MdbListKeywordIngestionResult(MdbListKeywordIngestionStatus.Succeeded));

            public Task<MdbListKeywordIngestionResult> IngestMovieByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
                Task.FromResult(new MdbListKeywordIngestionResult(MdbListKeywordIngestionStatus.Succeeded));

            public Task<MdbListKeywordIngestionResult> IngestTvShowByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
                Task.FromResult(new MdbListKeywordIngestionResult(MdbListKeywordIngestionStatus.Succeeded));
        }
    }
}

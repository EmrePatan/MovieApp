using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Search;

public sealed class ContentSearchTitleProviderEnrichmentRepositoryTests
{
    [Fact]
    public async Task SelectMovieCandidatesAsync_WithRepairFilter_SkipsFullyRepairedTitles()
    {
        await using var context = CreateContext();
        var repairedMovieId = await SeedMovieAsync(context, 100, needsRepair: false);
        var repairMovieId = await SeedMovieAsync(context, 200, needsRepair: true);
        await SeedMovieAsync(context, 300, needsRepair: false, tmdbIdOverride: null);
        var repository = new ContentSearchTitleProviderEnrichmentRepository(context);

        var candidates = await repository.SelectMovieCandidatesAsync(
            null,
            null,
            10,
            needsSearchTitleMetadataRepairOnly: true);

        Assert.Single(candidates);
        Assert.Equal(repairMovieId, candidates[0].ContentId);
        Assert.Equal(200, candidates[0].TmdbId);
    }

    [Fact]
    public async Task SelectTvShowCandidatesAsync_WithRepairFilter_SkipsFullyRepairedTitles()
    {
        await using var context = CreateContext();
        await SeedTvShowAsync(context, 100, needsRepair: false);
        var repairTvId = await SeedTvShowAsync(context, 200, needsRepair: true);
        var repository = new ContentSearchTitleProviderEnrichmentRepository(context);

        var candidates = await repository.SelectTvShowCandidatesAsync(
            null,
            null,
            10,
            needsSearchTitleMetadataRepairOnly: true);

        Assert.Single(candidates);
        Assert.Equal(repairTvId, candidates[0].ContentId);
    }

    [Fact]
    public async Task CountNeedingSearchTitleMetadataRepair_CountsDistinctCatalogItems()
    {
        await using var context = CreateContext();
        await SeedMovieAsync(context, 101, needsRepair: true);
        await SeedMovieAsync(context, 102, needsRepair: true);
        await SeedMovieAsync(context, 103, needsRepair: false);
        await SeedTvShowAsync(context, 201, needsRepair: true);
        var repository = new ContentSearchTitleProviderEnrichmentRepository(context);

        Assert.Equal(2, await repository.CountMoviesNeedingSearchTitleMetadataRepairAsync());
        Assert.Equal(1, await repository.CountTvShowsNeedingSearchTitleMetadataRepairAsync());
    }

    [Fact]
    public async Task SelectMovieCandidatesAsync_WithRepairFilter_SkipsAlternativeNullCountryAfterProviderSync()
    {
        await using var context = CreateContext();
        await SeedMovieAsync(context, 635396, needsRepair: false, alternativeCountryCode: null, providerSynced: true);
        var staleMovieId = await SeedMovieAsync(context, 100, needsRepair: true);
        var repository = new ContentSearchTitleProviderEnrichmentRepository(context);

        var candidates = await repository.SelectMovieCandidatesAsync(
            null,
            null,
            10,
            needsSearchTitleMetadataRepairOnly: true);

        Assert.Single(candidates);
        Assert.Equal(staleMovieId, candidates[0].ContentId);
    }

    [Fact]
    public async Task SelectMovieCandidatesAsync_WithoutRepairFilter_ReturnsAllEligibleMovies()
    {
        await using var context = CreateContext();
        await SeedMovieAsync(context, 101, needsRepair: false);
        await SeedMovieAsync(context, 102, needsRepair: true);
        var repository = new ContentSearchTitleProviderEnrichmentRepository(context);

        var candidates = await repository.SelectMovieCandidatesAsync(null, null, 10);

        Assert.Equal(2, candidates.Count);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"cst-enrichment-repair-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Guid> SeedMovieAsync(
        ApplicationDbContext context,
        int tmdbId,
        bool needsRepair,
        int? tmdbIdOverride = null,
        string? alternativeCountryCode = "US",
        bool providerSynced = false)
    {
        var movieId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = $"Movie {tmdbId}",
            OriginalTitle = "Original",
            TmdbId = tmdbIdOverride ?? tmdbId,
            ReleaseDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 1,
            VoteCount = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        var translationSynced = providerSynced || !needsRepair;
        context.ContentSearchTitles.Add(new ContentSearchTitle
        {
            Id = Guid.NewGuid(),
            ContentType = CatalogContentType.Movie,
            ContentId = movieId,
            Title = "Localized",
            NormalizedTitle = "localized",
            TitleKind = ContentSearchTitleKind.Translation,
            Source = ContentSearchTitleSource.TmdbTranslation,
            LanguageCode = needsRepair ? null : "en",
            CountryCode = "US",
            ProviderUpdatedAtUtc = translationSynced ? utcNow : null,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        });

        if (alternativeCountryCode is not null || providerSynced)
        {
            context.ContentSearchTitles.Add(new ContentSearchTitle
            {
                Id = Guid.NewGuid(),
                ContentType = CatalogContentType.Movie,
                ContentId = movieId,
                Title = "Alternative",
                NormalizedTitle = "alternative",
                TitleKind = ContentSearchTitleKind.Alternative,
                Source = ContentSearchTitleSource.TmdbAlternative,
                LanguageCode = null,
                CountryCode = alternativeCountryCode,
                ProviderUpdatedAtUtc = providerSynced ? utcNow : null,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow,
            });
        }

        await context.SaveChangesAsync();
        return movieId;
    }

    private static async Task<Guid> SeedTvShowAsync(
        ApplicationDbContext context,
        int tmdbId,
        bool needsRepair)
    {
        var tvShowId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            Title = $"Show {tmdbId}",
            TmdbId = tmdbId,
            FirstAirDate = new DateOnly(2020, 1, 1),
            VoteAverage = 1,
            VoteCount = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        context.ContentSearchTitles.Add(new ContentSearchTitle
        {
            Id = Guid.NewGuid(),
            ContentType = CatalogContentType.Tv,
            ContentId = tvShowId,
            Title = "Alias",
            NormalizedTitle = "alias",
            TitleKind = ContentSearchTitleKind.Alternative,
            Source = ContentSearchTitleSource.TmdbAlternative,
            LanguageCode = null,
            CountryCode = needsRepair ? null : "DE",
            ProviderUpdatedAtUtc = needsRepair ? null : utcNow,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        });

        await context.SaveChangesAsync();
        return tvShowId;
    }
}

using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class ContentSearchTitleProviderEnrichmentIntegrationTests
{
    [Fact]
    public async Task CanonicalBackfillIsIdempotent()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var movieId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Idempotent Title",
            OriginalTitle = "Original",
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
        var countAfterFirst = await context.ContentSearchTitles.CountAsync(row => row.ContentId == movieId);

        await backfill.BackfillCanonicalAndOriginalAsync();
        var countAfterSecond = await context.ContentSearchTitles.CountAsync(row => row.ContentId == movieId);

        Assert.Equal(2, countAfterFirst);
        Assert.Equal(2, countAfterSecond);
    }

    [Fact]
    public async Task ProviderEnrichmentPersistsAliasesWithoutDuplicates()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var movieId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Display Title",
            OriginalTitle = "Original",
            TmdbId = 4242,
            ReleaseDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 1,
            VoteCount = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        var synchronizer = new ContentSearchTitleSynchronizer(context);
        var upsert = new CatalogProviderUpsertService(
            new MovieRepository(context, synchronizer),
            new TvShowRepository(context, synchronizer),
            new NoOpKeywordIngestion(),
            new NoOpMovieCacheInvalidator(),
            synchronizer);

        var enrichment = new ContentSearchTitleProviderEnrichmentService(
            new ContentSearchTitleProviderEnrichmentRepository(context),
            new StubMovieDataProvider(
                new MovieProviderDetails(
                    "tmdb-4242",
                    4242,
                    null,
                    null,
                    "Display Title",
                    "Original",
                    "Overview",
                    new DateOnly(2020, 1, 1),
                    100,
                    null,
                    null,
                    "en",
                    7,
                    10,
                    ["Drama"],
                    ProviderSearchTitles:
                    [
                        new ProviderSearchTitleEntry(
                            "Alias One",
                            ContentSearchTitleKind.Alternative,
                            ContentSearchTitleSource.TmdbAlternative,
                            null,
                            "TR",
                            null),
                    ])),
            new NoOpTvShowDataProvider(),
            new TvShowRepository(context, synchronizer),
            new MovieApp.Infrastructure.Providers.FakeTvExternalIdResolver(),
            upsert,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ContentSearchTitleProviderEnrichmentService>.Instance);

        var result = await enrichment.EnrichFromProviderAsync(new ContentSearchTitleProviderEnrichmentRequest
        {
            OnlyMovieId = movieId,
            MaxItems = 1,
            DelayBetweenRequestsMs = 0,
        });

        Assert.Equal(1, result.Succeeded);
        var rows = await context.ContentSearchTitles.Where(row => row.ContentId == movieId).ToListAsync();
        Assert.Contains(rows, row => row.Source == ContentSearchTitleSource.TmdbAlternative);
        Assert.Equal(rows.Count, rows.Select(row => row.NormalizedTitle).Distinct().Count());

        await enrichment.EnrichFromProviderAsync(new ContentSearchTitleProviderEnrichmentRequest
        {
            OnlyMovieId = movieId,
            MaxItems = 1,
            DelayBetweenRequestsMs = 0,
        });

        Assert.Equal(rows.Count, await context.ContentSearchTitles.CountAsync(row => row.ContentId == movieId));
    }

    private sealed class StubMovieDataProvider(MovieProviderDetails details) : IMovieDataProvider
    {
        public Task<MovieProviderDetails?> GetMovieAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MovieProviderDetails?>(details);

        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpTvShowDataProvider : ITvShowDataProvider
    {
        public Task<TvShowProviderDetails?> GetTvShowAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShowProviderDetails?>(null);

        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> AdvancedDiscoverTvShowsAsync(
            AdvancedDiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpKeywordIngestion : ICatalogKeywordIngestionService
    {
        public Task TryEnrichMovieKeywordsAsync(
            Guid movieId,
            bool refreshKeywords,
            IReadOnlyList<ProviderKeywordSummary>? prefetchedKeywords = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task TryEnrichTvShowKeywordsAsync(
            Guid tvShowId,
            bool refreshKeywords,
            IReadOnlyList<ProviderKeywordSummary>? prefetchedKeywords = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpMovieCacheInvalidator : IMovieCatalogDetailsCacheInvalidator
    {
        public Task InvalidateAsync(Guid movieId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Catalog;
using MovieApp.Infrastructure.Persistence.Catalog;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class CatalogMetadataFreshnessRepository(
    ApplicationDbContext dbContext,
    IOptions<NewReleasesOptions> newReleasesOptions,
    IOptions<RecommendationOptions> recommendationOptions,
    IOptions<TopRatedOptions> topRatedOptions,
    IOptions<CatalogMetadataFreshnessOptions> freshnessOptions) : ICatalogMetadataFreshnessRepository
{
    public async Task MarkProviderDiscoverySeenForMoviesAsync(
        IReadOnlyList<Guid> movieIds,
        DateTime seenAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (movieIds.Count == 0)
        {
            return;
        }

        await dbContext.Movies
            .Where(movie => movieIds.Contains(movie.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(movie => movie.TmdbProviderDiscoverySeenAtUtc, seenAtUtc),
                cancellationToken);
    }

    public async Task MarkProviderDiscoverySeenForTvShowsAsync(
        IReadOnlyList<Guid> tvShowIds,
        DateTime seenAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (tvShowIds.Count == 0)
        {
            return;
        }

        await dbContext.TvShows
            .Where(tvShow => tvShowIds.Contains(tvShow.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(tvShow => tvShow.TmdbProviderDiscoverySeenAtUtc, seenAtUtc),
                cancellationToken);
    }

    public Task<int> CountStaleDiscoveryRelevantMoviesAsync(
        DateTime staleBeforeUtc,
        CancellationToken cancellationToken = default) =>
        BuildStaleMovieQuery(staleBeforeUtc).CountAsync(cancellationToken);

    public Task<int> CountStaleDiscoveryRelevantTvShowsAsync(
        DateTime staleBeforeUtc,
        CancellationToken cancellationToken = default) =>
        BuildStaleTvQuery(staleBeforeUtc).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> SelectStaleDiscoveryRelevantMovieIdsAsync(
        DateTime staleBeforeUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        return await BuildStaleMovieQuery(staleBeforeUtc)
            .OrderBy(movie => movie.TmdbMetadataUpdatedAtUtc ?? DateTime.MinValue)
            .ThenBy(movie => movie.Id)
            .Select(movie => movie.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> SelectStaleDiscoveryRelevantTvShowIdsAsync(
        DateTime staleBeforeUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        return await BuildStaleTvQuery(staleBeforeUtc)
            .OrderBy(tvShow => tvShow.TmdbMetadataUpdatedAtUtc ?? DateTime.MinValue)
            .ThenBy(tvShow => tvShow.Id)
            .Select(tvShow => tvShow.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<CatalogMetadataFreshnessDistribution> GetDiscoveryFreshnessDistributionAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var thresholds = BuildThresholds(utcNow);
        var today = DateOnly.FromDateTime(utcNow);
        var fresh24Cutoff = utcNow.AddHours(-24);
        var fresh72Cutoff = utcNow.AddHours(-72);
        var stale7dCutoff = utcNow.AddDays(-7);

        var movieQuery = DiscoveryCatalogRelevanceQuery.WhereDiscoveryRelevant(
            dbContext.Movies.AsNoTracking(),
            thresholds,
            today);
        var tvQuery = DiscoveryCatalogRelevanceQuery.WhereDiscoveryRelevant(
            dbContext.TvShows.AsNoTracking(),
            thresholds,
            today);

        var movieBuckets = await CountMovieFreshnessBucketsAsync(
            movieQuery,
            fresh24Cutoff,
            fresh72Cutoff,
            stale7dCutoff,
            cancellationToken);
        var tvBuckets = await CountTvFreshnessBucketsAsync(
            tvQuery,
            fresh24Cutoff,
            fresh72Cutoff,
            stale7dCutoff,
            cancellationToken);

        return new CatalogMetadataFreshnessDistribution(
            movieBuckets.Total + tvBuckets.Total,
            movieBuckets.FreshWithin24Hours + tvBuckets.FreshWithin24Hours,
            movieBuckets.Fresh24To72Hours + tvBuckets.Fresh24To72Hours,
            movieBuckets.StaleOver72Hours + tvBuckets.StaleOver72Hours,
            movieBuckets.StaleOver7Days + tvBuckets.StaleOver7Days,
            movieBuckets.NeverRefreshed + tvBuckets.NeverRefreshed);
    }

    private static async Task<FreshnessBucketCounts> CountMovieFreshnessBucketsAsync(
        IQueryable<Domain.Entities.Movie> query,
        DateTime fresh24Cutoff,
        DateTime fresh72Cutoff,
        DateTime stale7dCutoff,
        CancellationToken cancellationToken)
    {
        if (!await query.AnyAsync(cancellationToken))
        {
            return new FreshnessBucketCounts(0, 0, 0, 0, 0, 0);
        }

        return await query
            .GroupBy(_ => 1)
            .Select(group => new FreshnessBucketCounts(
                group.Count(),
                group.Count(movie =>
                    movie.TmdbMetadataUpdatedAtUtc != null && movie.TmdbMetadataUpdatedAtUtc >= fresh24Cutoff),
                group.Count(movie =>
                    movie.TmdbMetadataUpdatedAtUtc != null
                    && movie.TmdbMetadataUpdatedAtUtc < fresh24Cutoff
                    && movie.TmdbMetadataUpdatedAtUtc >= fresh72Cutoff),
                group.Count(movie =>
                    movie.TmdbMetadataUpdatedAtUtc == null || movie.TmdbMetadataUpdatedAtUtc < fresh72Cutoff),
                group.Count(movie =>
                    movie.TmdbMetadataUpdatedAtUtc == null || movie.TmdbMetadataUpdatedAtUtc < stale7dCutoff),
                group.Count(movie => movie.TmdbMetadataUpdatedAtUtc == null)))
            .SingleAsync(cancellationToken);
    }

    private static async Task<FreshnessBucketCounts> CountTvFreshnessBucketsAsync(
        IQueryable<Domain.Entities.TvShow> query,
        DateTime fresh24Cutoff,
        DateTime fresh72Cutoff,
        DateTime stale7dCutoff,
        CancellationToken cancellationToken)
    {
        if (!await query.AnyAsync(cancellationToken))
        {
            return new FreshnessBucketCounts(0, 0, 0, 0, 0, 0);
        }

        return await query
            .GroupBy(_ => 1)
            .Select(group => new FreshnessBucketCounts(
                group.Count(),
                group.Count(tvShow =>
                    tvShow.TmdbMetadataUpdatedAtUtc != null && tvShow.TmdbMetadataUpdatedAtUtc >= fresh24Cutoff),
                group.Count(tvShow =>
                    tvShow.TmdbMetadataUpdatedAtUtc != null
                    && tvShow.TmdbMetadataUpdatedAtUtc < fresh24Cutoff
                    && tvShow.TmdbMetadataUpdatedAtUtc >= fresh72Cutoff),
                group.Count(tvShow =>
                    tvShow.TmdbMetadataUpdatedAtUtc == null || tvShow.TmdbMetadataUpdatedAtUtc < fresh72Cutoff),
                group.Count(tvShow =>
                    tvShow.TmdbMetadataUpdatedAtUtc == null || tvShow.TmdbMetadataUpdatedAtUtc < stale7dCutoff),
                group.Count(tvShow => tvShow.TmdbMetadataUpdatedAtUtc == null)))
            .SingleAsync(cancellationToken);
    }

    private sealed record FreshnessBucketCounts(
        int Total,
        int FreshWithin24Hours,
        int Fresh24To72Hours,
        int StaleOver72Hours,
        int StaleOver7Days,
        int NeverRefreshed);

    private IQueryable<Domain.Entities.Movie> BuildStaleMovieQuery(DateTime staleBeforeUtc)
    {
        var thresholds = BuildThresholds(staleBeforeUtc);
        var today = DateOnly.FromDateTime(staleBeforeUtc);
        return DiscoveryCatalogRelevanceQuery
            .WhereDiscoveryRelevant(dbContext.Movies.AsNoTracking(), thresholds, today)
            .Where(movie =>
                movie.TmdbMetadataUpdatedAtUtc == null
                || movie.TmdbMetadataUpdatedAtUtc < staleBeforeUtc);
    }

    private IQueryable<Domain.Entities.TvShow> BuildStaleTvQuery(DateTime staleBeforeUtc)
    {
        var thresholds = BuildThresholds(staleBeforeUtc);
        var today = DateOnly.FromDateTime(staleBeforeUtc);
        return DiscoveryCatalogRelevanceQuery
            .WhereDiscoveryRelevant(dbContext.TvShows.AsNoTracking(), thresholds, today)
            .Where(tvShow =>
                tvShow.TmdbMetadataUpdatedAtUtc == null
                || tvShow.TmdbMetadataUpdatedAtUtc < staleBeforeUtc);
    }

    private DiscoveryCatalogRelevanceThresholds BuildThresholds(DateTime utcNow) =>
        DiscoveryCatalogRelevanceThresholdsFactory.Create(
            utcNow,
            newReleasesOptions.Value,
            recommendationOptions.Value,
            topRatedOptions.Value,
            freshnessOptions.Value);
}

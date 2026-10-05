using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Changes;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence.Catalog;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class CatalogChangesRelevanceRepository(
    ApplicationDbContext dbContext,
    IOptions<NewReleasesOptions> newReleasesOptions,
    IOptions<RecommendationOptions> recommendationOptions,
    IOptions<TopRatedOptions> topRatedOptions,
    IOptions<CatalogMetadataFreshnessOptions> freshnessOptions) : ICatalogChangesRelevanceRepository
{
    public async Task<IReadOnlyDictionary<int, Guid>> GetRelevantMovieIdsByTmdbIdAsync(
        CancellationToken cancellationToken = default)
    {
        var relevantMovieIds = BuildRelevantMovieIdsQuery();

        var rows = await dbContext.Movies
            .AsNoTracking()
            .Where(movie => movie.TmdbId.HasValue && relevantMovieIds.Contains(movie.Id))
            .Select(movie => new { movie.TmdbId, movie.Id })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.TmdbId!.Value)
            .ToDictionary(group => group.Key, group => group.First().Id);
    }

    public async Task<IReadOnlyDictionary<int, Guid>> GetRelevantTvShowIdsByTmdbIdAsync(
        CancellationToken cancellationToken = default)
    {
        var relevantTvShowIds = BuildRelevantTvShowIdsQuery();

        var rows = await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShow.TmdbId.HasValue && relevantTvShowIds.Contains(tvShow.Id))
            .Select(tvShow => new { tvShow.TmdbId, tvShow.Id })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.TmdbId!.Value)
            .ToDictionary(group => group.Key, group => group.First().Id);
    }

    public async Task<IReadOnlyDictionary<int, Guid>> GetDiscoveryRelevantMovieIdsByTmdbIdAsync(
        CancellationToken cancellationToken = default) =>
        await LoadDiscoveryMovieMapAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, Guid>> GetDiscoveryRelevantTvShowIdsByTmdbIdAsync(
        CancellationToken cancellationToken = default) =>
        await LoadDiscoveryTvMapAsync(cancellationToken);

    public async Task<CatalogChangesRefreshMaps> GetMovieChangesRefreshMapsForTmdbIdsAsync(
        IReadOnlyCollection<int> changedTmdbIds,
        CancellationToken cancellationToken = default) =>
        await BuildChangesRefreshMapsForMoviesAsync(changedTmdbIds, cancellationToken);

    public async Task<CatalogChangesRefreshMaps> GetTvShowChangesRefreshMapsForTmdbIdsAsync(
        IReadOnlyCollection<int> changedTmdbIds,
        CancellationToken cancellationToken = default) =>
        await BuildChangesRefreshMapsForTvShowsAsync(changedTmdbIds, cancellationToken);

    private async Task<CatalogChangesRefreshMaps> BuildChangesRefreshMapsForMoviesAsync(
        IReadOnlyCollection<int> changedTmdbIds,
        CancellationToken cancellationToken)
    {
        if (changedTmdbIds.Count == 0)
        {
            return CatalogChangesRefreshMaps.Create(
                new Dictionary<int, Guid>(),
                new Dictionary<int, Guid>());
        }

        var userRelevant = new Dictionary<int, Guid>();
        var discoveryRelevant = new Dictionary<int, Guid>();
        var relevantMovieIds = BuildRelevantMovieIdsQuery();
        var thresholds = BuildThresholds();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var batch in CatalogChangesRelevanceTmdbIdBatching.EnumerateBatches(changedTmdbIds))
        {
            var batchUserRows = await dbContext.Movies
                .AsNoTracking()
                .Where(movie =>
                    movie.TmdbId.HasValue
                    && batch.Contains(movie.TmdbId.Value)
                    && relevantMovieIds.Contains(movie.Id))
                .Select(movie => new { movie.TmdbId, movie.Id })
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var row in batchUserRows)
            {
                userRelevant.TryAdd(row.TmdbId!.Value, row.Id);
            }

            var batchDiscoveryRows = await DiscoveryCatalogRelevanceQuery
                .WhereDiscoveryRelevant(
                    dbContext.Movies.AsNoTracking().Where(movie =>
                        movie.TmdbId.HasValue && batch.Contains(movie.TmdbId.Value)),
                    thresholds,
                    today)
                .Select(movie => new { movie.TmdbId, movie.Id })
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var row in batchDiscoveryRows)
            {
                if (row.TmdbId.HasValue)
                {
                    discoveryRelevant.TryAdd(row.TmdbId.Value, row.Id);
                }
            }
        }

        return CatalogChangesRefreshMaps.Create(userRelevant, discoveryRelevant);
    }

    private async Task<CatalogChangesRefreshMaps> BuildChangesRefreshMapsForTvShowsAsync(
        IReadOnlyCollection<int> changedTmdbIds,
        CancellationToken cancellationToken)
    {
        if (changedTmdbIds.Count == 0)
        {
            return CatalogChangesRefreshMaps.Create(
                new Dictionary<int, Guid>(),
                new Dictionary<int, Guid>());
        }

        var userRelevant = new Dictionary<int, Guid>();
        var discoveryRelevant = new Dictionary<int, Guid>();
        var relevantTvShowIds = BuildRelevantTvShowIdsQuery();
        var thresholds = BuildThresholds();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var batch in CatalogChangesRelevanceTmdbIdBatching.EnumerateBatches(changedTmdbIds))
        {
            var batchUserRows = await dbContext.TvShows
                .AsNoTracking()
                .Where(tvShow =>
                    tvShow.TmdbId.HasValue
                    && batch.Contains(tvShow.TmdbId.Value)
                    && relevantTvShowIds.Contains(tvShow.Id))
                .Select(tvShow => new { tvShow.TmdbId, tvShow.Id })
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var row in batchUserRows)
            {
                userRelevant.TryAdd(row.TmdbId!.Value, row.Id);
            }

            var batchDiscoveryRows = await DiscoveryCatalogRelevanceQuery
                .WhereDiscoveryRelevant(
                    dbContext.TvShows.AsNoTracking().Where(tvShow =>
                        tvShow.TmdbId.HasValue && batch.Contains(tvShow.TmdbId.Value)),
                    thresholds,
                    today)
                .Select(tvShow => new { tvShow.TmdbId, tvShow.Id })
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var row in batchDiscoveryRows)
            {
                if (row.TmdbId.HasValue)
                {
                    discoveryRelevant.TryAdd(row.TmdbId.Value, row.Id);
                }
            }
        }

        return CatalogChangesRefreshMaps.Create(userRelevant, discoveryRelevant);
    }

    private async Task<IReadOnlyDictionary<int, Guid>> LoadDiscoveryMovieMapAsync(
        CancellationToken cancellationToken)
    {
        var thresholds = BuildThresholds();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await DiscoveryCatalogRelevanceQuery
            .WhereDiscoveryRelevant(dbContext.Movies.AsNoTracking(), thresholds, today)
            .Select(movie => new { movie.TmdbId, movie.Id })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows
            .Where(row => row.TmdbId.HasValue)
            .GroupBy(row => row.TmdbId!.Value)
            .ToDictionary(group => group.Key, group => group.First().Id);
    }

    private async Task<IReadOnlyDictionary<int, Guid>> LoadDiscoveryTvMapAsync(
        CancellationToken cancellationToken)
    {
        var thresholds = BuildThresholds();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await DiscoveryCatalogRelevanceQuery
            .WhereDiscoveryRelevant(dbContext.TvShows.AsNoTracking(), thresholds, today)
            .Select(tvShow => new { tvShow.TmdbId, tvShow.Id })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows
            .Where(row => row.TmdbId.HasValue)
            .GroupBy(row => row.TmdbId!.Value)
            .ToDictionary(group => group.Key, group => group.First().Id);
    }

    private DiscoveryCatalogRelevanceThresholds BuildThresholds() =>
        DiscoveryCatalogRelevanceThresholdsFactory.Create(
            DateTime.UtcNow,
            newReleasesOptions.Value,
            recommendationOptions.Value,
            topRatedOptions.Value,
            freshnessOptions.Value);

    private IQueryable<Guid> BuildRelevantMovieIdsQuery() =>
        dbContext.CatalogFollows
            .AsNoTracking()
            .Where(follow => follow.ContentType == CatalogContentType.Movie)
            .Select(follow => follow.ContentId)
            .Union(dbContext.Favorites
                .AsNoTracking()
                .Where(favorite => favorite.MovieId != null)
                .Select(favorite => favorite.MovieId!.Value))
            .Union(dbContext.WatchlistItems
                .AsNoTracking()
                .Where(item => item.MovieId != null)
                .Select(item => item.MovieId!.Value))
            .Union(dbContext.WatchedMovies
                .AsNoTracking()
                .Select(watchedMovie => watchedMovie.MovieId));

    private IQueryable<Guid> BuildRelevantTvShowIdsQuery()
    {
        var showsWithUnwatchedRegularEpisodes = dbContext.Episodes
            .AsNoTracking()
            .Where(episode => episode.Season.SeasonNumber >= 1)
            .Where(episode => !dbContext.WatchedEpisodes.Any(
                watchedEpisode => watchedEpisode.EpisodeId == episode.Id))
            .Select(episode => episode.Season.TvShowId)
            .Distinct();

        var watchingTvShowIds = dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.Episode.Season.SeasonNumber >= 1)
            .Select(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
            .Distinct()
            .Where(tvShowId => showsWithUnwatchedRegularEpisodes.Contains(tvShowId));

        return dbContext.CatalogFollows
            .AsNoTracking()
            .Where(follow => follow.ContentType == CatalogContentType.Tv)
            .Select(follow => follow.ContentId)
            .Union(dbContext.Favorites
                .AsNoTracking()
                .Where(favorite => favorite.TvShowId != null)
                .Select(favorite => favorite.TvShowId!.Value))
            .Union(dbContext.WatchlistItems
                .AsNoTracking()
                .Where(item => item.TvShowId != null)
                .Select(item => item.TvShowId!.Value))
            .Union(watchingTvShowIds);
    }
}

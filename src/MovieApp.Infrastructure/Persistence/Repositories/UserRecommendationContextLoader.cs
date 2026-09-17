using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal sealed class UserRecommendationContextLoader(
    ApplicationDbContext dbContext,
    ILogger logger)
{
    private const int MaxCastPeople = 20;
    private const int MaxSearchQueries = 10;

    public async Task<UserRecommendationContext> LoadAsync(
        Guid userId,
        int minimumInteractionsForEnrichment = 0,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        long dbTotalMs = 0;

        var ratingsStopwatch = Stopwatch.StartNew();
        var ratingRows = await dbContext.Ratings
            .AsNoTracking()
            .Where(rating => rating.UserId == userId)
            .Select(rating => new RatingRow(
                rating.MovieId,
                rating.TvShowId,
                rating.Score,
                rating.UpdatedAt,
                rating.MovieId != null ? rating.Movie!.Title : null,
                rating.TvShowId != null ? rating.TvShow!.Title : null))
            .ToListAsync(cancellationToken);
        ratingsStopwatch.Stop();
        dbTotalMs += ratingsStopwatch.ElapsedMilliseconds;

        var favoritesStopwatch = Stopwatch.StartNew();
        var favoriteRows = await dbContext.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId)
            .Select(favorite => new TimestampedContentRow(
                favorite.MovieId,
                favorite.TvShowId,
                favorite.CreatedAt,
                favorite.MovieId != null ? favorite.Movie!.Title : null,
                favorite.TvShowId != null ? favorite.TvShow!.Title : null))
            .ToListAsync(cancellationToken);
        favoritesStopwatch.Stop();
        dbTotalMs += favoritesStopwatch.ElapsedMilliseconds;

        var watchedMoviesStopwatch = Stopwatch.StartNew();
        var watchedMovieRows = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.WatchedAt)
            .Select(item => new WatchedMovieRow(item.MovieId, item.Movie!.Title, item.WatchedAt))
            .ToListAsync(cancellationToken);
        watchedMoviesStopwatch.Stop();
        dbTotalMs += watchedMoviesStopwatch.ElapsedMilliseconds;

        var watchlistStopwatch = Stopwatch.StartNew();
        var watchlistRows = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.Watchlist.UserId == userId)
            .Select(item => new TimestampedContentRow(
                item.MovieId,
                item.TvShowId,
                item.CreatedAt,
                item.MovieId != null ? item.Movie!.Title : null,
                item.TvShowId != null ? item.TvShow!.Title : null))
            .ToListAsync(cancellationToken);
        watchlistStopwatch.Stop();
        dbTotalMs += watchlistStopwatch.ElapsedMilliseconds;

        var watchedEpisodesStopwatch = Stopwatch.StartNew();
        var watchedEpisodeRows = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new WatchedEpisodeRow(
                item.Episode.Season.TvShowId,
                item.WatchedAt))
            .ToListAsync(cancellationToken);
        watchedEpisodesStopwatch.Stop();
        dbTotalMs += watchedEpisodesStopwatch.ElapsedMilliseconds;

        var catalogFollowsStopwatch = Stopwatch.StartNew();
        var catalogFollowRows = await dbContext.CatalogFollows
            .AsNoTracking()
            .Where(follow => follow.UserId == userId)
            .Select(follow => new CatalogFollowRow(
                follow.ContentType,
                follow.ContentId,
                follow.CreatedAt))
            .ToListAsync(cancellationToken);
        catalogFollowsStopwatch.Stop();
        dbTotalMs += catalogFollowsStopwatch.ElapsedMilliseconds;

        var searchHistoryStopwatch = Stopwatch.StartNew();
        var recentQueries = await dbContext.SearchHistories
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.SearchedAt)
            .Select(item => item.NormalizedQuery)
            .Take(MaxSearchQueries)
            .ToListAsync(cancellationToken);
        searchHistoryStopwatch.Stop();
        dbTotalMs += searchHistoryStopwatch.ElapsedMilliseconds;

        var meaningfulInteractionCount = CountMeaningfulInteractions(
            ratingRows,
            favoriteRows,
            watchedMovieRows,
            watchlistRows,
            watchedEpisodeRows,
            catalogFollowRows);

        if (minimumInteractionsForEnrichment > 0 &&
            meaningfulInteractionCount < minimumInteractionsForEnrichment)
        {
            totalStopwatch.Stop();
            LogLoadSummary(
                totalStopwatch.ElapsedMilliseconds,
                dbTotalMs,
                ratingsStopwatch.ElapsedMilliseconds,
                favoritesStopwatch.ElapsedMilliseconds,
                watchedMoviesStopwatch.ElapsedMilliseconds,
                watchlistStopwatch.ElapsedMilliseconds,
                watchedEpisodesStopwatch.ElapsedMilliseconds,
                catalogFollowsStopwatch.ElapsedMilliseconds,
                searchHistoryStopwatch.ElapsedMilliseconds,
                fullyWatchedTvMs: 0,
                watchedTvTitlesMs: 0,
                tvFollowTitlesMs: 0,
                searchMatchMoviesMs: 0,
                searchMatchTvMs: 0,
                movieSignalsDbMs: 0,
                tvSignalsDbMs: 0,
                signalBuildExecutionMode: "skipped",
                ratingCount: ratingRows.Count,
                favoriteCount: favoriteRows.Count,
                watchedMovieCount: watchedMovieRows.Count,
                watchlistCount: watchlistRows.Count,
                watchedEpisodeCount: watchedEpisodeRows.Count,
                catalogFollowCount: catalogFollowRows.Count,
                searchQueryCount: recentQueries.Count,
                movieSignalCount: 0,
                tvSignalCount: 0,
                meaningfulInteractionCount);

            return new UserRecommendationContext(
                [],
                new HashSet<Guid>(),
                new HashSet<Guid>(),
                meaningfulInteractionCount);
        }

        var excludedMovieIds = new HashSet<Guid>();
        var excludedTvShowIds = new HashSet<Guid>();

        foreach (var rating in ratingRows)
        {
            if (rating.MovieId.HasValue)
            {
                excludedMovieIds.Add(rating.MovieId.Value);
            }

            if (rating.TvShowId.HasValue)
            {
                excludedTvShowIds.Add(rating.TvShowId.Value);
            }
        }

        foreach (var favorite in favoriteRows)
        {
            if (favorite.MovieId.HasValue)
            {
                excludedMovieIds.Add(favorite.MovieId.Value);
            }

            if (favorite.TvShowId.HasValue)
            {
                excludedTvShowIds.Add(favorite.TvShowId.Value);
            }
        }

        foreach (var watchedMovie in watchedMovieRows)
        {
            excludedMovieIds.Add(watchedMovie.MovieId);
        }

        foreach (var watchlistItem in watchlistRows)
        {
            if (watchlistItem.MovieId.HasValue)
            {
                excludedMovieIds.Add(watchlistItem.MovieId.Value);
            }

            if (watchlistItem.TvShowId.HasValue)
            {
                excludedTvShowIds.Add(watchlistItem.TvShowId.Value);
            }
        }

        foreach (var follow in catalogFollowRows)
        {
            if (follow.ContentType == CatalogContentType.Movie)
            {
                excludedMovieIds.Add(follow.ContentId);
            }
            else if (follow.ContentType == CatalogContentType.Tv)
            {
                excludedTvShowIds.Add(follow.ContentId);
            }
        }

        var watchedEpisodeTvShowIds = watchedEpisodeRows.Select(row => row.TvShowId).ToList();
        var fullyWatchedTvStopwatch = Stopwatch.StartNew();
        excludedTvShowIds.UnionWith(await GetFullyWatchedTvShowIdsAsync(watchedEpisodeTvShowIds, cancellationToken));
        fullyWatchedTvStopwatch.Stop();
        dbTotalMs += fullyWatchedTvStopwatch.ElapsedMilliseconds;

        var seeds = new List<SignalSeed>();
        seeds.AddRange(CreateRatingSeeds(ratingRows));
        seeds.AddRange(CreateFavoriteSeeds(favoriteRows));
        seeds.AddRange(CreateWatchedMovieSeeds(watchedMovieRows));

        var watchedTvTitlesStopwatch = Stopwatch.StartNew();
        seeds.AddRange(await CreateWatchedTvShowSeedsAsync(watchedEpisodeRows, cancellationToken));
        watchedTvTitlesStopwatch.Stop();
        dbTotalMs += watchedTvTitlesStopwatch.ElapsedMilliseconds;

        seeds.AddRange(CreateWatchlistSeeds(watchlistRows));

        var tvFollowTitlesStopwatch = Stopwatch.StartNew();
        seeds.AddRange(await CreateTvFollowSeedsAsync(catalogFollowRows, cancellationToken));
        tvFollowTitlesStopwatch.Stop();
        dbTotalMs += tvFollowTitlesStopwatch.ElapsedMilliseconds;

        var searchMatchMoviesMs = 0L;
        var searchMatchTvMs = 0L;
        seeds.AddRange(await CreateSearchSeedsAsync(
            recentQueries,
            cancellationToken,
            movieQueryMs => searchMatchMoviesMs = movieQueryMs,
            tvQueryMs => searchMatchTvMs = tvQueryMs));
        dbTotalMs += searchMatchMoviesMs + searchMatchTvMs;

        var collapsedSeeds = CollapseSeeds(seeds);
        var movieSeedList = collapsedSeeds.Where(seed => seed.ContentType == "movie").ToList();
        var tvSeedList = collapsedSeeds.Where(seed => seed.ContentType == "tv").ToList();

        var movieSignalsStopwatch = Stopwatch.StartNew();
        var movieSignals = await BuildMovieSignalsAsync(movieSeedList, cancellationToken);
        movieSignalsStopwatch.Stop();
        dbTotalMs += movieSignalsStopwatch.ElapsedMilliseconds;

        var tvSignalsStopwatch = Stopwatch.StartNew();
        var tvSignals = await BuildTvSignalsAsync(tvSeedList, cancellationToken);
        tvSignalsStopwatch.Stop();
        dbTotalMs += tvSignalsStopwatch.ElapsedMilliseconds;

        totalStopwatch.Stop();
        LogLoadSummary(
            totalStopwatch.ElapsedMilliseconds,
            dbTotalMs,
            ratingsStopwatch.ElapsedMilliseconds,
            favoritesStopwatch.ElapsedMilliseconds,
            watchedMoviesStopwatch.ElapsedMilliseconds,
            watchlistStopwatch.ElapsedMilliseconds,
            watchedEpisodesStopwatch.ElapsedMilliseconds,
            catalogFollowsStopwatch.ElapsedMilliseconds,
            searchHistoryStopwatch.ElapsedMilliseconds,
            fullyWatchedTvStopwatch.ElapsedMilliseconds,
            watchedTvTitlesStopwatch.ElapsedMilliseconds,
            tvFollowTitlesStopwatch.ElapsedMilliseconds,
            searchMatchMoviesMs,
            searchMatchTvMs,
            movieSignalsStopwatch.ElapsedMilliseconds,
            tvSignalsStopwatch.ElapsedMilliseconds,
            signalBuildExecutionMode: "sequential",
            ratingCount: ratingRows.Count,
            favoriteCount: favoriteRows.Count,
            watchedMovieCount: watchedMovieRows.Count,
            watchlistCount: watchlistRows.Count,
            watchedEpisodeCount: watchedEpisodeRows.Count,
            catalogFollowCount: catalogFollowRows.Count,
            searchQueryCount: recentQueries.Count,
            movieSignalCount: movieSignals.Count,
            tvSignalCount: tvSignals.Count,
            meaningfulInteractionCount);

        return new UserRecommendationContext(
            movieSignals.Concat(tvSignals).ToList(),
            excludedMovieIds,
            excludedTvShowIds,
            meaningfulInteractionCount);
    }

    private void LogLoadSummary(
        long totalMs,
        long dbTotalMs,
        long ratingsMs,
        long favoritesMs,
        long watchedMoviesMs,
        long watchlistMs,
        long watchedEpisodesMs,
        long catalogFollowsMs,
        long searchHistoryMs,
        long fullyWatchedTvMs,
        long watchedTvTitlesMs,
        long tvFollowTitlesMs,
        long searchMatchMoviesMs,
        long searchMatchTvMs,
        long movieSignalsDbMs,
        long tvSignalsDbMs,
        string signalBuildExecutionMode,
        int ratingCount,
        int favoriteCount,
        int watchedMovieCount,
        int watchlistCount,
        int watchedEpisodeCount,
        int catalogFollowCount,
        int searchQueryCount,
        int movieSignalCount,
        int tvSignalCount,
        int meaningfulInteractionCount)
    {
        var cpuMs = Math.Max(0, totalMs - dbTotalMs);

        UserRecommendationContextLoaderLogMessages.LogLoadComplete(
            logger,
            totalMs,
            dbTotalMs,
            cpuMs,
            "sequential",
            ratingsMs,
            favoritesMs,
            watchedMoviesMs,
            watchlistMs,
            watchedEpisodesMs,
            catalogFollowsMs,
            searchHistoryMs,
            fullyWatchedTvMs,
            watchedTvTitlesMs,
            tvFollowTitlesMs,
            searchMatchMoviesMs,
            searchMatchTvMs,
            movieSignalsDbMs,
            tvSignalsDbMs,
            signalBuildExecutionMode,
            ratingCount,
            favoriteCount,
            watchedMovieCount,
            watchlistCount,
            watchedEpisodeCount,
            catalogFollowCount,
            searchQueryCount,
            movieSignalCount,
            tvSignalCount,
            meaningfulInteractionCount);
    }

    private static int CountMeaningfulInteractions(
        IReadOnlyList<RatingRow> ratingRows,
        IReadOnlyList<TimestampedContentRow> favoriteRows,
        IReadOnlyList<WatchedMovieRow> watchedMovieRows,
        IReadOnlyList<TimestampedContentRow> watchlistRows,
        IReadOnlyList<WatchedEpisodeRow> watchedEpisodeRows,
        IReadOnlyList<CatalogFollowRow> catalogFollowRows)
    {
        var interactions = new HashSet<(string ContentType, Guid ContentId)>();

        foreach (var rating in ratingRows)
        {
            if (rating.MovieId.HasValue)
            {
                interactions.Add(("movie", rating.MovieId.Value));
            }

            if (rating.TvShowId.HasValue)
            {
                interactions.Add(("tv", rating.TvShowId.Value));
            }
        }

        foreach (var favorite in favoriteRows)
        {
            if (favorite.MovieId.HasValue)
            {
                interactions.Add(("movie", favorite.MovieId.Value));
            }

            if (favorite.TvShowId.HasValue)
            {
                interactions.Add(("tv", favorite.TvShowId.Value));
            }
        }

        foreach (var watchedMovie in watchedMovieRows)
        {
            interactions.Add(("movie", watchedMovie.MovieId));
        }

        foreach (var watchlistItem in watchlistRows)
        {
            if (watchlistItem.MovieId.HasValue)
            {
                interactions.Add(("movie", watchlistItem.MovieId.Value));
            }

            if (watchlistItem.TvShowId.HasValue)
            {
                interactions.Add(("tv", watchlistItem.TvShowId.Value));
            }
        }

        foreach (var watchedEpisode in watchedEpisodeRows
                     .Select(row => row.TvShowId)
                     .Distinct())
        {
            interactions.Add(("tv", watchedEpisode));
        }

        foreach (var follow in catalogFollowRows.Where(row => row.ContentType == CatalogContentType.Tv))
        {
            interactions.Add(("tv", follow.ContentId));
        }

        return interactions.Count;
    }

    private static IEnumerable<SignalSeed> CreateRatingSeeds(IReadOnlyList<RatingRow> ratingRows)
    {
        foreach (var rating in ratingRows)
        {
            if (rating.MovieId.HasValue)
            {
                yield return new SignalSeed(
                    rating.MovieId.Value,
                    "movie",
                    UserBehaviorSignalTypes.Rating,
                    rating.MovieTitle!,
                    rating.Score,
                    rating.SignalAtUtc);
            }

            if (rating.TvShowId.HasValue)
            {
                yield return new SignalSeed(
                    rating.TvShowId.Value,
                    "tv",
                    UserBehaviorSignalTypes.Rating,
                    rating.TvShowTitle!,
                    rating.Score,
                    rating.SignalAtUtc);
            }
        }
    }

    private static IEnumerable<SignalSeed> CreateFavoriteSeeds(IReadOnlyList<TimestampedContentRow> favoriteRows)
    {
        foreach (var favorite in favoriteRows)
        {
            if (favorite.MovieId.HasValue)
            {
                yield return new SignalSeed(
                    favorite.MovieId.Value,
                    "movie",
                    UserBehaviorSignalTypes.Favorite,
                    favorite.MovieTitle!,
                    null,
                    favorite.SignalAtUtc);
            }

            if (favorite.TvShowId.HasValue)
            {
                yield return new SignalSeed(
                    favorite.TvShowId.Value,
                    "tv",
                    UserBehaviorSignalTypes.Favorite,
                    favorite.TvShowTitle!,
                    null,
                    favorite.SignalAtUtc);
            }
        }
    }

    private static IEnumerable<SignalSeed> CreateWatchedMovieSeeds(IReadOnlyList<WatchedMovieRow> watchedMovieRows) =>
        watchedMovieRows.Select(item => new SignalSeed(
            item.MovieId,
            "movie",
            UserBehaviorSignalTypes.Watched,
            item.Title,
            null,
            item.WatchedAt));

    private async Task<IReadOnlyList<SignalSeed>> CreateWatchedTvShowSeedsAsync(
        IReadOnlyList<WatchedEpisodeRow> watchedEpisodeRows,
        CancellationToken cancellationToken)
    {
        if (watchedEpisodeRows.Count == 0)
        {
            return [];
        }

        var watchedByShow = watchedEpisodeRows
            .GroupBy(row => row.TvShowId)
            .Select(group => new
            {
                TvShowId = group.Key,
                LastWatchedAt = group.Max(row => row.WatchedAt)
            })
            .ToList();

        var tvShowIds = watchedByShow.Select(item => item.TvShowId).ToList();
        var tvShowTitles = await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShowIds.Contains(tvShow.Id))
            .Select(tvShow => new { tvShow.Id, tvShow.Title })
            .ToListAsync(cancellationToken);

        var lastWatchedLookup = watchedByShow.ToDictionary(item => item.TvShowId, item => item.LastWatchedAt);

        return tvShowTitles
            .Select(tvShow => new SignalSeed(
                tvShow.Id,
                "tv",
                UserBehaviorSignalTypes.Watched,
                tvShow.Title,
                null,
                lastWatchedLookup[tvShow.Id]))
            .ToList();
    }

    private static IEnumerable<SignalSeed> CreateWatchlistSeeds(IReadOnlyList<TimestampedContentRow> watchlistRows)
    {
        foreach (var watchlistItem in watchlistRows)
        {
            if (watchlistItem.MovieId.HasValue)
            {
                yield return new SignalSeed(
                    watchlistItem.MovieId.Value,
                    "movie",
                    UserBehaviorSignalTypes.Watchlist,
                    watchlistItem.MovieTitle!,
                    null,
                    watchlistItem.SignalAtUtc);
            }

            if (watchlistItem.TvShowId.HasValue)
            {
                yield return new SignalSeed(
                    watchlistItem.TvShowId.Value,
                    "tv",
                    UserBehaviorSignalTypes.Watchlist,
                    watchlistItem.TvShowTitle!,
                    null,
                    watchlistItem.SignalAtUtc);
            }
        }
    }

    private async Task<IReadOnlyList<SignalSeed>> CreateTvFollowSeedsAsync(
        IReadOnlyList<CatalogFollowRow> catalogFollowRows,
        CancellationToken cancellationToken)
    {
        var tvFollows = catalogFollowRows
            .Where(follow => follow.ContentType == CatalogContentType.Tv)
            .ToList();

        if (tvFollows.Count == 0)
        {
            return [];
        }

        var tvShowIds = tvFollows.Select(follow => follow.ContentId).ToList();
        var tvShowTitles = await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShowIds.Contains(tvShow.Id))
            .Select(tvShow => new { tvShow.Id, tvShow.Title })
            .ToListAsync(cancellationToken);

        var followLookup = tvFollows.ToDictionary(follow => follow.ContentId, follow => follow.FollowedAt);

        return tvShowTitles
            .Select(tvShow => new SignalSeed(
                tvShow.Id,
                "tv",
                UserBehaviorSignalTypes.TvFollow,
                tvShow.Title,
                null,
                followLookup[tvShow.Id]))
            .ToList();
    }

    private async Task<IReadOnlyList<SignalSeed>> CreateSearchSeedsAsync(
        IReadOnlyList<string> recentQueries,
        CancellationToken cancellationToken,
        Action<long>? recordMovieQueryMs = null,
        Action<long>? recordTvQueryMs = null)
    {
        var distinctQueries = recentQueries
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctQueries.Count == 0)
        {
            return [];
        }

        var movieQueryStopwatch = Stopwatch.StartNew();
        var matchingMovies = await LoadSearchMatchMoviesAsync(distinctQueries, cancellationToken);
        movieQueryStopwatch.Stop();
        recordMovieQueryMs?.Invoke(movieQueryStopwatch.ElapsedMilliseconds);

        var tvQueryStopwatch = Stopwatch.StartNew();
        var matchingTvShows = await LoadSearchMatchTvShowsAsync(distinctQueries, cancellationToken);
        tvQueryStopwatch.Stop();
        recordTvQueryMs?.Invoke(tvQueryStopwatch.ElapsedMilliseconds);
        var seeds = new List<SignalSeed>();

        foreach (var query in distinctQueries)
        {
            var movieSeed = SelectBestSearchMatch(
                matchingMovies,
                query,
                movie => new SignalSeed(
                    movie.Id,
                    "movie",
                    UserBehaviorSignalTypes.Search,
                    movie.Title,
                    null,
                    null));

            if (movieSeed is not null)
            {
                seeds.Add(movieSeed);
                continue;
            }

            var tvSeed = SelectBestSearchMatch(
                matchingTvShows,
                query,
                tvShow => new SignalSeed(
                    tvShow.Id,
                    "tv",
                    UserBehaviorSignalTypes.Search,
                    tvShow.Title,
                    null,
                    null));

            if (tvSeed is not null)
            {
                seeds.Add(tvSeed);
            }
        }

        return seeds;
    }

    private static List<SignalSeed> CollapseSeeds(IEnumerable<SignalSeed> seeds) =>
        seeds
            .GroupBy(seed => (seed.ContentType, seed.ContentId))
            .Select(group => group
                .OrderByDescending(seed => RecommendationSignalScoring.GetSignalPriority(seed.SignalType))
                .ThenByDescending(seed => seed.RatingScore ?? 0)
                .ThenByDescending(seed => seed.SignalAtUtc ?? DateTime.MinValue)
                .First())
            .ToList();

    private Task<List<SearchMatchRow>> LoadSearchMatchMoviesAsync(
        IReadOnlyList<string> distinctQueries,
        CancellationToken cancellationToken) =>
        FilterMoviesBySearchQueries(dbContext.Movies.AsNoTracking(), distinctQueries)
            .Select(movie => new SearchMatchRow(movie.Id, movie.Title, movie.VoteCount))
            .ToListAsync(cancellationToken);

    private Task<List<SearchMatchRow>> LoadSearchMatchTvShowsAsync(
        IReadOnlyList<string> distinctQueries,
        CancellationToken cancellationToken) =>
        FilterTvShowsBySearchQueries(dbContext.TvShows.AsNoTracking(), distinctQueries)
            .Select(tvShow => new SearchMatchRow(tvShow.Id, tvShow.Title, tvShow.VoteCount))
            .ToListAsync(cancellationToken);

    private static SignalSeed? SelectBestSearchMatch(
        IReadOnlyList<SearchMatchRow> matches,
        string query,
        Func<SearchMatchRow, SignalSeed> createSeed)
    {
        var bestMatch = matches
            .Where(match => TitleMatchesQuery(match.Title, query))
            .OrderByDescending(match => match.VoteCount)
            .FirstOrDefault();

        return bestMatch is null ? null : createSeed(bestMatch);
    }

    internal static bool TitleMatchesQuery(string title, string query) =>
        title.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static IQueryable<Domain.Entities.Movie> FilterMoviesBySearchQueries(
        IQueryable<Domain.Entities.Movie> source,
        IReadOnlyList<string> distinctQueries)
    {
        if (distinctQueries.Count == 0)
        {
            return source.Where(_ => false);
        }

        return source.Where(movie => distinctQueries
            .Any(term => EF.Functions.ILike(movie.Title, "%" + term + "%")));
    }

    private static IQueryable<Domain.Entities.TvShow> FilterTvShowsBySearchQueries(
        IQueryable<Domain.Entities.TvShow> source,
        IReadOnlyList<string> distinctQueries)
    {
        if (distinctQueries.Count == 0)
        {
            return source.Where(_ => false);
        }

        return source.Where(tvShow => distinctQueries
            .Any(term => EF.Functions.ILike(tvShow.Title, "%" + term + "%")));
    }

    private async Task<IReadOnlyList<Guid>> GetFullyWatchedTvShowIdsAsync(
        List<Guid> watchedEpisodeTvShowIds,
        CancellationToken cancellationToken)
    {
        if (watchedEpisodeTvShowIds.Count == 0)
        {
            return [];
        }

        var watchedCounts = watchedEpisodeTvShowIds
            .GroupBy(tvShowId => tvShowId)
            .Select(group => new { TvShowId = group.Key, WatchedCount = group.Count() })
            .ToList();

        var tvShowIds = watchedCounts.Select(item => item.TvShowId).ToList();
        var totalEpisodeCounts = await dbContext.Episodes
            .AsNoTracking()
            .Where(episode => tvShowIds.Contains(episode.Season.TvShowId))
            .GroupBy(episode => episode.Season.TvShowId)
            .Select(group => new { TvShowId = group.Key, TotalCount = group.Count() })
            .ToListAsync(cancellationToken);

        var totals = totalEpisodeCounts.ToDictionary(item => item.TvShowId, item => item.TotalCount);

        return watchedCounts
            .Where(item => totals.TryGetValue(item.TvShowId, out var totalCount) &&
                           totalCount > 0 &&
                           item.WatchedCount >= totalCount)
            .Select(item => item.TvShowId)
            .ToList();
    }

    private async Task<List<UserBehaviorSignal>> BuildMovieSignalsAsync(
        List<SignalSeed> seeds,
        CancellationToken cancellationToken)
    {
        if (seeds.Count == 0)
        {
            return [];
        }

        var movieIds = seeds.Select(seed => seed.ContentId).Distinct().ToList();
        var genreRows = await dbContext.MovieGenres
            .AsNoTracking()
            .Where(item => movieIds.Contains(item.MovieId))
            .Select(item => new GenreRow(item.MovieId, item.GenreId, item.Genre.Name))
            .ToListAsync(cancellationToken);

        var personRows = await dbContext.MoviePeople
            .AsNoTracking()
            .Where(item => movieIds.Contains(item.MovieId) && item.CreditType == CreditType.Cast)
            .GroupBy(item => item.MovieId)
            .Select(group => new PersonRow(
                group.Key,
                group.OrderBy(person => person.PersonId)
                    .Select(person => person.PersonId)
                    .Take(MaxCastPeople)
                    .ToList()))
            .ToListAsync(cancellationToken);

        var keywordRows = await dbContext.MovieKeywords
            .AsNoTracking()
            .Where(item => movieIds.Contains(item.MovieId))
            .Select(item => new KeywordRow(item.MovieId, item.KeywordId))
            .ToListAsync(cancellationToken);

        var catalogRows = await dbContext.Movies
            .AsNoTracking()
            .Where(movie => movieIds.Contains(movie.Id))
            .Select(movie => new CatalogMetadataRow(
                movie.Id,
                movie.VoteAverage,
                movie.ReleaseDate.HasValue ? movie.ReleaseDate.Value.Year : (int?)null))
            .ToListAsync(cancellationToken);

        return seeds
            .Select(seed => CreateSignal(seed, genreRows, personRows, keywordRows, catalogRows))
            .ToList();
    }

    private async Task<List<UserBehaviorSignal>> BuildTvSignalsAsync(
        List<SignalSeed> seeds,
        CancellationToken cancellationToken)
    {
        if (seeds.Count == 0)
        {
            return [];
        }

        var tvShowIds = seeds.Select(seed => seed.ContentId).Distinct().ToList();
        var genreRows = await dbContext.TvShowGenres
            .AsNoTracking()
            .Where(item => tvShowIds.Contains(item.TvShowId))
            .Select(item => new GenreRow(item.TvShowId, item.GenreId, item.Genre.Name))
            .ToListAsync(cancellationToken);

        var personRows = await dbContext.TvShowPeople
            .AsNoTracking()
            .Where(item => tvShowIds.Contains(item.TvShowId) && item.CreditType == CreditType.Cast)
            .GroupBy(item => item.TvShowId)
            .Select(group => new PersonRow(
                group.Key,
                group.OrderBy(person => person.PersonId)
                    .Select(person => person.PersonId)
                    .Take(MaxCastPeople)
                    .ToList()))
            .ToListAsync(cancellationToken);

        var keywordRows = await dbContext.TvShowKeywords
            .AsNoTracking()
            .Where(item => tvShowIds.Contains(item.TvShowId))
            .Select(item => new KeywordRow(item.TvShowId, item.KeywordId))
            .ToListAsync(cancellationToken);

        var catalogRows = await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShowIds.Contains(tvShow.Id))
            .Select(tvShow => new CatalogMetadataRow(
                tvShow.Id,
                tvShow.VoteAverage,
                tvShow.FirstAirDate.HasValue ? tvShow.FirstAirDate.Value.Year : (int?)null))
            .ToListAsync(cancellationToken);

        return seeds
            .Select(seed => CreateSignal(seed, genreRows, personRows, keywordRows, catalogRows))
            .ToList();
    }

    private static UserBehaviorSignal CreateSignal(
        SignalSeed seed,
        List<GenreRow> genreRows,
        List<PersonRow> personRows,
        List<KeywordRow> keywordRows,
        List<CatalogMetadataRow> catalogRows)
    {
        var genres = genreRows.Where(row => row.ContentId == seed.ContentId).ToList();
        var people = personRows.FirstOrDefault(row => row.ContentId == seed.ContentId)?.PersonIds ?? [];
        var keywords = keywordRows
            .Where(row => row.ContentId == seed.ContentId)
            .Select(row => row.KeywordId)
            .Distinct()
            .ToList();
        var catalog = catalogRows.FirstOrDefault(row => row.ContentId == seed.ContentId);

        return new UserBehaviorSignal(
            seed.ContentId,
            seed.ContentType,
            seed.SignalType,
            seed.Title,
            seed.RatingScore,
            seed.SignalAtUtc,
            genres.Select(genre => genre.GenreId).ToList(),
            genres.ToDictionary(genre => genre.GenreId, genre => genre.GenreName),
            people)
        {
            CatalogVoteAverage = catalog?.VoteAverage ?? 0m,
            CatalogYear = catalog?.Year,
            KeywordIds = keywords
        };
    }

    private sealed record RatingRow(
        Guid? MovieId,
        Guid? TvShowId,
        int Score,
        DateTime SignalAtUtc,
        string? MovieTitle,
        string? TvShowTitle);

    private sealed record TimestampedContentRow(
        Guid? MovieId,
        Guid? TvShowId,
        DateTime SignalAtUtc,
        string? MovieTitle,
        string? TvShowTitle);

    private sealed record WatchedMovieRow(Guid MovieId, string Title, DateTime WatchedAt);

    private sealed record WatchedEpisodeRow(Guid TvShowId, DateTime WatchedAt);

    private sealed record CatalogFollowRow(
        CatalogContentType ContentType,
        Guid ContentId,
        DateTime FollowedAt);

    private sealed record SearchMatchRow(Guid Id, string Title, int VoteCount);

    private sealed record SignalSeed(
        Guid ContentId,
        string ContentType,
        string SignalType,
        string Title,
        int? RatingScore,
        DateTime? SignalAtUtc);

    private sealed record GenreRow(Guid ContentId, Guid GenreId, string GenreName);

    private sealed record PersonRow(Guid ContentId, List<Guid> PersonIds);

    private sealed record KeywordRow(Guid ContentId, Guid KeywordId);

    private sealed record CatalogMetadataRow(Guid ContentId, decimal VoteAverage, int? Year);
}

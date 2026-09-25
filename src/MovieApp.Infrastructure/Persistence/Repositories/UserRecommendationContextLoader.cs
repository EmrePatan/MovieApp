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

    public async Task<UserRecommendationContext> LoadAsync(
        Guid userId,
        int minimumInteractionsForEnrichment = 0,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var metrics = new RecommendationQueryMetrics();
        long dbTotalMs = 0;
        long probeMs = 0;

        if (minimumInteractionsForEnrichment > 0)
        {
            var probeStopwatch = Stopwatch.StartNew();
            metrics.RecordRoundTrip();
            var prefetchedMeaningfulInteractionCount =
                await UserRecommendationContextInteractionProbe.CountDistinctMeaningfulInteractionsAsync(
                    dbContext,
                    userId,
                    cancellationToken);
            probeStopwatch.Stop();
            probeMs = probeStopwatch.ElapsedMilliseconds;
            dbTotalMs += probeMs;

            if (prefetchedMeaningfulInteractionCount < minimumInteractionsForEnrichment)
            {
                totalStopwatch.Stop();
                LogPreflightShortCircuit(
                    totalStopwatch.ElapsedMilliseconds,
                    dbTotalMs,
                    probeMs,
                    prefetchedMeaningfulInteractionCount,
                    metrics.DbRoundTrips);

                return new UserRecommendationContext(
                    [],
                    new HashSet<Guid>(),
                    new HashSet<Guid>(),
                    prefetchedMeaningfulInteractionCount);
            }
        }

        var interactions = await UserRecommendationContextInteractionLoader.LoadAsync(
            dbContext,
            userId,
            metrics,
            cancellationToken);
        dbTotalMs += interactions.RatingsMs
            + interactions.FavoritesMs
            + interactions.WatchedMoviesMs
            + interactions.WatchlistMs
            + interactions.WatchedEpisodesMs
            + interactions.CatalogFollowsMs
            + interactions.SearchHistoryMs;

        var meaningfulInteractionCount = CountMeaningfulInteractions(
            interactions.RatingRows,
            interactions.FavoriteRows,
            interactions.WatchedMovieRows,
            interactions.WatchlistRows,
            interactions.WatchedEpisodeRows,
            interactions.CatalogFollowRows);

        if (minimumInteractionsForEnrichment > 0 &&
            meaningfulInteractionCount < minimumInteractionsForEnrichment)
        {
            totalStopwatch.Stop();
            LogLoadSummary(
                totalStopwatch.ElapsedMilliseconds,
                dbTotalMs,
                probeMs,
                interactions,
                fullyWatchedTvMs: 0,
                tvTitleLookupMs: 0,
                searchMatchMoviesMs: 0,
                searchMatchTvMs: 0,
                movieSignalsDbMs: 0,
                tvSignalsDbMs: 0,
                signalBuildExecutionMode: "skipped",
                movieSignalCount: 0,
                tvSignalCount: 0,
                meaningfulInteractionCount,
                metrics.DbRoundTrips);

            return new UserRecommendationContext(
                [],
                new HashSet<Guid>(),
                new HashSet<Guid>(),
                meaningfulInteractionCount);
        }

        var excludedMovieIds = BuildExcludedMovieIds(
            interactions.RatingRows,
            interactions.FavoriteRows,
            interactions.WatchedMovieRows,
            interactions.WatchlistRows,
            interactions.CatalogFollowRows);
        var excludedTvShowIds = BuildExcludedTvShowIds(
            interactions.RatingRows,
            interactions.FavoriteRows,
            interactions.WatchlistRows,
            interactions.CatalogFollowRows);

        // Started shows are surfaced by Continue Watching; recommending them again mirrors watched movies.
        excludedTvShowIds.UnionWith(interactions.WatchedEpisodeRows.Select(row => row.TvShowId));

        var tvTitleLookupStopwatch = Stopwatch.StartNew();
        var tvShowTitleLookup = await LoadTvShowTitleLookupAsync(
            interactions.WatchedEpisodeRows,
            interactions.CatalogFollowRows,
            metrics,
            cancellationToken);
        tvTitleLookupStopwatch.Stop();
        dbTotalMs += tvTitleLookupStopwatch.ElapsedMilliseconds;

        var seeds = new List<UserRecommendationContextModels.SignalSeed>();
        seeds.AddRange(CreateRatingSeeds(interactions.RatingRows));
        seeds.AddRange(CreateFavoriteSeeds(interactions.FavoriteRows));
        seeds.AddRange(CreateWatchedMovieSeeds(interactions.WatchedMovieRows));
        seeds.AddRange(CreateWatchedTvShowSeeds(interactions.WatchedEpisodeRows, tvShowTitleLookup));
        seeds.AddRange(CreateWatchlistSeeds(interactions.WatchlistRows));
        seeds.AddRange(CreateTvFollowSeeds(interactions.CatalogFollowRows, tvShowTitleLookup));

        var searchMatchMoviesMs = 0L;
        var searchMatchTvMs = 0L;
        seeds.AddRange(await CreateSearchSeedsAsync(
            interactions.RecentQueries,
            metrics,
            cancellationToken,
            movieQueryMs => searchMatchMoviesMs = movieQueryMs,
            tvQueryMs => searchMatchTvMs = tvQueryMs));
        dbTotalMs += searchMatchMoviesMs + searchMatchTvMs;

        var collapsedSeeds = CollapseSeeds(seeds);
        var movieSeedList = collapsedSeeds.Where(seed => seed.ContentType == "movie").ToList();
        var tvSeedList = collapsedSeeds.Where(seed => seed.ContentType == "tv").ToList();

        var movieSignalsStopwatch = Stopwatch.StartNew();
        var movieSignals = await BuildMovieSignalsAsync(movieSeedList, metrics, cancellationToken);
        movieSignalsStopwatch.Stop();
        dbTotalMs += movieSignalsStopwatch.ElapsedMilliseconds;

        var tvSignalsStopwatch = Stopwatch.StartNew();
        var tvSignals = await BuildTvSignalsAsync(tvSeedList, metrics, cancellationToken);
        tvSignalsStopwatch.Stop();
        dbTotalMs += tvSignalsStopwatch.ElapsedMilliseconds;

        totalStopwatch.Stop();
        LogLoadSummary(
            totalStopwatch.ElapsedMilliseconds,
            dbTotalMs,
            probeMs,
            interactions,
            0,
            tvTitleLookupStopwatch.ElapsedMilliseconds,
            searchMatchMoviesMs,
            searchMatchTvMs,
            movieSignalsStopwatch.ElapsedMilliseconds,
            tvSignalsStopwatch.ElapsedMilliseconds,
            signalBuildExecutionMode: "split-query",
            movieSignalCount: movieSignals.Count,
            tvSignalCount: tvSignals.Count,
            meaningfulInteractionCount,
            metrics.DbRoundTrips);

        return new UserRecommendationContext(
            movieSignals.Concat(tvSignals).ToList(),
            excludedMovieIds,
            excludedTvShowIds,
            meaningfulInteractionCount);
    }

    private void LogPreflightShortCircuit(
        long totalMs,
        long dbTotalMs,
        long probeMs,
        int meaningfulInteractionCount,
        int dbRoundTrips)
    {
        var cpuMs = Math.Max(0, totalMs - dbTotalMs);

        UserRecommendationContextLoaderLogMessages.LogPreflightShortCircuit(
            logger,
            totalMs,
            dbTotalMs,
            cpuMs,
            probeMs,
            meaningfulInteractionCount,
            dbRoundTrips);
    }

    private void LogLoadSummary(
        long totalMs,
        long dbTotalMs,
        long probeMs,
        UserRecommendationContextInteractionLoader.UserInteractionSnapshot interactions,
        long fullyWatchedTvMs,
        long tvTitleLookupMs,
        long searchMatchMoviesMs,
        long searchMatchTvMs,
        long movieSignalsDbMs,
        long tvSignalsDbMs,
        string signalBuildExecutionMode,
        int movieSignalCount,
        int tvSignalCount,
        int meaningfulInteractionCount,
        int dbRoundTrips)
    {
        var cpuMs = Math.Max(0, totalMs - dbTotalMs);

        UserRecommendationContextLoaderLogMessages.LogLoadComplete(
            logger,
            totalMs,
            dbTotalMs,
            cpuMs,
            probeMs,
            interactions.ExecutionMode,
            interactions.RatingsMs,
            interactions.FavoritesMs,
            interactions.WatchedMoviesMs,
            interactions.WatchlistMs,
            interactions.WatchedEpisodesMs,
            interactions.CatalogFollowsMs,
            interactions.SearchHistoryMs,
            fullyWatchedTvMs,
            tvTitleLookupMs,
            0,
            searchMatchMoviesMs,
            searchMatchTvMs,
            movieSignalsDbMs,
            tvSignalsDbMs,
            signalBuildExecutionMode,
            interactions.RatingRows.Count,
            interactions.FavoriteRows.Count,
            interactions.WatchedMovieRows.Count,
            interactions.WatchlistRows.Count,
            interactions.WatchedEpisodeRows.Count,
            interactions.CatalogFollowRows.Count,
            interactions.RecentQueries.Count,
            movieSignalCount,
            tvSignalCount,
            meaningfulInteractionCount,
            dbRoundTrips);
    }

    private static HashSet<Guid> BuildExcludedMovieIds(
        IReadOnlyList<UserRecommendationContextModels.RatingRow> ratingRows,
        IReadOnlyList<UserRecommendationContextModels.TimestampedContentRow> favoriteRows,
        IReadOnlyList<UserRecommendationContextModels.WatchedMovieRow> watchedMovieRows,
        IReadOnlyList<UserRecommendationContextModels.TimestampedContentRow> watchlistRows,
        IReadOnlyList<UserRecommendationContextModels.CatalogFollowRow> catalogFollowRows)
    {
        var excludedMovieIds = new HashSet<Guid>();

        foreach (var rating in ratingRows)
        {
            if (rating.MovieId.HasValue)
            {
                excludedMovieIds.Add(rating.MovieId.Value);
            }
        }

        foreach (var favorite in favoriteRows)
        {
            if (favorite.MovieId.HasValue)
            {
                excludedMovieIds.Add(favorite.MovieId.Value);
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
        }

        foreach (var follow in catalogFollowRows)
        {
            if (follow.ContentType == CatalogContentType.Movie)
            {
                excludedMovieIds.Add(follow.ContentId);
            }
        }

        return excludedMovieIds;
    }

    private static HashSet<Guid> BuildExcludedTvShowIds(
        IReadOnlyList<UserRecommendationContextModels.RatingRow> ratingRows,
        IReadOnlyList<UserRecommendationContextModels.TimestampedContentRow> favoriteRows,
        IReadOnlyList<UserRecommendationContextModels.TimestampedContentRow> watchlistRows,
        IReadOnlyList<UserRecommendationContextModels.CatalogFollowRow> catalogFollowRows)
    {
        var excludedTvShowIds = new HashSet<Guid>();

        foreach (var rating in ratingRows)
        {
            if (rating.TvShowId.HasValue)
            {
                excludedTvShowIds.Add(rating.TvShowId.Value);
            }
        }

        foreach (var favorite in favoriteRows)
        {
            if (favorite.TvShowId.HasValue)
            {
                excludedTvShowIds.Add(favorite.TvShowId.Value);
            }
        }

        foreach (var watchlistItem in watchlistRows)
        {
            if (watchlistItem.TvShowId.HasValue)
            {
                excludedTvShowIds.Add(watchlistItem.TvShowId.Value);
            }
        }

        foreach (var follow in catalogFollowRows)
        {
            if (follow.ContentType == CatalogContentType.Tv)
            {
                excludedTvShowIds.Add(follow.ContentId);
            }
        }

        return excludedTvShowIds;
    }

    internal static int CountMeaningfulInteractions(
        IReadOnlyList<UserRecommendationContextModels.RatingRow> ratingRows,
        IReadOnlyList<UserRecommendationContextModels.TimestampedContentRow> favoriteRows,
        IReadOnlyList<UserRecommendationContextModels.WatchedMovieRow> watchedMovieRows,
        IReadOnlyList<UserRecommendationContextModels.TimestampedContentRow> watchlistRows,
        IReadOnlyList<UserRecommendationContextModels.WatchedEpisodeRow> watchedEpisodeRows,
        IReadOnlyList<UserRecommendationContextModels.CatalogFollowRow> catalogFollowRows)
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

    private static IEnumerable<UserRecommendationContextModels.SignalSeed> CreateRatingSeeds(
        IReadOnlyList<UserRecommendationContextModels.RatingRow> ratingRows)
    {
        foreach (var rating in ratingRows)
        {
            if (rating.MovieId.HasValue)
            {
                yield return new UserRecommendationContextModels.SignalSeed(
                    rating.MovieId.Value,
                    "movie",
                    UserBehaviorSignalTypes.Rating,
                    rating.MovieTitle!,
                    rating.Score,
                    rating.SignalAtUtc);
            }

            if (rating.TvShowId.HasValue)
            {
                yield return new UserRecommendationContextModels.SignalSeed(
                    rating.TvShowId.Value,
                    "tv",
                    UserBehaviorSignalTypes.Rating,
                    rating.TvShowTitle!,
                    rating.Score,
                    rating.SignalAtUtc);
            }
        }
    }

    private static IEnumerable<UserRecommendationContextModels.SignalSeed> CreateFavoriteSeeds(
        IReadOnlyList<UserRecommendationContextModels.TimestampedContentRow> favoriteRows)
    {
        foreach (var favorite in favoriteRows)
        {
            if (favorite.MovieId.HasValue)
            {
                yield return new UserRecommendationContextModels.SignalSeed(
                    favorite.MovieId.Value,
                    "movie",
                    UserBehaviorSignalTypes.Favorite,
                    favorite.MovieTitle!,
                    null,
                    favorite.SignalAtUtc);
            }

            if (favorite.TvShowId.HasValue)
            {
                yield return new UserRecommendationContextModels.SignalSeed(
                    favorite.TvShowId.Value,
                    "tv",
                    UserBehaviorSignalTypes.Favorite,
                    favorite.TvShowTitle!,
                    null,
                    favorite.SignalAtUtc);
            }
        }
    }

    private static IEnumerable<UserRecommendationContextModels.SignalSeed> CreateWatchedMovieSeeds(
        IReadOnlyList<UserRecommendationContextModels.WatchedMovieRow> watchedMovieRows) =>
        watchedMovieRows.Select(item => new UserRecommendationContextModels.SignalSeed(
            item.MovieId,
            "movie",
            UserBehaviorSignalTypes.Watched,
            item.Title,
            null,
            item.WatchedAt));

    internal static List<UserRecommendationContextModels.SignalSeed> CreateWatchedTvShowSeeds(
        IReadOnlyList<UserRecommendationContextModels.WatchedEpisodeRow> watchedEpisodeRows,
        Dictionary<Guid, string> tvShowTitleLookup)
    {
        if (watchedEpisodeRows.Count == 0)
        {
            return [];
        }

        return watchedEpisodeRows
            .GroupBy(row => row.TvShowId)
            .Where(group => tvShowTitleLookup.ContainsKey(group.Key))
            .Select(group => new UserRecommendationContextModels.SignalSeed(
                group.Key,
                "tv",
                UserBehaviorSignalTypes.Watched,
                tvShowTitleLookup[group.Key],
                null,
                group.Max(row => row.WatchedAt)))
            .ToList();
    }

    private static IEnumerable<UserRecommendationContextModels.SignalSeed> CreateWatchlistSeeds(
        IReadOnlyList<UserRecommendationContextModels.TimestampedContentRow> watchlistRows)
    {
        foreach (var watchlistItem in watchlistRows)
        {
            if (watchlistItem.MovieId.HasValue)
            {
                yield return new UserRecommendationContextModels.SignalSeed(
                    watchlistItem.MovieId.Value,
                    "movie",
                    UserBehaviorSignalTypes.Watchlist,
                    watchlistItem.MovieTitle!,
                    null,
                    watchlistItem.SignalAtUtc);
            }

            if (watchlistItem.TvShowId.HasValue)
            {
                yield return new UserRecommendationContextModels.SignalSeed(
                    watchlistItem.TvShowId.Value,
                    "tv",
                    UserBehaviorSignalTypes.Watchlist,
                    watchlistItem.TvShowTitle!,
                    null,
                    watchlistItem.SignalAtUtc);
            }
        }
    }

    private static List<UserRecommendationContextModels.SignalSeed> CreateTvFollowSeeds(
        IReadOnlyList<UserRecommendationContextModels.CatalogFollowRow> catalogFollowRows,
        Dictionary<Guid, string> tvShowTitleLookup)
    {
        return catalogFollowRows
            .Where(follow => follow.ContentType == CatalogContentType.Tv)
            .Where(follow => tvShowTitleLookup.ContainsKey(follow.ContentId))
            .Select(follow => new UserRecommendationContextModels.SignalSeed(
                follow.ContentId,
                "tv",
                UserBehaviorSignalTypes.TvFollow,
                tvShowTitleLookup[follow.ContentId],
                null,
                follow.FollowedAt))
            .ToList();
    }

    private async Task<Dictionary<Guid, string>> LoadTvShowTitleLookupAsync(
        IReadOnlyList<UserRecommendationContextModels.WatchedEpisodeRow> watchedEpisodeRows,
        IReadOnlyList<UserRecommendationContextModels.CatalogFollowRow> catalogFollowRows,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        var tvShowIds = watchedEpisodeRows
            .Select(row => row.TvShowId)
            .Concat(catalogFollowRows
                .Where(follow => follow.ContentType == CatalogContentType.Tv)
                .Select(follow => follow.ContentId))
            .Distinct()
            .ToList();

        if (tvShowIds.Count == 0)
        {
            return [];
        }

        metrics.RecordRoundTrip();
        var rows = await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShowIds.Contains(tvShow.Id))
            .Select(tvShow => new { tvShow.Id, tvShow.Title })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Id, row => row.Title);
    }

    private async Task<IReadOnlyList<UserRecommendationContextModels.SignalSeed>> CreateSearchSeedsAsync(
        IReadOnlyList<string> recentQueries,
        RecommendationQueryMetrics metrics,
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
        metrics.RecordRoundTrip();
        var matchingMovies = await LoadSearchMatchMoviesAsync(distinctQueries, cancellationToken);
        movieQueryStopwatch.Stop();
        recordMovieQueryMs?.Invoke(movieQueryStopwatch.ElapsedMilliseconds);

        var tvQueryStopwatch = Stopwatch.StartNew();
        metrics.RecordRoundTrip();
        var matchingTvShows = await LoadSearchMatchTvShowsAsync(distinctQueries, cancellationToken);
        tvQueryStopwatch.Stop();
        recordTvQueryMs?.Invoke(tvQueryStopwatch.ElapsedMilliseconds);

        var seeds = new List<UserRecommendationContextModels.SignalSeed>();

        foreach (var query in distinctQueries)
        {
            var movieSeed = SelectBestSearchMatch(
                matchingMovies,
                query,
                movie => new UserRecommendationContextModels.SignalSeed(
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
                tvShow => new UserRecommendationContextModels.SignalSeed(
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

    private static List<UserRecommendationContextModels.SignalSeed> CollapseSeeds(
        IEnumerable<UserRecommendationContextModels.SignalSeed> seeds) =>
        seeds
            .GroupBy(seed => (seed.ContentType, seed.ContentId))
            .Select(group => group
                .OrderByDescending(seed => RecommendationSignalScoring.GetSignalPriority(seed.SignalType))
                .ThenByDescending(seed => seed.RatingScore ?? 0)
                .ThenByDescending(seed => seed.SignalAtUtc ?? DateTime.MinValue)
                .First())
            .ToList();

    private Task<List<UserRecommendationContextModels.SearchMatchRow>> LoadSearchMatchMoviesAsync(
        IReadOnlyList<string> distinctQueries,
        CancellationToken cancellationToken) =>
        BestMovieTitleMatches(dbContext.Movies.AsNoTracking(), distinctQueries)
            .ToListAsync(cancellationToken);

    private Task<List<UserRecommendationContextModels.SearchMatchRow>> LoadSearchMatchTvShowsAsync(
        IReadOnlyList<string> distinctQueries,
        CancellationToken cancellationToken) =>
        BestTvShowTitleMatches(dbContext.TvShows.AsNoTracking(), distinctQueries)
            .ToListAsync(cancellationToken);

    internal static IQueryable<UserRecommendationContextModels.SearchMatchRow> BestMovieTitleMatches(
        IQueryable<Domain.Entities.Movie> movies,
        IReadOnlyList<string> distinctQueries)
    {
        if (distinctQueries.Count == 0)
        {
            return movies.Where(_ => false).Select(movie => new UserRecommendationContextModels.SearchMatchRow(
                movie.Id,
                movie.Title,
                movie.VoteCount));
        }

        IQueryable<Domain.Entities.Movie>? union = null;
        foreach (var term in distinctQueries)
        {
            var branch = movies
                .Where(movie => EF.Functions.ILike(movie.Title, "%" + term + "%"))
                .OrderByDescending(movie => movie.VoteCount)
                .ThenBy(movie => movie.Id)
                .Take(UserRecommendationContextModels.SearchMatchCandidatesPerQuery);
            union = union is null ? branch : union.Concat(branch);
        }

        return union!.Select(movie => new UserRecommendationContextModels.SearchMatchRow(
            movie.Id,
            movie.Title,
            movie.VoteCount));
    }

    internal static IQueryable<UserRecommendationContextModels.SearchMatchRow> BestTvShowTitleMatches(
        IQueryable<Domain.Entities.TvShow> tvShows,
        IReadOnlyList<string> distinctQueries)
    {
        if (distinctQueries.Count == 0)
        {
            return tvShows.Where(_ => false).Select(tvShow => new UserRecommendationContextModels.SearchMatchRow(
                tvShow.Id,
                tvShow.Title,
                tvShow.VoteCount));
        }

        IQueryable<Domain.Entities.TvShow>? union = null;
        foreach (var term in distinctQueries)
        {
            var branch = tvShows
                .Where(tvShow => EF.Functions.ILike(tvShow.Title, "%" + term + "%"))
                .OrderByDescending(tvShow => tvShow.VoteCount)
                .ThenBy(tvShow => tvShow.Id)
                .Take(UserRecommendationContextModels.SearchMatchCandidatesPerQuery);
            union = union is null ? branch : union.Concat(branch);
        }

        return union!.Select(tvShow => new UserRecommendationContextModels.SearchMatchRow(
            tvShow.Id,
            tvShow.Title,
            tvShow.VoteCount));
    }

    private static UserRecommendationContextModels.SignalSeed? SelectBestSearchMatch(
        IReadOnlyList<UserRecommendationContextModels.SearchMatchRow> matches,
        string query,
        Func<UserRecommendationContextModels.SearchMatchRow, UserRecommendationContextModels.SignalSeed> createSeed)
    {
        var bestMatch = matches
            .Where(match => TitleMatchesQuery(match.Title, query))
            .OrderByDescending(match => match.VoteCount)
            .FirstOrDefault();

        return bestMatch is null ? null : createSeed(bestMatch);
    }

    internal static bool TitleMatchesQuery(string title, string query) =>
        title.Contains(query, StringComparison.OrdinalIgnoreCase);

    private async Task<List<UserBehaviorSignal>> BuildMovieSignalsAsync(
        List<UserRecommendationContextModels.SignalSeed> seeds,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        if (seeds.Count == 0)
        {
            return [];
        }

        var movieIds = seeds.Select(seed => seed.ContentId).Distinct().ToList();
        metrics.RecordRoundTrip();
        var projections = await dbContext.Movies
            .AsNoTracking()
            .AsSplitQuery()
            .Where(movie => movieIds.Contains(movie.Id))
            .Select(movie => new UserRecommendationContextModels.MovieSignalProjection(
                movie.Id,
                movie.VoteAverage,
                movie.ReleaseDate.HasValue ? movie.ReleaseDate.Value.Year : (int?)null,
                movie.MovieGenres
                    .Select(genre => new UserRecommendationContextModels.GenreRow(
                        movie.Id,
                        genre.GenreId,
                        genre.Genre.Name))
                    .ToList(),
                movie.MovieKeywords
                    .Select(keyword => new UserRecommendationContextModels.KeywordRow(movie.Id, keyword.KeywordId))
                    .ToList(),
                movie.MoviePeople
                    .Where(person => person.CreditType == CreditType.Cast)
                    .OrderBy(person => person.PersonId)
                    .Select(person => person.PersonId)
                    .Take(MaxCastPeople)
                    .ToList()))
            .ToListAsync(cancellationToken);

        var projectionLookup = projections.ToDictionary(projection => projection.Id);

        return seeds
            .Where(seed => projectionLookup.ContainsKey(seed.ContentId))
            .Select(seed => CreateSignal(seed, projectionLookup[seed.ContentId]))
            .ToList();
    }

    private async Task<List<UserBehaviorSignal>> BuildTvSignalsAsync(
        List<UserRecommendationContextModels.SignalSeed> seeds,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        if (seeds.Count == 0)
        {
            return [];
        }

        var tvShowIds = seeds.Select(seed => seed.ContentId).Distinct().ToList();
        metrics.RecordRoundTrip();
        var projections = await dbContext.TvShows
            .AsNoTracking()
            .AsSplitQuery()
            .Where(tvShow => tvShowIds.Contains(tvShow.Id))
            .Select(tvShow => new UserRecommendationContextModels.TvSignalProjection(
                tvShow.Id,
                tvShow.VoteAverage,
                tvShow.FirstAirDate.HasValue ? tvShow.FirstAirDate.Value.Year : (int?)null,
                tvShow.TvShowGenres
                    .Select(genre => new UserRecommendationContextModels.GenreRow(
                        tvShow.Id,
                        genre.GenreId,
                        genre.Genre.Name))
                    .ToList(),
                tvShow.TvShowKeywords
                    .Select(keyword => new UserRecommendationContextModels.KeywordRow(tvShow.Id, keyword.KeywordId))
                    .ToList(),
                tvShow.TvShowPeople
                    .Where(person => person.CreditType == CreditType.Cast)
                    .OrderBy(person => person.PersonId)
                    .Select(person => person.PersonId)
                    .Take(MaxCastPeople)
                    .ToList()))
            .ToListAsync(cancellationToken);

        var projectionLookup = projections.ToDictionary(projection => projection.Id);

        return seeds
            .Where(seed => projectionLookup.ContainsKey(seed.ContentId))
            .Select(seed => CreateSignal(seed, projectionLookup[seed.ContentId]))
            .ToList();
    }

    private static UserBehaviorSignal CreateSignal(
        UserRecommendationContextModels.SignalSeed seed,
        UserRecommendationContextModels.MovieSignalProjection projection)
    {
        var genres = projection.Genres;
        var keywords = projection.Keywords
            .Select(row => row.KeywordId)
            .Distinct()
            .ToList();

        return new UserBehaviorSignal(
            seed.ContentId,
            seed.ContentType,
            seed.SignalType,
            seed.Title,
            seed.RatingScore,
            seed.SignalAtUtc,
            genres.Select(genre => genre.GenreId).ToList(),
            genres.ToDictionary(genre => genre.GenreId, genre => genre.GenreName),
            projection.PersonIds)
        {
            CatalogVoteAverage = projection.VoteAverage,
            CatalogYear = projection.Year,
            KeywordIds = keywords
        };
    }

    private static UserBehaviorSignal CreateSignal(
        UserRecommendationContextModels.SignalSeed seed,
        UserRecommendationContextModels.TvSignalProjection projection)
    {
        var genres = projection.Genres;
        var keywords = projection.Keywords
            .Select(row => row.KeywordId)
            .Distinct()
            .ToList();

        return new UserBehaviorSignal(
            seed.ContentId,
            seed.ContentType,
            seed.SignalType,
            seed.Title,
            seed.RatingScore,
            seed.SignalAtUtc,
            genres.Select(genre => genre.GenreId).ToList(),
            genres.ToDictionary(genre => genre.GenreId, genre => genre.GenreName),
            projection.PersonIds)
        {
            CatalogVoteAverage = projection.VoteAverage,
            CatalogYear = projection.Year,
            KeywordIds = keywords
        };
    }
}

using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal sealed class UserRecommendationContextLoader(ApplicationDbContext dbContext)
{
    private const int MaxCastPeople = 20;
    private const int MaxSearchQueries = 10;

    public async Task<UserRecommendationContext> LoadAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var ratingRows = await dbContext.Ratings
            .AsNoTracking()
            .Where(rating => rating.UserId == userId)
            .Select(rating => new RatingRow(
                rating.MovieId,
                rating.TvShowId,
                rating.Score,
                rating.MovieId != null ? rating.Movie!.Title : null,
                rating.TvShowId != null ? rating.TvShow!.Title : null))
            .ToListAsync(cancellationToken);

        var favoriteRows = await dbContext.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId)
            .Select(favorite => new FavoriteRow(
                favorite.MovieId,
                favorite.TvShowId,
                favorite.MovieId != null ? favorite.Movie!.Title : null,
                favorite.TvShowId != null ? favorite.TvShow!.Title : null))
            .ToListAsync(cancellationToken);

        var watchedMovieRows = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.WatchedAt)
            .Select(item => new WatchedMovieRow(item.MovieId, item.Movie!.Title))
            .ToListAsync(cancellationToken);

        var watchlistRows = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.Watchlist.UserId == userId)
            .Select(item => new WatchlistRow(
                item.MovieId,
                item.TvShowId,
                item.MovieId != null ? item.Movie!.Title : null,
                item.TvShowId != null ? item.TvShow!.Title : null))
            .ToListAsync(cancellationToken);

        var watchedEpisodeRows = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.Episode.Season.TvShowId)
            .ToListAsync(cancellationToken);

        var recentQueries = await dbContext.SearchHistories
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.SearchedAt)
            .Select(item => item.NormalizedQuery)
            .Take(MaxSearchQueries)
            .ToListAsync(cancellationToken);

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

        excludedTvShowIds.UnionWith(await GetFullyWatchedTvShowIdsAsync(watchedEpisodeRows, cancellationToken));

        var seeds = new List<SignalSeed>();
        seeds.AddRange(CreateRatingSeeds(ratingRows));
        seeds.AddRange(CreateFavoriteSeeds(favoriteRows));
        seeds.AddRange(CreateWatchedMovieSeeds(watchedMovieRows));
        seeds.AddRange(await CreateWatchedTvShowSeedsAsync(watchedEpisodeRows, cancellationToken));
        seeds.AddRange(CreateWatchlistSeeds(watchlistRows));
        seeds.AddRange(await CreateSearchSeedsAsync(recentQueries, cancellationToken));

        var movieSignals = await BuildMovieSignalsAsync(
            seeds.Where(seed => seed.ContentType == "movie").ToList(),
            cancellationToken);
        var tvSignals = await BuildTvSignalsAsync(
            seeds.Where(seed => seed.ContentType == "tv").ToList(),
            cancellationToken);

        var meaningfulInteractionCount =
            ratingRows.Count +
            favoriteRows.Count +
            watchedMovieRows.Count +
            watchedEpisodeRows.Count +
            watchlistRows.Count;

        return new UserRecommendationContext(
            movieSignals.Concat(tvSignals).ToList(),
            excludedMovieIds,
            excludedTvShowIds,
            meaningfulInteractionCount);
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
                    rating.Score);
            }

            if (rating.TvShowId.HasValue)
            {
                yield return new SignalSeed(
                    rating.TvShowId.Value,
                    "tv",
                    UserBehaviorSignalTypes.Rating,
                    rating.TvShowTitle!,
                    rating.Score);
            }
        }
    }

    private static IEnumerable<SignalSeed> CreateFavoriteSeeds(IReadOnlyList<FavoriteRow> favoriteRows)
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
                    null);
            }

            if (favorite.TvShowId.HasValue)
            {
                yield return new SignalSeed(
                    favorite.TvShowId.Value,
                    "tv",
                    UserBehaviorSignalTypes.Favorite,
                    favorite.TvShowTitle!,
                    null);
            }
        }
    }

    private static IEnumerable<SignalSeed> CreateWatchedMovieSeeds(IReadOnlyList<WatchedMovieRow> watchedMovieRows) =>
        watchedMovieRows.Select(item => new SignalSeed(
            item.MovieId,
            "movie",
            UserBehaviorSignalTypes.Watched,
            item.Title,
            null));

    private async Task<IReadOnlyList<SignalSeed>> CreateWatchedTvShowSeedsAsync(
        IReadOnlyList<Guid> watchedEpisodeTvShowIds,
        CancellationToken cancellationToken)
    {
        var distinctTvShowIds = watchedEpisodeTvShowIds.Distinct().ToList();
        if (distinctTvShowIds.Count == 0)
        {
            return [];
        }

        var tvShowTitles = await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => distinctTvShowIds.Contains(tvShow.Id))
            .Select(tvShow => new { tvShow.Id, tvShow.Title })
            .ToListAsync(cancellationToken);

        return tvShowTitles
            .Select(tvShow => new SignalSeed(
                tvShow.Id,
                "tv",
                UserBehaviorSignalTypes.Watched,
                tvShow.Title,
                null))
            .ToList();
    }

    private static IEnumerable<SignalSeed> CreateWatchlistSeeds(IReadOnlyList<WatchlistRow> watchlistRows)
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
                    null);
            }

            if (watchlistItem.TvShowId.HasValue)
            {
                yield return new SignalSeed(
                    watchlistItem.TvShowId.Value,
                    "tv",
                    UserBehaviorSignalTypes.Watchlist,
                    watchlistItem.TvShowTitle!,
                    null);
            }
        }
    }

    private async Task<IReadOnlyList<SignalSeed>> CreateSearchSeedsAsync(
        IReadOnlyList<string> recentQueries,
        CancellationToken cancellationToken)
    {
        var distinctQueries = recentQueries
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctQueries.Count == 0)
        {
            return [];
        }

        var matchingMovies = await LoadSearchMatchMoviesAsync(distinctQueries, cancellationToken);
        var matchingTvShows = await LoadSearchMatchTvShowsAsync(distinctQueries, cancellationToken);
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
                    null));

            if (tvSeed is not null)
            {
                seeds.Add(tvSeed);
            }
        }

        return seeds;
    }

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
            .Select(group => new PersonRow(group.Key, group.Select(person => person.PersonId).Take(MaxCastPeople).ToList()))
            .ToListAsync(cancellationToken);

        return seeds
            .Select(seed => CreateSignal(seed, genreRows, personRows))
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
            .Select(group => new PersonRow(group.Key, group.Select(person => person.PersonId).Take(MaxCastPeople).ToList()))
            .ToListAsync(cancellationToken);

        return seeds
            .Select(seed => CreateSignal(seed, genreRows, personRows))
            .ToList();
    }

    private static UserBehaviorSignal CreateSignal(
        SignalSeed seed,
        List<GenreRow> genreRows,
        List<PersonRow> personRows)
    {
        var genres = genreRows.Where(row => row.ContentId == seed.ContentId).ToList();
        var people = personRows.FirstOrDefault(row => row.ContentId == seed.ContentId)?.PersonIds ?? [];

        return new UserBehaviorSignal(
            seed.ContentId,
            seed.ContentType,
            seed.SignalType,
            seed.Title,
            seed.RatingScore,
            genres.Select(genre => genre.GenreId).ToList(),
            genres.ToDictionary(genre => genre.GenreId, genre => genre.GenreName),
            people);
    }

    private sealed record RatingRow(
        Guid? MovieId,
        Guid? TvShowId,
        int Score,
        string? MovieTitle,
        string? TvShowTitle);

    private sealed record FavoriteRow(
        Guid? MovieId,
        Guid? TvShowId,
        string? MovieTitle,
        string? TvShowTitle);

    private sealed record WatchedMovieRow(Guid MovieId, string Title);

    private sealed record WatchlistRow(
        Guid? MovieId,
        Guid? TvShowId,
        string? MovieTitle,
        string? TvShowTitle);

    private sealed record SearchMatchRow(Guid Id, string Title, int VoteCount);

    private sealed record SignalSeed(
        Guid ContentId,
        string ContentType,
        string SignalType,
        string Title,
        int? RatingScore);

    private sealed record GenreRow(Guid ContentId, Guid GenreId, string GenreName);

    private sealed record PersonRow(Guid ContentId, List<Guid> PersonIds);
}

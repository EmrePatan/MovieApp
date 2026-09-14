using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class UserStatisticsRepository(ApplicationDbContext dbContext) : IUserStatisticsRepository
{
    private sealed record ProfileStatisticsCounts(
        int FavoriteMovieCount,
        int FavoriteTvShowCount,
        int WatchlistCount,
        int WatchlistItemCount,
        int RatedMovieCount,
        int RatedTvShowCount,
        int ReviewedMovieCount,
        int ReviewedTvShowCount,
        int WatchedMovieCount,
        int WatchedEpisodeCount);

    public async Task<UserStatisticsResult> GetStatisticsAsync(
        Guid userId,
        string? timeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        var counts = await GetCountsAsync(userId, cancellationToken);

        var showsStarted = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId)
            .Select(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
            .Distinct()
            .CountAsync(cancellationToken);

        var showsCompleted = await CountCompletedShowsAsync(userId, cancellationToken);
        var monthlyActivity = await GetMonthlyActivityAsync(userId, cancellationToken);
        var watchTimestampsUtc = await GetWatchTimestampsUtcAsync(userId, cancellationToken);
        var distinctWatchDates = ProfileWatchDateHelper.TryGetTimeZone(timeZoneId, out var timeZone)
            ? ProfileWatchDateHelper.ToDistinctLocalWatchDates(watchTimestampsUtc, timeZone)
            : [];
        var genres = await GetGenreStatisticsAsync(userId, cancellationToken);
        var ratingScoreCounts = await GetRatingScoreCountsAsync(userId, cancellationToken);
        var firstMovieWatchedAt = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId)
            .OrderBy(watchedMovie => watchedMovie.WatchedAt)
            .Select(watchedMovie => (DateTime?)watchedMovie.WatchedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var firstCompletedShowAt = await GetFirstCompletedShowAtAsync(userId, cancellationToken);

        var raw = new ProfileStatisticsRawData(
            counts.FavoriteMovieCount,
            counts.FavoriteTvShowCount,
            counts.WatchlistCount,
            counts.WatchlistItemCount,
            counts.RatedMovieCount,
            counts.RatedTvShowCount,
            counts.ReviewedMovieCount,
            counts.ReviewedTvShowCount,
            counts.WatchedMovieCount,
            counts.WatchedEpisodeCount,
            showsStarted,
            showsCompleted,
            monthlyActivity,
            distinctWatchDates,
            genres,
            ratingScoreCounts,
            firstMovieWatchedAt,
            firstCompletedShowAt);

        return ProfileStatisticsBuilder.Build(raw, DateTime.UtcNow);
    }

    private async Task<ProfileStatisticsCounts> GetCountsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new ProfileStatisticsCounts(
                dbContext.Favorites.Count(favorite => favorite.UserId == userId && favorite.MovieId != null),
                dbContext.Favorites.Count(favorite => favorite.UserId == userId && favorite.TvShowId != null),
                dbContext.Watchlists.Count(watchlist => watchlist.UserId == userId),
                dbContext.WatchlistItems.Count(item => item.Watchlist.UserId == userId),
                dbContext.Ratings.Count(rating => rating.UserId == userId && rating.MovieId != null),
                dbContext.Ratings.Count(rating => rating.UserId == userId && rating.TvShowId != null),
                dbContext.Reviews.Count(review => review.UserId == userId && review.MovieId != null),
                dbContext.Reviews.Count(review => review.UserId == userId && review.TvShowId != null),
                dbContext.WatchedMovies.Count(watchedMovie => watchedMovie.UserId == userId),
                dbContext.WatchedEpisodes.Count(watchedEpisode => watchedEpisode.UserId == userId)))
            .FirstAsync(cancellationToken);
    }

    private async Task<int> CountCompletedShowsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => dbContext.WatchedEpisodes.Any(
                watchedEpisode => watchedEpisode.UserId == userId &&
                                  watchedEpisode.Episode.Season.TvShowId == tvShow.Id))
            .Select(tvShow => new
            {
                TotalEpisodes = tvShow.Seasons
                    .Where(season => season.SeasonNumber >= 1)
                    .SelectMany(season => season.Episodes)
                    .Count(),
                WatchedEpisodes = dbContext.WatchedEpisodes.Count(
                    watchedEpisode => watchedEpisode.UserId == userId &&
                                      watchedEpisode.Episode.Season.TvShowId == tvShow.Id &&
                                      watchedEpisode.Episode.Season.SeasonNumber >= 1),
            })
            .CountAsync(
                show => show.TotalEpisodes > 0 && show.WatchedEpisodes >= show.TotalEpisodes,
                cancellationToken);
    }

    private async Task<DateTime?> GetFirstCompletedShowAtAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => dbContext.WatchedEpisodes.Any(
                watchedEpisode => watchedEpisode.UserId == userId &&
                                  watchedEpisode.Episode.Season.TvShowId == tvShow.Id))
            .Select(tvShow => new
            {
                TotalEpisodes = tvShow.Seasons
                    .Where(season => season.SeasonNumber >= 1)
                    .SelectMany(season => season.Episodes)
                    .Count(),
                WatchedEpisodes = dbContext.WatchedEpisodes.Count(
                    watchedEpisode => watchedEpisode.UserId == userId &&
                                      watchedEpisode.Episode.Season.TvShowId == tvShow.Id &&
                                      watchedEpisode.Episode.Season.SeasonNumber >= 1),
                LastWatchedAt = dbContext.WatchedEpisodes
                    .Where(watchedEpisode => watchedEpisode.UserId == userId &&
                                             watchedEpisode.Episode.Season.TvShowId == tvShow.Id &&
                                             watchedEpisode.Episode.Season.SeasonNumber >= 1)
                    .Max(watchedEpisode => (DateTime?)watchedEpisode.WatchedAt),
            })
            .Where(show => show.TotalEpisodes > 0 &&
                           show.WatchedEpisodes >= show.TotalEpisodes &&
                           show.LastWatchedAt != null)
            .OrderBy(show => show.LastWatchedAt)
            .Select(show => show.LastWatchedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<MonthlyActivityResult>> GetMonthlyActivityAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rangeStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(-11);

        var movieMonths = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId && watchedMovie.WatchedAt >= rangeStart)
            .GroupBy(watchedMovie => new
            {
                watchedMovie.WatchedAt.Year,
                watchedMovie.WatchedAt.Month,
            })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken);

        var episodeMonths = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId && watchedEpisode.WatchedAt >= rangeStart)
            .GroupBy(watchedEpisode => new
            {
                watchedEpisode.WatchedAt.Year,
                watchedEpisode.WatchedAt.Month,
            })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken);

        var monthKeys = movieMonths
            .Select(item => (item.Year, item.Month))
            .Concat(episodeMonths.Select(item => (item.Year, item.Month)))
            .Distinct()
            .ToList();

        return monthKeys
            .Select(key =>
            {
                var movies = movieMonths
                    .FirstOrDefault(item => item.Year == key.Year && item.Month == key.Month)?.Count ?? 0;
                var episodes = episodeMonths
                    .FirstOrDefault(item => item.Year == key.Year && item.Month == key.Month)?.Count ?? 0;

                return new MonthlyActivityResult(key.Year, key.Month, movies, episodes, movies + episodes);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<DateTime>> GetWatchTimestampsUtcAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId)
            .Select(watchedMovie => watchedMovie.WatchedAt)
            .Concat(
                dbContext.WatchedEpisodes
                    .AsNoTracking()
                    .Where(watchedEpisode => watchedEpisode.UserId == userId)
                    .Select(watchedEpisode => watchedEpisode.WatchedAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<GenreStatisticResult>> GetGenreStatisticsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var movieGenreCounts = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId)
            .SelectMany(watchedMovie => watchedMovie.Movie!.MovieGenres.Select(movieGenre => new
            {
                movieGenre.GenreId,
                movieGenre.Genre.Name,
                watchedMovie.MovieId,
            }))
            .GroupBy(item => new { item.GenreId, item.Name })
            .Select(group => new GenreStatisticResult(
                group.Key.GenreId,
                group.Key.Name,
                group.Select(item => item.MovieId).Distinct().Count()))
            .ToListAsync(cancellationToken);

        var episodeGenreCounts = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId)
            .SelectMany(watchedEpisode => watchedEpisode.Episode.Season.TvShow.TvShowGenres.Select(tvGenre => new
            {
                tvGenre.GenreId,
                tvGenre.Genre.Name,
                TvShowId = watchedEpisode.Episode.Season.TvShowId,
            }))
            .GroupBy(item => new { item.GenreId, item.Name })
            .Select(group => new GenreStatisticResult(
                group.Key.GenreId,
                group.Key.Name,
                group.Select(item => item.TvShowId).Distinct().Count()))
            .ToListAsync(cancellationToken);

        return movieGenreCounts
            .Concat(episodeGenreCounts)
            .GroupBy(genre => genre.GenreId)
            .Select(group => new GenreStatisticResult(
                group.Key,
                group.First().Name,
                group.Sum(item => item.Count)))
            .OrderByDescending(genre => genre.Count)
            .ThenBy(genre => genre.Name)
            .ToList();
    }

    private async Task<IReadOnlyList<(int Score, int Count)>> GetRatingScoreCountsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Ratings
            .AsNoTracking()
            .Where(rating => rating.UserId == userId)
            .GroupBy(rating => rating.Score)
            .Select(group => new ValueTuple<int, int>(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
    }
}

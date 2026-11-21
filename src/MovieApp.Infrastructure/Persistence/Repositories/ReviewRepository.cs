using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Reviews;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class ReviewRepository(ApplicationDbContext dbContext) : IReviewRepository
{
    public async Task<Review?> GetByUserAndMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .AsNoTracking()
            .Include(review => review.User)
            .FirstOrDefaultAsync(
                review => review.UserId == userId && review.MovieId == movieId,
                cancellationToken);
    }

    public async Task<Review?> GetByUserAndTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .AsNoTracking()
            .Include(review => review.User)
            .FirstOrDefaultAsync(
                review => review.UserId == userId && review.TvShowId == tvShowId,
                cancellationToken);
    }

    public async Task<Review?> GetByUserAndEpisodeAsync(
        Guid userId,
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .AsNoTracking()
            .Include(review => review.User)
            .FirstOrDefaultAsync(
                review => review.UserId == userId && review.EpisodeId == episodeId,
                cancellationToken);
    }

    public async Task<Review?> GetTrackedByUserAndMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .Include(review => review.User)
            .FirstOrDefaultAsync(
                review => review.UserId == userId && review.MovieId == movieId,
                cancellationToken);
    }

    public async Task<Review?> GetTrackedByUserAndTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .Include(review => review.User)
            .FirstOrDefaultAsync(
                review => review.UserId == userId && review.TvShowId == tvShowId,
                cancellationToken);
    }

    public async Task<Review?> GetTrackedByUserAndEpisodeAsync(
        Guid userId,
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .Include(review => review.User)
            .FirstOrDefaultAsync(
                review => review.UserId == userId && review.EpisodeId == episodeId,
                cancellationToken);
    }

    public async Task<bool> ExistsForMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .AsNoTracking()
            .AnyAsync(
                review => review.UserId == userId && review.MovieId == movieId,
                cancellationToken);
    }

    public async Task<bool> ExistsForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .AsNoTracking()
            .AnyAsync(
                review => review.UserId == userId && review.TvShowId == tvShowId,
                cancellationToken);
    }

    public async Task<bool> ExistsForEpisodeAsync(
        Guid userId,
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .AsNoTracking()
            .AnyAsync(
                review => review.UserId == userId && review.EpisodeId == episodeId,
                cancellationToken);
    }

    public async Task<Review> AddAsync(Review review, CancellationToken cancellationToken = default)
    {
        review.ValidateInvariants();

        try
        {
            dbContext.Reviews.Add(review);
            await dbContext.SaveChangesAsync(cancellationToken);
            return review;
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            dbContext.Entry(review).State = EntityState.Detached;
            throw new ConflictException(
                review.MovieId is not null
                    ? "A review for this movie already exists."
                    : review.TvShowId is not null
                        ? "A review for this TV show already exists."
                        : "A review for this episode already exists.");
        }
    }

    public async Task UpdateAsync(Review review, CancellationToken cancellationToken = default)
    {
        review.ValidateInvariants();
        dbContext.Reviews.Update(review);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteForMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var review = await dbContext.Reviews
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.MovieId == movieId,
                cancellationToken);

        if (review is null)
        {
            return false;
        }

        dbContext.Reviews.Remove(review);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var review = await dbContext.Reviews
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.TvShowId == tvShowId,
                cancellationToken);

        if (review is null)
        {
            return false;
        }

        dbContext.Reviews.Remove(review);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteForEpisodeAsync(
        Guid userId,
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        var review = await dbContext.Reviews
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.EpisodeId == episodeId,
                cancellationToken);

        if (review is null)
        {
            return false;
        }

        dbContext.Reviews.Remove(review);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(IReadOnlyList<PublicReviewListItem> Reviews, int TotalCount)> GetPublicReviewsForMovieAsync(
        Guid movieId,
        int page,
        int pageSize,
        ReviewListSort sort,
        int? ratingStars = null,
        CancellationToken cancellationToken = default)
    {
        var reviewsQuery = dbContext.Reviews
            .AsNoTracking()
            .Include(item => item.User)
            .Where(item => item.MovieId == movieId);

        var ratingsQuery = dbContext.Ratings
            .AsNoTracking()
            .Where(item => item.MovieId == movieId);

        return await GetPublicReviewsAsync(
            reviewsQuery,
            ratingsQuery,
            page,
            pageSize,
            sort,
            ratingStars,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<PublicReviewListItem> Reviews, int TotalCount)> GetPublicReviewsForTvShowAsync(
        Guid tvShowId,
        int page,
        int pageSize,
        ReviewListSort sort,
        int? ratingStars = null,
        CancellationToken cancellationToken = default)
    {
        var reviewsQuery = dbContext.Reviews
            .AsNoTracking()
            .Include(item => item.User)
            .Where(item => item.TvShowId == tvShowId);

        var ratingsQuery = dbContext.Ratings
            .AsNoTracking()
            .Where(item => item.TvShowId == tvShowId);

        return await GetPublicReviewsAsync(
            reviewsQuery,
            ratingsQuery,
            page,
            pageSize,
            sort,
            ratingStars,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<PublicReviewListItem> Reviews, int TotalCount)> GetPublicReviewsForEpisodeAsync(
        Guid episodeId,
        int page,
        int pageSize,
        ReviewListSort sort,
        int? ratingStars = null,
        CancellationToken cancellationToken = default)
    {
        var reviewsQuery = dbContext.Reviews
            .AsNoTracking()
            .Include(item => item.User)
            .Where(item => item.EpisodeId == episodeId);

        var ratingsQuery = dbContext.Ratings
            .AsNoTracking()
            .Where(item => item.EpisodeId == episodeId);

        return await GetPublicReviewsAsync(
            reviewsQuery,
            ratingsQuery,
            page,
            pageSize,
            sort,
            ratingStars,
            cancellationToken);
    }

    private static async Task<(IReadOnlyList<PublicReviewListItem> Reviews, int TotalCount)> GetPublicReviewsAsync(
        IQueryable<Review> reviewsQuery,
        IQueryable<Rating> ratingsQuery,
        int page,
        int pageSize,
        ReviewListSort sort,
        int? ratingStars,
        CancellationToken cancellationToken)
    {
        var query =
            from review in reviewsQuery
            join rating in ratingsQuery on review.UserId equals rating.UserId into ratings
            from rating in ratings.DefaultIfEmpty()
            select new { review, rating };

        if (ratingStars.HasValue)
        {
            var stars = ratingStars.Value;
            query = query.Where(item =>
                item.rating != null &&
                (item.rating.Score + 1) / 2 == stars);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var orderedQuery = sort switch
        {
            ReviewListSort.Oldest => query.OrderBy(item => item.review.CreatedAt),
            ReviewListSort.RatingDesc => query
                .OrderByDescending(item => item.rating == null ? -1 : item.rating.Score)
                .ThenByDescending(item => item.review.CreatedAt),
            ReviewListSort.RatingAsc => query
                .OrderBy(item => item.rating == null ? int.MaxValue : item.rating.Score)
                .ThenByDescending(item => item.review.CreatedAt),
            _ => query.OrderByDescending(item => item.review.CreatedAt),
        };

        var rows = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        IReadOnlyList<PublicReviewListItem> reviews = rows
            .Select(item => new PublicReviewListItem(item.review, item.rating?.Score))
            .ToList();

        return (reviews, totalCount);
    }

    public Task<IReadOnlyDictionary<int, int>> GetReviewScoreDistributionForMovieAsync(
        Guid movieId,
        CancellationToken cancellationToken = default) =>
        GetReviewScoreDistributionAsync(
            dbContext.Reviews.AsNoTracking().Where(review => review.MovieId == movieId),
            dbContext.Ratings.AsNoTracking().Where(rating => rating.MovieId == movieId),
            cancellationToken);

    public Task<IReadOnlyDictionary<int, int>> GetReviewScoreDistributionForTvShowAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default) =>
        GetReviewScoreDistributionAsync(
            dbContext.Reviews.AsNoTracking().Where(review => review.TvShowId == tvShowId),
            dbContext.Ratings.AsNoTracking().Where(rating => rating.TvShowId == tvShowId),
            cancellationToken);

    public Task<IReadOnlyDictionary<int, int>> GetReviewScoreDistributionForEpisodeAsync(
        Guid episodeId,
        CancellationToken cancellationToken = default) =>
        GetReviewScoreDistributionAsync(
            dbContext.Reviews.AsNoTracking().Where(review => review.EpisodeId == episodeId),
            dbContext.Ratings.AsNoTracking().Where(rating => rating.EpisodeId == episodeId),
            cancellationToken);

    private static async Task<IReadOnlyDictionary<int, int>> GetReviewScoreDistributionAsync(
        IQueryable<Review> reviewsQuery,
        IQueryable<Rating> ratingsQuery,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from review in reviewsQuery
                join rating in ratingsQuery on review.UserId equals rating.UserId
                group rating by rating.Score
                into scoreGroup
                select new { Score = scoreGroup.Key, Count = scoreGroup.Count() })
            .ToListAsync(cancellationToken);

        var distribution = RatingMapper.CreateEmptyDistribution()
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var row in rows)
        {
            if (distribution.ContainsKey(row.Score))
            {
                distribution[row.Score] = row.Count;
            }
        }

        return distribution;
    }

    public async Task<ReviewTranslationSource?> GetTranslationSourceByIdAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .AsNoTracking()
            .Where(review => review.Id == reviewId)
            .Select(review => new ReviewTranslationSource(
                review.Id,
                review.Content,
                review.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<UserReviewCatalogListItem> Reviews, int TotalCount)> GetUserReviewsAsync(
        Guid userId,
        int page,
        int pageSize,
        SearchContentType contentType,
        CancellationToken cancellationToken = default)
    {
        var reviewsQuery = dbContext.Reviews
            .AsNoTracking()
            .Where(review => review.UserId == userId);

        if (contentType == SearchContentType.Movie)
        {
            reviewsQuery = reviewsQuery.Where(review => review.MovieId != null);
        }
        else if (contentType == SearchContentType.Tv)
        {
            // Show-level + episode reviews both belong under the TV filter.
            reviewsQuery = reviewsQuery.Where(review =>
                review.TvShowId != null || review.EpisodeId != null);
        }
        // All: movies, shows, and episodes — no EpisodeId exclusion.

        var totalCount = await reviewsQuery.CountAsync(cancellationToken);

        var rows = await reviewsQuery
            .OrderByDescending(review => review.UpdatedAt)
            .ThenByDescending(review => review.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(review => new UserReviewPageRow(
                review.Id,
                review.UserId,
                review.MovieId,
                review.TvShowId,
                review.EpisodeId,
                review.Content,
                review.CreatedAt,
                review.UpdatedAt,
                review.Movie != null
                    ? review.Movie.Title
                    : review.TvShow != null
                        ? review.TvShow.Title
                        : review.Episode!.Season.TvShow.Title,
                review.Movie != null
                    ? review.Movie.PosterPath
                    : review.TvShow != null
                        ? review.TvShow.PosterPath
                        : review.Episode!.Season.TvShow.PosterPath ?? review.Episode.StillPath,
                review.Movie != null
                    ? review.Movie.ReleaseDate
                    : review.TvShow != null
                        ? review.TvShow.FirstAirDate
                        : review.Episode!.AirDate,
                review.Episode != null ? review.Episode.Season.TvShowId : null,
                review.Episode != null ? review.Episode.Season.SeasonNumber : null,
                review.Episode != null ? review.Episode.EpisodeNumber : null,
                review.Episode != null ? review.Episode.Name : null,
                review.Episode != null ? review.Episode.StillPath : null))
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return (Array.Empty<UserReviewCatalogListItem>(), totalCount);
        }

        var movieIds = rows
            .Where(review => review.MovieId != null)
            .Select(review => review.MovieId!.Value)
            .ToList();
        var tvShowIds = rows
            .Where(review => review.TvShowId != null)
            .Select(review => review.TvShowId!.Value)
            .ToList();
        var episodeIds = rows
            .Where(review => review.EpisodeId != null)
            .Select(review => review.EpisodeId!.Value)
            .ToList();

        var ratings = await dbContext.Ratings
            .AsNoTracking()
            .Where(rating =>
                rating.UserId == userId &&
                ((rating.MovieId != null && movieIds.Contains(rating.MovieId.Value)) ||
                 (rating.TvShowId != null && tvShowIds.Contains(rating.TvShowId.Value)) ||
                 (rating.EpisodeId != null && episodeIds.Contains(rating.EpisodeId.Value))))
            .Select(rating => new { rating.MovieId, rating.TvShowId, rating.EpisodeId, rating.Score })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row =>
            {
                int? userRating = null;
                if (row.MovieId is Guid movieId)
                {
                    userRating = ratings.FirstOrDefault(rating => rating.MovieId == movieId)?.Score;
                }
                else if (row.TvShowId is Guid tvShowId)
                {
                    userRating = ratings.FirstOrDefault(rating => rating.TvShowId == tvShowId)?.Score;
                }
                else if (row.EpisodeId is Guid episodeId)
                {
                    userRating = ratings.FirstOrDefault(rating => rating.EpisodeId == episodeId)?.Score;
                }

                return new UserReviewCatalogListItem(row.ToReview(), userRating);
            })
            .ToList();

        return (items, totalCount);
    }

    private sealed record UserReviewPageRow(
        Guid Id,
        Guid UserId,
        Guid? MovieId,
        Guid? TvShowId,
        Guid? EpisodeId,
        string Content,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        string Title,
        string? PosterPath,
        DateOnly? ReleaseDate,
        Guid? EpisodeTvShowId,
        int? SeasonNumber,
        int? EpisodeNumber,
        string? EpisodeName,
        string? EpisodeStillPath)
    {
        public Review ToReview()
        {
            var review = new Review
            {
                Id = Id,
                UserId = UserId,
                MovieId = MovieId,
                TvShowId = TvShowId,
                EpisodeId = EpisodeId,
                Content = Content,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt
            };

            if (MovieId is Guid movieId)
            {
                review.Movie = new Movie
                {
                    Id = movieId,
                    Title = Title,
                    PosterPath = PosterPath,
                    ReleaseDate = ReleaseDate
                };
            }
            else if (TvShowId is Guid tvShowId)
            {
                review.TvShow = new TvShow
                {
                    Id = tvShowId,
                    Title = Title,
                    PosterPath = PosterPath,
                    FirstAirDate = ReleaseDate
                };
            }
            else if (EpisodeId is Guid episodeId)
            {
                var season = new Season
                {
                    SeasonNumber = SeasonNumber ?? 0,
                    TvShowId = EpisodeTvShowId ?? Guid.Empty,
                    TvShow = EpisodeTvShowId is Guid showId
                        ? new TvShow
                        {
                            Id = showId,
                            Title = Title,
                            PosterPath = PosterPath
                        }
                        : null!
                };

                review.Episode = new Episode
                {
                    Id = episodeId,
                    Name = EpisodeName,
                    EpisodeNumber = EpisodeNumber ?? 0,
                    AirDate = ReleaseDate,
                    StillPath = EpisodeStillPath,
                    Season = season
                };
            }

            return review;
        }
    }
}

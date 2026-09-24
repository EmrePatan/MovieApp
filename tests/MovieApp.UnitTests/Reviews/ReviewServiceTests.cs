using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Reviews;
using MovieApp.Application.Services.Reviews;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Ratings;

using MovieApp.UnitTests.Caching;

namespace MovieApp.UnitTests.Reviews;

public sealed class ReviewServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid MovieId = Guid.NewGuid();

    [Fact]
    public async Task CreateMovieReviewAsyncCreatesReview()
    {
        var repository = new FakeReviewRepository();
        var service = CreateService(repository, movie: CreateMovie());

        var result = await service.CreateMovieReviewAsync(MovieId, "Great movie", "en-US");

        Assert.Equal("Great movie", result.Content);
        Assert.Equal("en-US", result.AuthoringLocale);
        Assert.Equal(1, repository.AddCount);
    }

    [Fact]
    public async Task CreateMovieReviewAsyncThrowsConflictForDuplicate()
    {
        var repository = new FakeReviewRepository(existsForMovie: true);
        var service = CreateService(repository, movie: CreateMovie());

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateMovieReviewAsync(MovieId, "Duplicate", "en-US"));
    }

    [Fact]
    public async Task UpdateMovieReviewAsyncThrowsWhenReviewMissing()
    {
        var repository = new FakeReviewRepository();
        var service = CreateService(repository, movie: CreateMovie());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateMovieReviewAsync(MovieId, "Updated", "en-US"));
    }

    [Fact]
    public async Task UpdateMovieReviewAsyncUpdatesOwnReview()
    {
        var review = Review.CreateForMovie(UserId, MovieId, "Original", "en-US", DateTime.UtcNow);
        review.User = new User { Id = UserId, DisplayName = "Emre" };

        var repository = new FakeReviewRepository(trackedMovieReview: review);
        var service = CreateService(repository, movie: CreateMovie());

        var result = await service.UpdateMovieReviewAsync(MovieId, "Updated content", "tr-TR");

        Assert.Equal("Updated content", result.Content);
        Assert.Equal("tr-TR", result.AuthoringLocale);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public async Task CreateMovieReviewAsyncThrowsWhenMovieMissing()
    {
        var service = CreateService(new FakeReviewRepository(), movie: null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateMovieReviewAsync(MovieId, "Missing movie", "en-US"));
    }

    [Fact]
    public async Task CreateMovieReviewAsyncThrowsForInvalidContent()
    {
        var service = CreateService(new FakeReviewRepository(), movie: CreateMovie());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateMovieReviewAsync(MovieId, "   ", "en-US"));
    }

    [Fact]
    public async Task GetMovieReviewsAsyncReturnsUnfilteredReviewScoreDistribution()
    {
        var distribution = RatingMapper.CreateEmptyDistribution().ToDictionary(pair => pair.Key, pair => pair.Value);
        distribution[3] = 1;
        distribution[4] = 1;

        var service = CreateService(
            new FakeReviewRepository(reviewScoreDistribution: distribution),
            CreateMovie());

        var result = await service.GetMovieReviewsAsync(
            MovieId,
            page: 1,
            pageSize: 20,
            ReviewListSort.Newest,
            ratingStars: 2);

        Assert.Equal(1, result.ReviewScoreDistribution[3]);
        Assert.Equal(1, result.ReviewScoreDistribution[4]);
        Assert.Equal(0, result.ReviewScoreDistribution[8]);
        Assert.Equal(0, result.Page.TotalCount);
        Assert.Equal(2, RatingStarMapping.ToStarBucket(3));
        Assert.Equal(2, RatingStarMapping.ToStarBucket(4));
    }

    private static ReviewService CreateService(FakeReviewRepository repository, Movie? movie) =>
        new(
            new FakeCurrentUser(UserId),
            repository,
            new FakeMovieRepository(movie),
            new FakeTvShowRepository(null),
            new FakeUserAnalyticsCacheInvalidator());

    private static Movie CreateMovie() =>
        new()
        {
            Id = MovieId,
            Title = "Interstellar",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class FakeReviewRepository(
        bool existsForMovie = false,
        Review? trackedMovieReview = null,
        IReadOnlyDictionary<int, int>? reviewScoreDistribution = null) : IReviewRepository
    {
        private Review? _createdReview;

        public int AddCount { get; private set; }

        public int UpdateCount { get; private set; }

        public Task<Review?> GetByUserAndMovieAsync(
            Guid userId,
            Guid movieId,
            CancellationToken cancellationToken = default)
        {
            if (_createdReview is not null)
            {
                return Task.FromResult<Review?>(_createdReview);
            }

            if (trackedMovieReview is null)
            {
                return Task.FromResult<Review?>(null);
            }

            trackedMovieReview.User = new User { Id = userId, DisplayName = "Emre" };
            return Task.FromResult<Review?>(trackedMovieReview);
        }

        public Task<Review?> GetByUserAndTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Review?>(null);

        public Task<Review?> GetTrackedByUserAndMovieAsync(
            Guid userId,
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(trackedMovieReview);

        public Task<Review?> GetTrackedByUserAndTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Review?>(null);

        public Task<bool> ExistsForMovieAsync(
            Guid userId,
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(existsForMovie);

        public Task<bool> ExistsForTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<Review> AddAsync(Review review, CancellationToken cancellationToken = default)
        {
            AddCount++;
            review.User = new User { Id = review.UserId, DisplayName = "Emre" };
            _createdReview = review;
            return Task.FromResult(review);
        }

        public Task UpdateAsync(Review review, CancellationToken cancellationToken = default)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteForMovieAsync(
            Guid userId,
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> DeleteForTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(IReadOnlyList<PublicReviewListItem> Reviews, int TotalCount)> GetPublicReviewsForMovieAsync(
            Guid movieId,
            int page,
            int pageSize,
            ReviewListSort sort,
            int? ratingStars = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<PublicReviewListItem>, int)>(([], 0));

        public Task<(IReadOnlyList<PublicReviewListItem> Reviews, int TotalCount)> GetPublicReviewsForTvShowAsync(
            Guid tvShowId,
            int page,
            int pageSize,
            ReviewListSort sort,
            int? ratingStars = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<PublicReviewListItem>, int)>(([], 0));

        public Task<IReadOnlyDictionary<int, int>> GetReviewScoreDistributionForMovieAsync(
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(reviewScoreDistribution ?? RatingMapper.CreateEmptyDistribution());

        public Task<IReadOnlyDictionary<int, int>> GetReviewScoreDistributionForTvShowAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RatingMapper.CreateEmptyDistribution());

        public Task<ReviewTranslationSource?> GetTranslationSourceByIdAsync(
            Guid reviewId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ReviewTranslationSource?>(null);
    }

    private sealed class FakeMovieRepository(Movie? movie) : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(movie);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie> UpsertFromProviderAsync(
            Application.Models.Providers.MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTvShowRepository(TvShow? tvShow) : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(tvShow);

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<TvShow> UpsertFromProviderAsync(
            Application.Models.Providers.TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

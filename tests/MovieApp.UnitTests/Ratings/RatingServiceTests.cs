using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Ratings;
using MovieApp.Application.Services.Ratings;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Ratings;

public sealed class RatingServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid MovieId = Guid.NewGuid();
    private static readonly Guid TvShowId = Guid.NewGuid();

    [Fact]
    public async Task UpsertMovieRatingAsyncCreatesNewRating()
    {
        var repository = new FakeRatingRepository();
        var service = CreateService(repository, movie: CreateMovie(), tvShow: null);

        var result = await service.UpsertMovieRatingAsync(MovieId, 9);

        Assert.True(result.Created);
        Assert.Equal(9, result.Rating.Score);
        Assert.Equal(1, repository.AddCount);
    }

    [Fact]
    public async Task UpsertMovieRatingAsyncUpdatesExistingRating()
    {
        var repository = new FakeRatingRepository(
            existingMovieRating: Rating.CreateForMovie(UserId, MovieId, 5, DateTime.UtcNow));
        var service = CreateService(repository, movie: CreateMovie(), tvShow: null);

        var result = await service.UpsertMovieRatingAsync(MovieId, 9);

        Assert.False(result.Created);
        Assert.Equal(9, result.Rating.Score);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public async Task DeleteMovieRatingAsyncThrowsWhenMissing()
    {
        var service = CreateService(new FakeRatingRepository(), movie: CreateMovie(), tvShow: null);

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteMovieRatingAsync(MovieId));
    }

    [Fact]
    public async Task UpsertMovieRatingAsyncThrowsWhenMovieMissing()
    {
        var service = CreateService(new FakeRatingRepository(), movie: null, tvShow: null);

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpsertMovieRatingAsync(MovieId, 8));
    }

    [Fact]
    public async Task UpsertMovieRatingAsyncThrowsForInvalidScore()
    {
        var service = CreateService(new FakeRatingRepository(), movie: CreateMovie(), tvShow: null);

        await Assert.ThrowsAsync<ValidationException>(() => service.UpsertMovieRatingAsync(MovieId, 0));
    }

    [Fact]
    public async Task GetCurrentUserMovieRatingAsyncReturnsRating()
    {
        var existing = Rating.CreateForMovie(UserId, MovieId, 7, DateTime.UtcNow);
        var service = CreateService(
            new FakeRatingRepository(existingMovieRating: existing),
            movie: CreateMovie(),
            tvShow: null);

        var result = await service.GetCurrentUserMovieRatingAsync(MovieId);

        Assert.Equal(7, result.Score);
    }

    [Fact]
    public async Task GetMovieRatingSummaryAsyncReturnsEmptySummaryWhenNoRatings()
    {
        var service = CreateService(new FakeRatingRepository(), movie: CreateMovie(), tvShow: null);

        var result = await service.GetMovieRatingSummaryAsync(MovieId);

        Assert.Equal(0, result.RatingCount);
        Assert.Equal(0m, result.AverageScore);
        Assert.Equal(10, result.ScoreDistribution.Count);
        Assert.All(result.ScoreDistribution.Values, count => Assert.Equal(0, count));
    }

    private static RatingService CreateService(
        FakeRatingRepository ratingRepository,
        Movie? movie,
        TvShow? tvShow) =>
        new(
            new FakeCurrentUser(UserId),
            ratingRepository,
            new FakeMovieRepository(movie),
            new FakeTvShowRepository(tvShow));

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

    private sealed class FakeRatingRepository(Rating? existingMovieRating = null) : IRatingRepository
    {
        public int AddCount { get; private set; }

        public int UpdateCount { get; private set; }

        public Task<Rating?> GetByUserAndMovieAsync(
            Guid userId,
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(existingMovieRating);

        public Task<Rating?> GetByUserAndTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Rating?>(null);

        public Task<Rating> AddAsync(Rating rating, CancellationToken cancellationToken = default)
        {
            AddCount++;
            return Task.FromResult(rating);
        }

        public Task UpdateAsync(Rating rating, CancellationToken cancellationToken = default)
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

        public Task<RatingSummaryResult> GetSummaryForMovieAsync(
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RatingSummaryResult(0m, 0, RatingMapper.CreateEmptyDistribution()));

        public Task<RatingSummaryResult> GetSummaryForTvShowAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RatingSummaryResult(0m, 0, RatingMapper.CreateEmptyDistribution()));
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

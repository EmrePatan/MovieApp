using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Reviews;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Reviews;
using MovieApp.Application.Services.Reviews;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Reviews;

public sealed class ReviewTranslationServiceTests
{
    private static readonly Guid ReviewId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime UpdatedAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [Fact]
    public async Task TranslateAsyncReturnsCachedResultWithoutCallingProvider()
    {
        var cache = new FakeCacheService();
        var provider = new FakeTranslationProvider();
        var repository = new FakeTranslationReviewRepository(
            new ReviewTranslationSource(ReviewId, "Hola mundo", UpdatedAt));

        await cache.SetAsync(
            ReviewTranslationCacheKeys.For(ReviewId, UpdatedAt, "en-US"),
            new ReviewTranslationCacheEntry
            {
                Outcome = ReviewTranslationOutcome.Translated,
                TranslatedText = "Hello world",
                DetectedSourceLanguage = "es",
                TargetLocale = "en-US",
            },
            TimeSpan.FromMinutes(20));

        var service = CreateService(repository, provider, cache);
        var result = await service.TranslateAsync(ReviewId, "en-US");

        Assert.Equal(ReviewTranslationOutcome.Translated, result.Outcome);
        Assert.Equal("Hello world", result.TranslatedText);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task TranslateAsyncCallsProviderOnCacheMissAndCachesResult()
    {
        var cache = new FakeCacheService();
        var provider = new FakeTranslationProvider
        {
            Result = new ReviewTranslationProviderResult("Merhaba", "en", false),
        };
        var repository = new FakeTranslationReviewRepository(
            new ReviewTranslationSource(ReviewId, "Hello", UpdatedAt));

        var service = CreateService(repository, provider, cache);
        var result = await service.TranslateAsync(ReviewId, "tr-TR");

        Assert.Equal("Merhaba", result.TranslatedText);
        Assert.Equal(1, provider.CallCount);
        Assert.False(provider.UsedAuthoringLocaleHint);

        var cached = await cache.GetAsync<ReviewTranslationCacheEntry>(
            ReviewTranslationCacheKeys.For(ReviewId, UpdatedAt, "tr-TR"));
        Assert.NotNull(cached);
        Assert.Equal("Merhaba", cached!.TranslatedText);
    }

    [Fact]
    public async Task TranslateAsyncReturnsSourceMatchesTargetWithoutTranslatedText()
    {
        var provider = new FakeTranslationProvider
        {
            Result = new ReviewTranslationProviderResult(null, "en", true),
        };
        var repository = new FakeTranslationReviewRepository(
            new ReviewTranslationSource(ReviewId, "Hello", UpdatedAt));

        var service = CreateService(repository, provider, new FakeCacheService());
        var result = await service.TranslateAsync(ReviewId, "en-US");

        Assert.Equal(ReviewTranslationOutcome.SourceMatchesTarget, result.Outcome);
        Assert.Null(result.TranslatedText);
    }

    [Fact]
    public async Task TranslateAsyncThrowsWhenReviewMissing()
    {
        var service = CreateService(
            new FakeTranslationReviewRepository(null),
            new FakeTranslationProvider(),
            new FakeCacheService());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.TranslateAsync(ReviewId, "en-US"));
    }

    [Fact]
    public async Task TranslateAsyncThrowsWhenProviderUnavailable()
    {
        var provider = new FakeTranslationProvider { ThrowUnavailable = true };
        var repository = new FakeTranslationReviewRepository(
            new ReviewTranslationSource(ReviewId, "Hello", UpdatedAt));

        var service = CreateService(repository, provider, new FakeCacheService());

        await Assert.ThrowsAsync<ReviewTranslationUnavailableException>(() =>
            service.TranslateAsync(ReviewId, "tr-TR"));
    }

    [Fact]
    public async Task TranslateAsyncUsesUpdatedAtInCacheKey()
    {
        var cache = new FakeCacheService();
        var provider = new FakeTranslationProvider
        {
            Result = new ReviewTranslationProviderResult("Yeni", "en", false),
        };
        var repository = new FakeTranslationReviewRepository(
            new ReviewTranslationSource(ReviewId, "New", UpdatedAt.AddHours(1)));

        var service = CreateService(repository, provider, cache);
        await service.TranslateAsync(ReviewId, "tr-TR");

        Assert.Null(await cache.GetAsync<ReviewTranslationCacheEntry>(
            ReviewTranslationCacheKeys.For(ReviewId, UpdatedAt, "tr-TR")));
        Assert.NotNull(await cache.GetAsync<ReviewTranslationCacheEntry>(
            ReviewTranslationCacheKeys.For(ReviewId, UpdatedAt.AddHours(1), "tr-TR")));
    }

    private static ReviewTranslationService CreateService(
        IReviewRepository repository,
        IReviewTranslationProvider provider,
        ICacheService cache) =>
        new(repository, provider, cache, Microsoft.Extensions.Logging.Abstractions.NullLogger<ReviewTranslationService>.Instance);

    private sealed class FakeTranslationProvider : IReviewTranslationProvider
    {
        public int CallCount { get; private set; }

        public bool UsedAuthoringLocaleHint { get; private set; }

        public ReviewTranslationProviderResult Result { get; init; } =
            new("Translated", "es", false);

        public bool ThrowUnavailable { get; init; }

        public Task<ReviewTranslationProviderResult> TranslateAsync(
            string text,
            string targetContentLocale,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            UsedAuthoringLocaleHint = false;

            if (ThrowUnavailable)
            {
                throw new ReviewTranslationProviderException("Unavailable");
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class FakeCacheService : ICacheService
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            if (!_values.TryGetValue(key, out var json))
            {
                return Task.FromResult<T?>(null);
            }

            return Task.FromResult(System.Text.Json.JsonSerializer.Deserialize<T>(json));
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class
        {
            _values[key] = System.Text.Json.JsonSerializer.Serialize(value);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTranslationReviewRepository(ReviewTranslationSource? source) : IReviewRepository
    {
        public Task<ReviewTranslationSource?> GetTranslationSourceByIdAsync(
            Guid reviewId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(source is not null && source.Id == reviewId ? source : null);

        public Task<Review?> GetByUserAndMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Review?> GetByUserAndTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Review?> GetTrackedByUserAndMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Review?> GetTrackedByUserAndTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExistsForMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExistsForTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Review> AddAsync(Review review, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(Review review, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteForMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteForTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<PublicReviewListItem> Reviews, int TotalCount)> GetPublicReviewsForMovieAsync(
            Guid movieId,
            int page,
            int pageSize,
            ReviewListSort sort,
            int? ratingStars = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<PublicReviewListItem> Reviews, int TotalCount)> GetPublicReviewsForTvShowAsync(
            Guid tvShowId,
            int page,
            int pageSize,
            ReviewListSort sort,
            int? ratingStars = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, int>> GetReviewScoreDistributionForMovieAsync(
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, int>> GetReviewScoreDistributionForTvShowAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

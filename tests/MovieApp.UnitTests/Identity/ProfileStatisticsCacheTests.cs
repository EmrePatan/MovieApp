using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Favorites;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Favorites;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;
using MovieApp.UnitTests.Caching;

namespace MovieApp.UnitTests.Identity;

public sealed class ProfileStatisticsCacheTests
{
    [Fact]
    public async Task GetStatisticsAsyncUsesSeparateCacheKeysPerTimezone()
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "user@example.com", "hash", "User", DateTime.UtcNow);
        var repository = new CountingStatisticsRepository();
        var cache = new RecordingCacheService();
        var profileStatisticsCache = new ProfileStatisticsCache(cache);
        var service = CreateUserProfileService(user, repository, profileStatisticsCache);

        await service.GetStatisticsAsync("Europe/Istanbul");
        await service.GetStatisticsAsync("America/New_York");

        Assert.Equal(2, repository.CallCount);
        Assert.Equal(2, cache.StoredStatisticsKeys.Count);
        Assert.Contains(
            ProfileStatisticsCacheKeys.Create(userId, "Europe/Istanbul", 0),
            cache.StoredStatisticsKeys);
        Assert.Contains(
            ProfileStatisticsCacheKeys.Create(userId, "America/New_York", 0),
            cache.StoredStatisticsKeys);
    }

    [Fact]
    public async Task GetStatisticsAsyncReturnsCachedResultWithoutRepositoryCall()
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "user@example.com", "hash", "User", DateTime.UtcNow);
        var repository = new CountingStatisticsRepository();
        var cache = new RecordingCacheService();
        var profileStatisticsCache = new ProfileStatisticsCache(cache);
        var service = CreateUserProfileService(user, repository, profileStatisticsCache);

        var first = await service.GetStatisticsAsync("Europe/Istanbul");
        var second = await service.GetStatisticsAsync("Europe/Istanbul");

        Assert.Equal(first, second);
        Assert.Equal(1, repository.CallCount);
    }

    [Fact]
    public async Task InvalidateForUserAsyncPreventsReturningPreviousCachedStatistics()
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "user@example.com", "hash", "User", DateTime.UtcNow);
        var repository = new CountingStatisticsRepository();
        var cache = new RecordingCacheService();
        var profileStatisticsCache = new ProfileStatisticsCache(cache);
        var service = CreateUserProfileService(user, repository, profileStatisticsCache);

        await service.GetStatisticsAsync("Europe/Istanbul");
        await profileStatisticsCache.InvalidateForUserAsync(userId);
        await service.GetStatisticsAsync("Europe/Istanbul");

        Assert.Equal(2, repository.CallCount);
    }

    [Fact]
    public async Task InvalidateForUserAsyncDoesNotAffectOtherUsers()
    {
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();
        var userA = User.Create(userAId, "a@example.com", "hash", "User A", DateTime.UtcNow);
        var userB = User.Create(userBId, "b@example.com", "hash", "User B", DateTime.UtcNow);
        var repositoryA = new CountingStatisticsRepository();
        var repositoryB = new CountingStatisticsRepository();
        var cache = new RecordingCacheService();
        var profileStatisticsCache = new ProfileStatisticsCache(cache);
        var serviceA = CreateUserProfileService(userA, repositoryA, profileStatisticsCache);
        var serviceB = CreateUserProfileService(userB, repositoryB, profileStatisticsCache);

        await serviceA.GetStatisticsAsync("Europe/Istanbul");
        await serviceB.GetStatisticsAsync("Europe/Istanbul");
        await profileStatisticsCache.InvalidateForUserAsync(userAId);

        await serviceA.GetStatisticsAsync("Europe/Istanbul");
        await serviceB.GetStatisticsAsync("Europe/Istanbul");

        Assert.Equal(2, repositoryA.CallCount);
        Assert.Equal(1, repositoryB.CallCount);
    }

    [Fact]
    public async Task FavoriteMutationInvalidatesProfileStatisticsCacheForUser()
    {
        var userId = Guid.NewGuid();
        var movieId = Guid.NewGuid();
        var cache = new RecordingCacheService();
        var profileStatisticsCache = new ProfileStatisticsCache(cache);
        var insightsCache = new InsightsCache(cache);
        var analyticsCacheInvalidator = new UserAnalyticsCacheInvalidator(profileStatisticsCache, insightsCache);
        var service = new AddMovieFavoriteService(
            new FakeCurrentUser(userId),
            new FakeFavoriteRepository(exists: false, tryAddReturns: true),
            new FakeMovieRepository(movieId),
            analyticsCacheInvalidator);

        var result = await service.AddAsync(movieId);

        Assert.Equal(FavoriteMutationResult.Created, result);
        Assert.Equal(1L, await cache.GetGenerationAsync(userId));
    }

    [Fact]
    public async Task InvalidateForUserAsyncDoesNotThrowWhenCacheWriteFails()
    {
        var cache = new ThrowingCacheService();
        var profileStatisticsCache = new ProfileStatisticsCache(cache);

        await profileStatisticsCache.InvalidateForUserAsync(Guid.NewGuid());
    }

    private static UserProfileService CreateUserProfileService(
        User user,
        CountingStatisticsRepository repository,
        IProfileStatisticsCache profileStatisticsCache) =>
        new(
            new FakeCurrentUser(user.Id),
            new FakeUserRepository(user),
            repository,
            profileStatisticsCache,
            new FakePasswordHasher(),
            new FakeTokenService());

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class CountingStatisticsRepository : IUserStatisticsRepository
    {
        public int CallCount { get; private set; }

        public Task<UserStatisticsResult> GetStatisticsAsync(
            Guid userId,
            string? timeZoneId = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(
                ProfileStatisticsBuilder.Build(
                    new ProfileStatisticsRawData(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], [], [], [], null, null),
                    DateTime.UtcNow));
        }
    }

    private sealed class RecordingCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();

        public List<string> StoredStatisticsKeys { get; } = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            return Task.FromResult(_entries.TryGetValue(key, out var value) ? value as T : null);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class
        {
            _entries[key] = value;
            if (key.StartsWith(ProfileStatisticsCacheKeys.Prefix, StringComparison.Ordinal) &&
                !key.StartsWith($"{ProfileStatisticsCacheKeys.Prefix}generation:", StringComparison.Ordinal))
            {
                StoredStatisticsKeys.Add(key);
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }

        public async Task<long> GetGenerationAsync(Guid userId)
        {
            var generation = await GetAsync<ProfileStatisticsGenerationState>(
                ProfileStatisticsCacheKeys.Generation(userId));
            return generation?.Value ?? 0;
        }
    }

    private sealed class ThrowingCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            throw new InvalidOperationException("cache unavailable");

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class =>
            throw new InvalidOperationException("cache unavailable");

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("cache unavailable");
    }

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user.Id == id ? user : null);

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user.Id == id ? user : null);

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(user.SecurityStamp);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user.Id == id);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => "hash";

        public bool VerifyPassword(string password, string passwordHash) => true;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public AccessTokenResult CreateAccessToken(TokenUserContext user) =>
            new("token", DateTime.UtcNow.AddHours(1));
    }

    private sealed class FakeFavoriteRepository(bool exists, bool tryAddReturns) : IFavoriteRepository
    {
        public Task<bool> ExistsForMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default) =>
            Task.FromResult(exists);

        public Task<bool> ExistsForTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlySet<Guid>> GetFavoritedMovieIdsAsync(
            Guid userId,
            IReadOnlyCollection<Guid> movieIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

        public Task<IReadOnlySet<Guid>> GetFavoritedTvShowIdsAsync(
            Guid userId,
            IReadOnlyCollection<Guid> tvShowIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

        public Task<bool> TryAddAsync(Favorite favorite, CancellationToken cancellationToken = default) =>
            Task.FromResult(tryAddReturns);

        public Task<bool> RemoveForMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> RemoveForTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<(IReadOnlyList<Favorite> Favorites, int TotalCount)> GetUserFavoritesAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<Favorite>, int)>(([], 0));
    }

    private sealed class FakeMovieRepository(Guid movieId) : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == movieId ? new Movie { Id = movieId, Title = "Movie", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow } : null);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie> UpsertFromProviderAsync(
            Application.Models.Providers.MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}

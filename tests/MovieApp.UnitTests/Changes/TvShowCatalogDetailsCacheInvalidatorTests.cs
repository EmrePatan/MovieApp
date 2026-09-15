using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Changes;

namespace MovieApp.UnitTests.Changes;

public sealed class TvShowCatalogDetailsCacheInvalidatorTests
{
    [Fact]
    public async Task InvalidateAsync_RemovesDetailSeasonAndEpisodeKeys()
    {
        var cache = new RecordingCacheService();
        var invalidator = new TvShowCatalogDetailsCacheInvalidator(cache);
        var tvShowId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        await invalidator.InvalidateAsync(
            tvShowId,
            [new TmdbChangesHydratedSeasonCacheTarget(1, [1, 2])]);

        Assert.Contains(TvShowDetailsCacheKeys.Create(tvShowId), cache.RemovedKeys);
        Assert.Contains(TvShowSeasonCacheKeys.Create(tvShowId, 1), cache.RemovedKeys);
        Assert.Contains(TvShowEpisodeCacheKeys.Create(tvShowId, 1, 1), cache.RemovedKeys);
        Assert.Contains(TvShowEpisodeCacheKeys.Create(tvShowId, 1, 2), cache.RemovedKeys);
    }

    private sealed class RecordingCacheService : ICacheService
    {
        public List<string> RemovedKeys { get; } = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult<T?>(default);

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            RemovedKeys.Add(key);
            return Task.CompletedTask;
        }
    }
}

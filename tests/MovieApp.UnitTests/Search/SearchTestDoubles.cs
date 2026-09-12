using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using Microsoft.Extensions.Options;

namespace MovieApp.UnitTests.Search;

internal static class SearchTestDoubles
{
    internal static SearchOptions CreateOptions(
        TimeSpan? cacheDuration = null,
        TimeSpan? refreshInterval = null,
        TimeSpan? lockDuration = null) =>
        new()
        {
            CacheDuration = cacheDuration ?? TimeSpan.FromHours(1),
            ProviderRefreshInterval = refreshInterval ?? TimeSpan.FromHours(24),
            ProviderRefreshLockDuration = lockDuration ?? TimeSpan.FromSeconds(30)
        };

    internal static IOptions<SearchOptions> CreateOptionsMonitor(SearchOptions? options = null) =>
        Options.Create(options ?? CreateOptions());

    internal sealed class FakeSearchProviderRefreshRepository : ISearchProviderRefreshRepository
    {
        private readonly Dictionary<(string Query, SearchContentType Type, int Page), DateTime> _entries = new();

        public int SetCount { get; private set; }

        public void Seed(string normalizedQuery, SearchContentType type, int page, DateTime refreshedAtUtc) =>
            _entries[(normalizedQuery, type, page)] = refreshedAtUtc;

        public Task<DateTime?> GetLastRefreshedAtUtcAsync(
            string normalizedQuery,
            SearchContentType contentType,
            int page,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_entries.TryGetValue((normalizedQuery, contentType, page), out var value)
                ? value
                : (DateTime?)null);

        public Task SetLastRefreshedAtUtcAsync(
            string normalizedQuery,
            SearchContentType contentType,
            int page,
            DateTime refreshedAtUtc,
            CancellationToken cancellationToken = default)
        {
            SetCount++;
            _entries[(normalizedQuery, contentType, page)] = refreshedAtUtc;
            return Task.CompletedTask;
        }
    }

    internal sealed class InMemorySearchRefreshLockService : ISearchRefreshLockService
    {
        private readonly LockState _state = new();

        public int AcquireAttempts { get; private set; }

        public int SuccessfulAcquires { get; private set; }

        public int ReleaseCount { get; private set; }

        public bool ForceDenyAcquire { get; set; }

        public Task<SearchRefreshLockHandle?> TryAcquireAsync(
            string lockKey,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default)
        {
            lock (_state.Sync)
            {
                AcquireAttempts++;

                if (ForceDenyAcquire)
                {
                    return Task.FromResult<SearchRefreshLockHandle?>(null);
                }

                if (_state.Locks.TryGetValue(lockKey, out var existing) &&
                    existing.ExpiresAtUtc > DateTime.UtcNow)
                {
                    return Task.FromResult<SearchRefreshLockHandle?>(null);
                }

                var token = Guid.NewGuid().ToString("N");
                _state.Locks[lockKey] = new LockEntry(token, DateTime.UtcNow.Add(lockDuration));
                SuccessfulAcquires++;
                return Task.FromResult<SearchRefreshLockHandle?>(new SearchRefreshLockHandle(lockKey, token));
            }
        }

        public Task ReleaseAsync(
            string lockKey,
            string lockToken,
            CancellationToken cancellationToken = default)
        {
            lock (_state.Sync)
            {
                ReleaseCount++;

                if (_state.Locks.TryGetValue(lockKey, out var existing) &&
                    existing.Token == lockToken)
                {
                    _state.Locks.Remove(lockKey);
                }
            }

            return Task.CompletedTask;
        }

        public void ExpireLock(string lockKey)
        {
            lock (_state.Sync)
            {
                if (_state.Locks.TryGetValue(lockKey, out var existing))
                {
                    _state.Locks[lockKey] = existing with { ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1) };
                }
            }
        }

        public bool IsLocked(string lockKey)
        {
            lock (_state.Sync)
            {
                return _state.Locks.TryGetValue(lockKey, out var existing) &&
                       existing.ExpiresAtUtc > DateTime.UtcNow;
            }
        }

        private sealed class LockState
        {
            public object Sync { get; } = new();

            public Dictionary<string, LockEntry> Locks { get; } = new(StringComparer.Ordinal);
        }

        private sealed record LockEntry(string Token, DateTime ExpiresAtUtc);
    }

    internal sealed class FakeProviderIngestionService : IUnifiedSearchProviderIngestionService
    {
        public int IngestCount { get; private set; }

        public SearchCriteria? LastCriteria { get; private set; }

        public bool MovieSucceeds { get; set; } = true;

        public bool TvSucceeds { get; set; } = true;

        public bool ThrowBeforeResult { get; set; }

        public Task<UnifiedSearchProviderIngestionResult> IngestAsync(
            SearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            IngestCount++;
            LastCriteria = criteria;

            if (ThrowBeforeResult)
            {
                throw new InvalidOperationException("provider unavailable");
            }

            var movieRequired = criteria.Type is SearchContentType.Movie or SearchContentType.All;
            var tvRequired = criteria.Type is SearchContentType.Tv or SearchContentType.All;

            return Task.FromResult(new UnifiedSearchProviderIngestionResult(
                movieRequired,
                tvRequired,
                movieRequired,
                tvRequired,
                movieRequired && MovieSucceeds,
                tvRequired && TvSucceeds));
        }
    }

    internal sealed class FailOpenSearchRefreshLockService : ISearchRefreshLockService
    {
        public int AcquireCount { get; private set; }

        public Task<SearchRefreshLockHandle?> TryAcquireAsync(
            string lockKey,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default)
        {
            AcquireCount++;
            return Task.FromResult<SearchRefreshLockHandle?>(
                new SearchRefreshLockHandle(lockKey, Guid.NewGuid().ToString("N")));
        }

        public Task ReleaseAsync(
            string lockKey,
            string lockToken,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

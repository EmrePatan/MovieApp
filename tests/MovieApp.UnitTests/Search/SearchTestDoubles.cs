using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Infrastructure.Caching;
using MovieApp.Infrastructure.Configuration;

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

    internal static ISearchRefreshCompletionSignal CreateCompletionSignal() =>
        new SearchRefreshCompletionSignal(
            new ServiceCollection().BuildServiceProvider(),
            Options.Create(new RedisOptions { ConnectionString = string.Empty }),
            new LocalSearchRefreshCompletionRegistry(),
            NullLogger<SearchRefreshCompletionSignal>.Instance);

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
        private readonly LocalSearchRefreshSingleFlightGate _localGate = new();

        public int AcquireAttempts { get; private set; }

        public int SuccessfulAcquires { get; private set; }

        public int ReleaseCount { get; private set; }

        public bool ForceDenyAcquire { get; set; }

        public Task<SearchRefreshLockHandle?> TryAcquireAsync(
            string lockKey,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default)
        {
            AcquireAttempts++;

            if (ForceDenyAcquire)
            {
                return Task.FromResult<SearchRefreshLockHandle?>(null);
            }

            if (_localGate.TryAcquire(lockKey, out var lockToken))
            {
                SuccessfulAcquires++;
                return Task.FromResult<SearchRefreshLockHandle?>(
                    new SearchRefreshLockHandle(lockKey, lockToken, SearchRefreshLockBackend.LocalSingleFlight));
            }

            return Task.FromResult<SearchRefreshLockHandle?>(null);
        }

        public Task ReleaseAsync(
            string lockKey,
            string lockToken,
            SearchRefreshLockBackend backend,
            CancellationToken cancellationToken = default)
        {
            ReleaseCount++;
            _localGate.Release(lockKey, lockToken);
            return Task.CompletedTask;
        }

        public Task<bool> TryRenewAsync(
            string lockKey,
            string lockToken,
            SearchRefreshLockBackend backend,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_localGate.VerifyOwnership(lockKey, lockToken));
    }

    internal sealed class LocalSearchRefreshSingleFlightGate
    {
        private readonly Dictionary<string, bool> _held = new(StringComparer.Ordinal);

        public bool TryAcquire(string lockKey, out string lockToken)
        {
            lock (_held)
            {
                if (_held.TryGetValue(lockKey, out var held) && held)
                {
                    lockToken = string.Empty;
                    return false;
                }

                _held[lockKey] = true;
                lockToken = Guid.NewGuid().ToString("N");
                return true;
            }
        }

        public void Release(string lockKey, string lockToken)
        {
            lock (_held)
            {
                if (_held.ContainsKey(lockKey))
                {
                    _held[lockKey] = false;
                }
            }
        }

        public bool VerifyOwnership(string lockKey, string lockToken)
        {
            lock (_held)
            {
                return _held.TryGetValue(lockKey, out var held) && held;
            }
        }
    }

    internal sealed class FakeProviderIngestionService : IUnifiedSearchProviderIngestionService
    {
        private static readonly SearchItem MovieItem = new(
            Guid.NewGuid(),
            "movie",
            "Inception",
            null,
            "Overview",
            "/poster.jpg",
            null,
            new DateOnly(2010, 7, 16),
            8.8m,
            32000,
            2010);

        private static readonly SearchItem TvItem = new(
            Guid.NewGuid(),
            "tv",
            "Friends",
            null,
            "Overview",
            "/poster.jpg",
            null,
            new DateOnly(1994, 9, 22),
            8.9m,
            12000,
            1994);

        private int _ingestCount;

        public int IngestCount => _ingestCount;

        public SearchCriteria? LastCriteria { get; private set; }

        public bool MovieSucceeds { get; set; } = true;

        public bool TvSucceeds { get; set; } = true;

        public bool ThrowBeforeResult { get; set; }

        public bool ThrowOnAutocomplete { get; set; }

        public int ArtificialDelayMilliseconds { get; set; }

        public async Task<UnifiedSearchProviderIngestionResult> IngestAsync(
            SearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _ingestCount);
            LastCriteria = criteria;

            if (ArtificialDelayMilliseconds > 0)
            {
                await Task.Delay(ArtificialDelayMilliseconds, cancellationToken);
            }

            if (ThrowBeforeResult)
            {
                throw new InvalidOperationException("provider unavailable");
            }

            var movieRequired = criteria.Type is SearchContentType.Movie or SearchContentType.All;
            var tvRequired = criteria.Type is SearchContentType.Tv or SearchContentType.All;
            var movieSucceeded = movieRequired && MovieSucceeds;
            var tvSucceeded = tvRequired && TvSucceeds;

            PaginatedResult<SearchItem>? result = null;
            if (movieSucceeded || tvSucceeded)
            {
                result = CreateProviderResult(criteria, movieSucceeded, tvSucceeded);
            }

            return new UnifiedSearchProviderIngestionResult(
                movieRequired,
                tvRequired,
                movieRequired,
                tvRequired,
                movieSucceeded,
                tvSucceeded,
                result);
        }

        public Task<IReadOnlyList<SearchSuggestion>> GetAutocompleteSuggestionsAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnAutocomplete)
            {
                throw new InvalidOperationException("autocomplete provider unavailable");
            }

            var title = QueryTitle(query);
            IReadOnlyList<SearchSuggestion> suggestions =
            [
                new(MovieItem.Id, "movie", title, MovieItem.PosterUrl),
                new(TvItem.Id, "tv", title, TvItem.PosterUrl)
            ];

            return Task.FromResult<IReadOnlyList<SearchSuggestion>>(
                suggestions.Take(limit).ToList());
        }

        private static PaginatedResult<SearchItem> CreateProviderResult(
            SearchCriteria criteria,
            bool includeMovie,
            bool includeTv)
        {
            var title = QueryTitle(criteria.Query);
            var items = new List<SearchItem>();

            if (includeMovie)
            {
                items.Add(MovieItem with { Id = Guid.NewGuid(), Title = title });
            }

            if (includeTv)
            {
                items.Add(TvItem with { Id = Guid.NewGuid(), Title = title });
            }

            var totalCount = criteria.Type switch
            {
                SearchContentType.Movie => includeMovie ? Math.Max(items.Count, 2) : 0,
                SearchContentType.Tv => includeTv ? Math.Max(items.Count, 2) : 0,
                _ => (includeMovie ? 2 : 0) + (includeTv ? 2 : 0)
            };

            return new PaginatedResult<SearchItem>(
                items,
                criteria.Page,
                criteria.PageSize,
                totalCount,
                totalCount == 0 ? 0 : 1);
        }

        private static string QueryTitle(string? query) =>
            string.IsNullOrWhiteSpace(query)
                ? "Result"
                : char.ToUpperInvariant(query[0]) + query[1..];
    }

    internal sealed class CountingProviderIngestionService : IUnifiedSearchProviderIngestionService
    {
        private static readonly SearchItem MovieItem = new(
            Guid.NewGuid(),
            "movie",
            "Result",
            null,
            null,
            null,
            null,
            null,
            8m,
            100,
            null);

        private int _ingestCount;

        public int IngestCount => _ingestCount;

        public int ArtificialDelayMilliseconds { get; set; }

        public Task<UnifiedSearchProviderIngestionResult> IngestAsync(
            SearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _ingestCount);

            if (ArtificialDelayMilliseconds > 0)
            {
                return Task.Run(async () =>
                {
                    await Task.Delay(ArtificialDelayMilliseconds, cancellationToken);
                    return UnifiedSearchProviderIngestionResult.NotRequired();
                }, cancellationToken);
            }

            var result = new PaginatedResult<SearchItem>(
                [MovieItem],
                criteria.Page,
                criteria.PageSize,
                1,
                1);

            return Task.FromResult(new UnifiedSearchProviderIngestionResult(
                true,
                true,
                true,
                true,
                true,
                true,
                result));
        }

        public Task<IReadOnlyList<SearchSuggestion>> GetAutocompleteSuggestionsAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SearchSuggestion>>([]);
    }
}
